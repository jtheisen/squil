using Humanizer;
using Microsoft.Data.SqlClient;
using System.Collections.Specialized;
using System.Data;
using TaskLedgering;
using static Squil.StaticSqlAliases;

namespace Squil;

public enum QueryControllerQueryType
{
    Root,
    Row,
    Table,
    TableSlice,
    Column
}

public enum LocationQueryAccessMode
{
    QueryOnly,
    Rollback,
    Commit
}

[DebuggerDisplay("{ToString()}")]
public class LocationQueryLocation
{
    public String Source { get; init; }
    public String Schema { get; init; }
    public String Table { get; init; }
    public String Index { get; init; }
    public String Column { get; init; }

    public NameValueCollection KeyParams { get; init; }
    public NameValueCollection RestParams { get; init; }

    public Int32 KeyValuesCount { get; init; }

    public QuerySearchMode? SearchMode { get; init; }

    public String BackRelation { get; init; }

    public LocationQueryLocation()
    {
        KeyParams = new NameValueCollection();
        RestParams = new NameValueCollection();
    }

    public LocationQueryLocation(
        String[] segments,
        NameValueCollection queryParams
    )
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

        var keyKeysArray = KeyParams.Cast<String>().ToArray();

        KeyValuesCount = keyKeysArray.Length;

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

    public override String ToString()
    {
        var writer = new StringWriter();

        var tn = 6;

        writer.Write($"{Source.Truncate(tn)}/{Schema.Truncate(tn)}/{Table.Truncate(tn)}");

        if (Index is not null) writer.Write($"/{Index.Truncate(tn)}");
        if (Column is not null) writer.Write($"/{Column.Truncate(tn)}");

        writer.Write(RestParams.ToTruncatedString('{', '}'));

        return writer.ToString();
    }
}

[DebuggerDisplay("{ToString()}")]
public class LocationQueryRequest
{
    static Int32 staticRequestCount = 0;

    Int32 requestNo = ++staticRequestCount;

    public Int32 RequestNo => requestNo;

    public LocationQueryLocation Location { get; set; }

    public ChangeEntry[] Changes { get; }
    public LocationQueryAccessMode AccessMode { get; }
    public LocationQueryOperationType? OperationType { get; }

    public Int32 ListLimit { get; set; }

    public NameValueCollection SearchValues { get; }

    public Boolean IsIdentitizingPending => OperationType == LocationQueryOperationType.Insert && Changes == null;

    public LocationQueryRequest(
        LocationQueryLocation location,
        NameValueCollection searchValues,
        ChangeEntry[] changes = null,
        LocationQueryAccessMode accessMode = default,
        LocationQueryOperationType? operationType = null
    )
    {
        Location = location;

        Changes = changes?.Select(c => c.Clone()).ToArray();
        AccessMode = accessMode;
        OperationType = operationType;

        SearchValues = searchValues;
    }

    public override String ToString()
    {
        var writer = new StringWriter();

        var cs = Changes?.Length > 0 ? "*" : "";
        var ams = AccessMode.ToString().ToLower().FirstOrDefault();
        var os = OperationType?.Apply(ot => ot.ToString().FirstOrDefault()) ?? '?';

        var ss = SearchValues.ToTruncatedString();

        return $"request #{requestNo} {ams}{cs}{os}{ss} at {Location}";
    }
}

[DebuggerDisplay("{ToString()}")]
public class LocationQueryResponse
{
    public Int32 RequestNo { get; set; }

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
    public String PrimaryIdPredicateSql { get; set; }

    public Extent Extent { get; set; }
    public LiveSource Source { get; set; }

    public Task<LocationQueryResult> Task { get; set; }

    public TaskLedger Ledger { get; set; }
    public Exception ChangeException { get; set; }
    public Exception Exception { get; set; }

    public Boolean IsChangeOk => ChangeException == null;

    public Boolean IsOk => !HaveValidationIssues && Exception == null;

    public Boolean IsRunning => Task is not null && !Task.IsCompleted;
    public Boolean IsCompleted => Task is not null && Task.IsCompleted;

    public Boolean IsCompletedSuccessfully => Task?.IsCompletedSuccessfully ?? false;

    public Boolean IsCanceled => Exception is OperationCanceledException;

    public LocationQueryResult Result => Task?.IsCompletedSuccessfully == true ? Task.Result : null;

    public async Task Wait()
    {
        if (Task is null) return;

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
        return $"response #{RequestNo} {ToStringInner()}";
    }

