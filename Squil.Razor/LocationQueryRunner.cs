using System.Collections.Specialized;
using System.Data.SqlClient;
using TaskLedgering;

namespace Squil;

public enum QueryControllerQueryType
{
    Root,
    Row,
    Table,
    TableSlice,
    Column
}

public class LocationQueryRequest
{
    public String Source { get; }
    public String Schema { get; }
    public String Table { get; }
    public String Index { get; }
    public String Column { get; set; }

    public QuerySearchMode? SearchMode { get; set; }

    public Int32 ListLimit { get; set; }

    public String BackRelation { get; set; }

    public NameValueCollection KeyParams { get; }
    public NameValueCollection RestParams { get; }
    public NameValueCollection SearchValues { get; }

    public LocationQueryRequest(String path, NameValueCollection queryParams, NameValueCollection searchValues)
    {
        var segments = new Uri("https://host/" + path, UriKind.Absolute).Segments;
        
        Debug.Assert(segments.GetOrDefault(0) == "/");

        String Get(Int32 i)
        {
            var segment = segments.GetOrDefault(i)?.TrimEnd('/');

            return segment != UrlRenderer.BlazorDefeatingDummySegment ? segment : null;
        }

        Source = Get(1);

        var section = Get(2);

        switch (section)
        {
            case "views":
            case "tables":
                Schema = Get(3);
                Table = Get(4);
                Index = Get(5);
                Column = Get(6);
                break;
            case "indexes":
                Schema = Get(3);
                Index = Get(4);
                Column = Get(5);
                break;
        }

        if (Enum.TryParse<QuerySearchMode>(queryParams["search"], true, out var searchMode))
        {
            SearchMode = searchMode;
        }

        (var keyParams, var restParams) = SplitParams(queryParams);

        KeyParams = keyParams;
        RestParams = restParams;
        SearchValues = searchValues;

        BackRelation = queryParams["from"];
    }

    static (NameValueCollection keyParams, NameValueCollection restParams) SplitParams(NameValueCollection queryParams)
    {
        var groups =
            from key in queryParams.AllKeys
            group key by key?.StartsWith('$') ?? false into g
            select g;

        var keyParams = (
            from g in groups.Where(g => g.Key == true)
            from key in g
            select (key[1..], queryParams[key])
        ).ToMap().ToNameValueCollection();

        var restParams = (
            from g in groups.Where(g => g.Key == false)
            from key in g
            select (key, queryParams[key])
        ).ToMap().ToNameValueCollection();

        return (keyParams, restParams);
    }
}

public class LocationQueryResult
{
    public QueryControllerQueryType QueryType { get; set; }
    public QuerySearchMode? SearchMode { get; set; }
    public Boolean MayScan { get; set; }
    public String RootUrl { get; set; }
    public String RootName { get; set; }
    public CMTable Table { get; set; }
    public CMIndexlike Index { get; set; }
    public Entity Entity { get; set; }
    public RelatedEntities PrimaryEntities { get; set; }
    public RelatedEntities PrincipalEntities { get; set; }
    public CMRelationEnd PrincipalRelation { get; set; }
    public Boolean IsValidationOk { get; set; }
    public ValidationResult[] ValidatedColumns { get; set; }

    public TaskLedger Ledger { get; set; }
    public Exception Exception { get; set; }

    public Boolean IsOk => IsValidationOk && Exception == null;
}

public enum CanLoadMoreStatus
{
    Unavailable,
    Can,
    Complete
}

public class LocationQueryRunner
{
    private readonly ObjectNameParser parser = new ObjectNameParser();
    private readonly LiveConfiguration connections;

    public LocationQueryRunner(LiveConfiguration connections)
    {
        this.connections = connections;
    }

    public LocationQueryResult Query(String connectionName, LocationQueryRequest request)
    {
        using var ledger = InstallTaskLedger();

        LocationQueryResult result = null;

        try
        {
            result = QueryInternal0(connectionName, request);
        }
        catch (Exception ex)
        {
            result = new LocationQueryResult();
            result.Exception = ex;
        }
        finally
        {
            result.Ledger = ledger;
        }

        return result;
    }

    public LocationQueryResult QueryInternal0(String connectionName, LocationQueryRequest request)
    {
        try
        {
            return QueryInternal1(connectionName, request);
        }
        catch (SchemaChangedException)
        {
            return QueryInternal1(connectionName, request);
        }
    }

