using System.Collections.Specialized;
using System.Data.SqlClient;
using TaskLedgering;

namespace Squil;

public enum QueryControllerQueryType
{
    Root,
    Single,
    Table,
    Sublist
}

public class LocationQueryRequest
{
    public String Schema { get; }
    public String Table { get; }
    public String Index { get; }

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
            return segments.GetOrDefault(i)?.TrimEnd('/');
        }

        var section = Get(1);

        switch (section)
        {
            case "views":
            case "tables":
                Schema = Get(2);
                Table = Get(3);
                Index = Get(4);
                break;
            case "indexes":
                Schema = Get(2);
                Index = Get(3);
                break;
        }

        if (Index == UrlRenderer.BlazorDefeatingDummySegment)
        {
            Index = null;
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
    public SqlException Exception { get; set; }

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
    private readonly ConnectionManager connections;

    public LocationQueryRunner(ConnectionManager connections)
    {
        this.connections = connections;
    }

    public LocationQueryResult Query(String connectionName, LocationQueryRequest request)
    {
        try
        {
            return QueryInternal(connectionName, request);
        }
        catch (SchemaChangedException)
        {
            return QueryInternal(connectionName, request);
        }
    }

    LocationQueryResult QueryInternal(String connectionName, LocationQueryRequest request)
    {
        using var ledger = InstallTaskLedger();

        var context = connections.GetContext(connectionName);

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

            ValidationResult GetColumnValue(CMDirectedColumn column)
            {
                var keyValue = request.KeyParams[column.Name];
                var searchValue = request.SearchValues[column.Name];

                var validationResult = column.c.Type.Validate(keyValue, searchValue, column.d, default);

                return validationResult;
            }

            var columnValues = cmIndex?.Columns.Select(GetColumnValue).ToArray();

            var keyValueCount = columnValues?.TakeWhile(cv => cv.IsKeyValue).Count();

            var extentOrder = cmIndex?.Columns.Select(c => c.Name).ToArray();
            var extentValues = columnValues?.TakeWhile(cv => cv.SqlLowerValue != null).Select(cv => cv.GetSqlValue()).ToArray();

            var isSingletonQuery = table == null || (cmIndex != null && cmIndex.IsUnique && extentValues?.Length == extentOrder?.Length && columnValues.All(v => v.IsKeyValue));

            principalLocation = GetPrincipalLocation(cmTable, request);

            var searchMode = request.SearchMode ?? cmIndex?.GetDefaultSearchMode(settings) ?? cmTable.GetDefaultSearchMode(settings);

            if (cmIndex == null && searchMode == QuerySearchMode.Seek)
            {
                searchMode = null;
            }

            var scanValue = searchMode == QuerySearchMode.Scan ? request.SearchValues[""] ?? "" : null;

            extent = extentFactory.CreateRootExtentForTable(
                cmTable,
                isSingletonQuery ? ExtentFlavorType.PageList : ExtentFlavorType.BlockList,
                cmIndex, extentOrder, extentValues, keyValueCount, request.ListLimit,
                principalLocation, scanValue
            );

            result.QueryType = isSingletonQuery ? QueryControllerQueryType.Single : (extentValues?.Length ?? 0) == 0 ? QueryControllerQueryType.Table : QueryControllerQueryType.Sublist;
            result.SearchMode = searchMode;
            result.ValidatedColumns = columnValues;
            result.IsValidationOk = columnValues?.All(r => r.IsOk) ?? true;
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
        finally
        {
            result.Ledger = ledger;
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