    String ToStringInner()
    {
        if (IsCanceled) return "is canceled";

        if (Exception != null) return $"has exception: {Exception.Message}";

        if (HaveValidationIssues) return $"has validation issues";

        if (Task == null) return "is not started";

        if (!Task.IsCompleted) return $"has status {Task.Status}";

        if (IsChangeOk) return "is ok";

        return $"has change exception: {ChangeException.Message}";
    }

    public String GetPrimaryIdPredicateSql(String alias)
    {
        var aliasPrefix = String.IsNullOrWhiteSpace(alias) ? "" : alias.EscapeNamePart() + ".";

        return PrimaryIdPredicateSql?.Replace("\ue000", aliasPrefix);
    }
}

public class LocationQueryResult
{
    public Entity Entity { get; }
    public RelatedEntities PrimaryEntities { get; }
    public RelatedEntities PrincipalEntities { get; }
    public Boolean HasCommitted { get; set; }

    public LocationQueryResult(Entity entity)
    {
        Entity = entity;
        PrimaryEntities = entity.Related.GetRelatedEntitiesOrNull(PrimariesRelationAlias);
        PrincipalEntities = entity.Related.GetRelatedEntitiesOrNull(PrincipalRelationAlias);
    }

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

public class LocationQueryRunner : IDisposable
{
    static Logger log = LogManager.GetCurrentClassLogger();

    ILiveSourceProvider connections;

    ConnectionHolder currentConnectionHolder;

    LifetimeLogger<LocationQueryRunner> lifetimeLogger = new LifetimeLogger<LocationQueryRunner>();

    Boolean isDisposed;

    public Boolean IsDisposed => isDisposed;

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
        log.Info($"New {request}");

        if (connections.AppSettings.DebugQueryDelayMillis is Int32 d)
        {
            Thread.Sleep(d);
        }

        var location = request.Location;

        var schema = location.Schema;
        var table = location.Table;
        var index = location.Index;

        var isRoot = table == null;

        var settings = connections.AppSettings;

