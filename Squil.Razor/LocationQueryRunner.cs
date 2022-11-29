using System.Collections.Specialized;
using TaskLedgering;
using static Squil.ExtentFactory;

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

    public LocationQueryRequest(String[] segments, NameValueCollection queryParams, NameValueCollection searchValues)
    {
        String Get(Int32 i)
        {
            var segment = segments.GetOrDefault(i)?.TrimEnd('/');

            return segment != UrlRenderer.BlazorDefeatingDummySegment ? segment : null;
        }

        Debug.Assert(Get(0) == "ui");

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

[DebuggerDisplay("{ToString()}")]
public class LocationQueryResponse
{
    public QueryControllerQueryType QueryType { get; set; }
    public QuerySearchMode? SearchMode { get; set; }
    public ExtentFlavorType ExtentFlavorType { get; set; }
    public Boolean MayScan { get; set; }
    public String RootUrl { get; set; }
    public String RootName { get; set; }
    public CMTable Table { get; set; }
    public CMIndexlike Index { get; set; }
    public CMRelationEnd PrincipalRelation { get; set; }
    public Boolean HaveValidationIssues { get; set; }
    public ValidationResult[] ValidatedColumns { get; set; }

    public Extent Extent { get; set; }
    public LiveSource Context { get; set; }

    public Task<LocationQueryResult> Task { get; set; }

    public TaskLedger Ledger { get; set; }
    public Exception Exception { get; set; }

    public Boolean IsOk => !HaveValidationIssues && Exception == null;

    public Boolean IsCanceled => Exception is OperationCanceledException;

    public async Task Wait()
    {
        try
        {
            await Task;
        }
        catch (Exception)
        {
        }
    }

    public override String ToString()
    {
        if (IsCanceled) return "canceled";

        if (Exception != null) return $"exception: {Exception.Message}";

        if (HaveValidationIssues) return $"validation issues";

        if (Task == null) return "not started";

        return $"status {Task.Status}{Exception?.Apply(e => $", exception: {e.Message}")}";
    }
}

public class LocationQueryResult
{
    public Entity Entity { get; set; }
    public RelatedEntities PrimaryEntities { get; set; }
    public RelatedEntities PrincipalEntities { get; set; }

    public override String ToString()
    {
        var items = new[] { "entity".If(Entity != null), "primaries".If(PrimaryEntities != null), "principals".If(PrincipalEntities != null) };

        return $"result with {String.Join(", ", items.Where(i => i != null))}";
    }
}

public enum CanLoadMoreStatus
{
    Unavailable,
    Can,
    Complete
}

public class LocationQueryRunner
{
    static Logger log = LogManager.GetCurrentClassLogger();

    ILiveSourceProvider connections;

    ConnectionHolder currentConnectionHolder;

    public ConnectionHolder CurrentConnectionHolder => currentConnectionHolder;

    public LocationQueryRunner(ILiveSourceProvider connections, ConnectionHolder currentConnectionHolder)
    {
        this.connections = connections;

        this.currentConnectionHolder = currentConnectionHolder;
    }

    public void Cancel()
    {
        currentConnectionHolder.CancelAndReset();
    }

    public LiveSource GetLiveSource(String connectionName)
    {
        return connections.GetLiveSource(connectionName);
    }

    public LocationQueryResponse StartQuery(LiveSource source, String connectionName, LocationQueryRequest request)
    {
        if (connections.AppSettings.DebugQueryDelayMillis is Int32 d)
        {
            Thread.Sleep(d);
        }

        var schema = request.Schema;
        var table = request.Table;
        var index = request.Index;

        var isRoot = table == null;

        var settings = connections.AppSettings;

        var query = new LocationQueryResponse
        {
            RootName = connectionName,
            RootUrl = $"/ui/{connectionName}",
            Context = source
        };

        if (source.ExceptionOnModelBuilding != null)
        {
            query.Exception = source.ExceptionOnModelBuilding;
            query.Task = Task.FromException<LocationQueryResult>(source.ExceptionOnModelBuilding);

            return query;
        }

        var cmTable = isRoot ? source.CircularModel.RootTable : source.CircularModel.GetTable(new ObjectName(schema, table));

        var extentFactory = new ExtentFactory(2);

        query.Table = cmTable;

        Extent extent;

        ExtentFactory.PrincipalLocation principalLocation = null;

        if (isRoot)
        {
            query.QueryType = QueryControllerQueryType.Root;
            extent = extentFactory.CreateRootExtentForRoot(cmTable);
        }
        else
        {
            var cmIndex = query.Index = index?.Apply(i => cmTable.Indexes.Get(i, $"Could not find index '{index}' in table '{table}'"));

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

            var extentFlavorType = GetExtentFlavor();

            extent = extentFactory.CreateRootExtentForTable(
                cmTable,
                extentFlavorType,
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

            query.QueryType = GetQueryType();
            query.SearchMode = searchMode;
            query.ExtentFlavorType = extentFlavorType;
            query.ValidatedColumns = columnValues;
            query.HaveValidationIssues = !(columnValues?.All(r => r.IsOk) ?? true);
            query.MayScan = defaultSearchMode == QuerySearchMode.Scan; // ie "is small table/index"
            
            if (principalLocation != null)
            {
                query.PrincipalRelation = principalLocation.Relation;
            }
        }

        query.Extent = extent;

        if (query.HaveValidationIssues) return query;

        source.SetConnectionInHolder(currentConnectionHolder);

        query.Task = RunQuery(query);

        return query;
    }

    async Task<LocationQueryResult> RunQuery(LocationQueryResponse query)
    {
        using var ledger = LedgerControl.InstallTaskLedger();

        query.Ledger = ledger;

        try
        {
            var entity = await currentConnectionHolder.RunAsync(c => query.Context.QueryAsync(c, query.Extent));

            var result = new LocationQueryResult
            {
                Entity = entity
            };

            if (query.QueryType != QueryControllerQueryType.Root)
            {
                result.PrimaryEntities = entity.Related.GetRelatedEntities("primary");
            }

            if (query.PrincipalRelation != null)
            {
                result.PrincipalEntities = entity.Related.GetRelatedEntities("principal");
            }

            return result;
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                log.Info($"Query canceled");
            }
            else
            {
                log.Error(ex, "Query terminated with exception");
            }

            query.Exception = ex;

            throw;
        }

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