    LocationQueryResult QueryInternal1(String connectionName, LocationQueryRequest request)
    {
        var context = connections.GetLiveSource(connectionName);

        var schema = request.Schema;
        var table = request.Table;
        var index = request.Index;

        var isRoot = table == null;

        var settings = connections.AppSettings;

        var cmTable = isRoot ? context.CircularModel.RootTable : context.CircularModel.GetTable(new ObjectName(schema, table));

        var extentFactory = new ExtentFactory(2);

        var result = new LocationQueryResult
        {
            RootName = connectionName,
            RootUrl = $"/query/{connectionName}",
            Table = cmTable
        };

        Extent extent;

        ExtentFactory.PrincipalLocation principalLocation = null;

        if (isRoot)
        {
            result.QueryType = QueryControllerQueryType.Root;
            result.IsValidationOk = true;
            extent = extentFactory.CreateRootExtentForRoot(cmTable);
        }
        else
        {
            var cmIndex = result.Index = index?.Apply(i => cmTable.Indexes.Get(i, $"Could not find index '{index}' in table '{table}'"));

            var extentOrder = cmIndex?.Columns.Select(c => c.Name).ToArray();

            var keyValueCount = cmIndex?.Columns?.TakeWhile(cv => !String.IsNullOrWhiteSpace(request.KeyParams[cv.c.Name])).Count();

            var isSingletonQuery = table == null || (cmIndex != null && cmIndex.IsUnique && keyValueCount == extentOrder?.Length);

            ValidationResult GetColumnValue(CMDirectedColumn column, Int32 no)
            {
                var keyValue = request.KeyParams[column.Name];
                var searchValue = isSingletonQuery ? null : request.SearchValues[column.Name];

                var validationResult = column.c.Type.Validate(no, keyValue, searchValue ?? "", column.d, default);

                return validationResult;
            }

            var columnValues = cmIndex?.Columns.Select(GetColumnValue).ToArray();

            var noOfValuesToUse = columnValues?.Where(r => !String.IsNullOrWhiteSpace(r.Value)).Select(r => r.No + 1).LastOrDefault();

            var extentValues = columnValues?.Take(noOfValuesToUse.Value).Select(cv => cv.GetSqlValue()).ToArray();

            principalLocation = GetPrincipalLocation(cmTable, request);

            // Currently, we alsways scan the entire table and need to ignore the index selection
            var defaultSearchMode = /*cmIndex?.GetDefaultSearchMode(settings) ?? */cmTable.GetDefaultSearchMode(settings);

            QuerySearchMode? GetSearchMode()
            {
                if (isSingletonQuery) return QuerySearchMode.Seek;

                var searchMode = request.SearchMode ?? defaultSearchMode;

                if (cmIndex == null && searchMode == QuerySearchMode.Seek) return null;

                return searchMode;
            }

            ExtentFlavorType GetExtentFlavor()
            {
                if (isSingletonQuery)
                {
                    if (request.Column != null)
                    {
                        return ExtentFlavorType.ColumnPageList;
                    }
                    else
                    {
                        return ExtentFlavorType.PageList;
                    }
                }
                else
                {
                    return ExtentFlavorType.BlockList;
                }
            }

            var searchMode = GetSearchMode();

            var scanValue = searchMode == QuerySearchMode.Scan ? request.SearchValues[""] ?? "" : null;

            extent = extentFactory.CreateRootExtentForTable(
                cmTable,
                GetExtentFlavor(),
                cmIndex, extentOrder, extentValues, keyValueCount, request.ListLimit,
                principalLocation, scanValue, request.Column
            );

            QueryControllerQueryType GetQueryType()
            {
                if (isSingletonQuery)
                {
                    if (request.Column != null)
                    {
                        return QueryControllerQueryType.Column;
                    }
                    else
                    {
                        return QueryControllerQueryType.Row;
                    }
                }

                if ((keyValueCount ?? 0) == 0) return QueryControllerQueryType.Table;

                return QueryControllerQueryType.TableSlice;
            }

            result.QueryType = GetQueryType();
            result.SearchMode = searchMode;
            result.ValidatedColumns = columnValues;
            result.IsValidationOk = columnValues?.All(r => r.IsOk) ?? true;
            result.MayScan = defaultSearchMode == QuerySearchMode.Scan; // ie "is small table/index"
        }

        if (!result.IsValidationOk) return result;

        using var connection = context.GetConnection();

        try
        {
            result.Entity = context.Query(connection, extent);
        }
        catch (SqlException ex)
        {
            result.Exception = ex;

            return result;
        }

        if (!isRoot)
        {
            result.PrimaryEntities = result.Entity.Related.GetRelatedEntities("primary");
        }

        if (principalLocation != null)
        {
            result.PrincipalEntities = result.Entity.Related.GetRelatedEntities("principal");
            result.PrincipalRelation = principalLocation.Relation;
        }

        return result;
    }

    ExtentFactory.PrincipalLocation GetPrincipalLocation(CMTable cmTable, LocationQueryRequest request)
    {
        if (request.BackRelation == null) return null;

        var root = cmTable.Root.RootTable;

        var principal = cmTable.Relations[request.BackRelation];

        var principalRelation = root.Relations[principal.Table.Name.Simple];

        var foreignKey = principal.OtherEnd.Key as CMForeignKey;
        var domesticKey = principal.Key as CMIndexlike;

        Debug.Assert(foreignKey != null);
        Debug.Assert(domesticKey != null);

        var fkColumns =
            foreignKey.Columns.Select(c => request.KeyParams[c.c.Name]).TakeWhile(v => v != null).ToArray();

        return new ExtentFactory.PrincipalLocation(
            principal.OtherEnd, domesticKey.ColumnNames, fkColumns);
    }
}