        var query = new LocationQueryResponse
        {
            RequestNo = request.RequestNo,
            RootName = connectionName,
            RootUrl = $"/ui/{connectionName}",
            Source = source
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
            var isInsertQuery = request.OperationType == LocationQueryOperationType.Insert;

            var cmIndex = query.Index = index?.Apply(i => cmTable.Indexes.Get(i, $"Could not find index '{index}' in table '{table}'"));

            var extentOrder = cmIndex?.Columns.Select(c => c.Name).ToArray();

            var keyValueCount = cmIndex?.Columns?.TakeWhile(cv => !String.IsNullOrWhiteSpace(location.KeyParams[cv.c.Name])).Count();

            var isSingletonQuery = table == null || isInsertQuery || (cmIndex != null && cmIndex.IsUnique && keyValueCount == extentOrder?.Length);

            ValidationResult GetColumnValue(CMDirectedColumn column, Int32 no)
            {
                var keyValue = location.KeyParams[column.Name];
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

                var searchMode = location.SearchMode ?? defaultSearchMode;

                if (cmIndex == null && searchMode == QuerySearchMode.Seek) return null;

                return searchMode;
            }

            ExtentFlavorType GetExtentFlavor()
            {
                if (isSingletonQuery)
                {
                    if (location.Column != null)
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

            Int32? limit = isSingletonQuery ? null : request.ListLimit;

            extent = extentFactory.CreateRootExtentForTable(
                cmTable,
                extentFlavorType,
                cmIndex, extentOrder, extentValues, keyValueCount, limit,
                principalLocation, scanValue, location.Column
            );

            QueryControllerQueryType GetQueryType()
            {
                if (isSingletonQuery)
                {
                    if (location.Column != null)
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

            var primaryExtent = extent.Children.Single(c => c.RelationAlias == "primary");

            query.PrimaryIdPredicateSql = source.QueryGenerator.GetIdPredicateSql(primaryExtent, "\ue000");

            if (principalLocation != null)
            {
                query.PrincipalRelation = principalLocation.Relation;
            }
        }

        query.Extent = extent;

        if (query.HaveValidationIssues) return query;

        source.SetConnectionInHolder(currentConnectionHolder);

        query.Task = RunQuery(request, query);

        query.Task.ContinueWith((task, result) =>
        {
            log.Info($"Ran {query}");
        },
        null);

        return query;
    }

    async Task<LocationQueryResult> RunQueryInternal(SqlConnection connection, LocationQueryRequest request, LocationQueryResponse query)
    {
        var ct = StaticServiceStack.Get<CancellationToken>();

        await using var transaction = request.Changes != null ? await connection.BeginTransactionAsync(ct) : null;

        StaticServiceStack.Install<IDbTransaction>(transaction);

        if (request.Changes is ChangeEntry[] changes)
        {
            foreach (var change in changes)
            {
                try
                {
                    await query.Source.ExcecuteChange(connection, change, query.Table);

                    var primaries = query.Extent.GetPrimariesSubExtent();

                    primaries.Values = change.EntityKey.KeyColumnsAndValues.Select(kv => kv.v).ToArray();
                    primaries.KeyValueCount = primaries.Values.Length;
                }
                catch (SqlException ex)
                {
                    query.ChangeException = ex;

                    break;
                }
            }
        }

        var entity = await query.Source.QueryAsync(connection, query.Extent);

        var result = new LocationQueryResult(entity);

        if (query.QueryType != QueryControllerQueryType.Root)
        {
            if (result.PrimaryEntities == null) throw new Exception($"Unexpectedly no primary entities");
        }

        if (query.PrincipalRelation != null)
        {
            if (result.PrincipalEntities == null) throw new Exception($"Unexpectedly no principal entities");
        }

        if (request.AccessMode == LocationQueryAccessMode.Commit && query.ChangeException == null)
        {
            await transaction.CommitAsync();

            result.HasCommitted = true;
        }

        if (request.OperationType == LocationQueryOperationType.Insert)
        {
            if (request.Changes is null || !query.IsChangeOk)
            {
                // insert without a changes array or with a failed insert means we're initializing with a dummy entity

                var templateEntity = query.Extent.GetPrimariesSubExtent().MakeDummyEntity(DateTime.Now, query.Table);

                templateEntity.InitKeyValuesAsEdited();

                // if we have a key from a previous succesful insert, we need to prime the entity with it so that
                // it gets matched to the change entry later
                if (request.Changes?.FirstOrDefault() is ChangeEntry ce && ce.IsKeyed)
                {
                    templateEntity.SetEntityKey(ce.EntityKey);
                }

                result.PrimaryEntities.List = new[] { templateEntity };
            }
            else
            {
                // insert with a change array and a successful change operation means we should have loaded an inserted entity

                if (result.PrimaryEntities.List.Length == 0)
                {
                    throw new Exception($"Unexpectedly no entity retrieved after insertion");
                }
            }
        }

        // Error-free commits don't replay
        var replayRequired = query.ChangeException != null || request.AccessMode == LocationQueryAccessMode.Rollback;

        if (replayRequired)
        {
            foreach (var change in request.Changes)
            {
                var key = change.EntityKey;

                foreach (var e in result.PrimaryEntities.List)
                {
                    var entityKey = e.GetEntityKey();

                    if (entityKey == key)
                    {
                        e.SetEditValues(change);

                        e.EditState = query.ChangeException is null ? EntityEditState.Validated : EntityEditState.Modified;
                    }
                }
            }
        }

        return result;
    }

    async Task<LocationQueryResult> RunQuery(LocationQueryRequest request, LocationQueryResponse query)
    {
        using var ledger = LedgerControl.InstallTaskLedger();

        query.Ledger = ledger;

        try
        {
            return await currentConnectionHolder.RunAsync(c => RunQueryInternal(c, request, query));
        }
        catch (Exception ex)
        {
            query.Exception = ex;

            throw;
        }
    }

    ExtentFactory.PrincipalLocation GetPrincipalLocation(CMTable cmTable, LocationQueryRequest request)
    {
        var location = request.Location;

        if (location.BackRelation == null) return null;

        var root = cmTable.Root.RootTable;

        var principal = cmTable.Relations[location.BackRelation];

        var principalRelation = root.Relations[principal.Table.Name.Simple];

        var foreignKey = principal.OtherEnd.Key as CMForeignKey;
        var domesticKey = principal.Key as CMIndexlike;

        Debug.Assert(foreignKey != null);
        Debug.Assert(domesticKey != null);
        
        var fkColumns =
            foreignKey.Columns.Select(c => location.KeyParams[c.c.Name]).TakeWhile(v => v != null).ToArray();

        return new ExtentFactory.PrincipalLocation(
            principal.OtherEnd, domesticKey.ColumnNames, fkColumns);
    }

    public void Dispose()
    {
        isDisposed = true;

        lifetimeLogger.Dispose();
    }
}
