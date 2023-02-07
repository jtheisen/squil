using Microsoft.Data.SqlClient;
using System.Data;
using TaskLedgering;

namespace Squil;

public class NoSuchObjectException : Exception
{
    public NoSuchObjectException(ObjectName name)
    {
        ObjectName = name;
    }

    public ObjectName ObjectName { get; }
}

public class NoSuchIndexException : Exception
{
    public NoSuchIndexException(ObjectName table, String index)
    {
        Table = table;
        Index = index;
    }

    public ObjectName Table { get; }
    public String Index { get; }
}

public class UiQueryRunner : IDisposable
{
    static Logger log = LogManager.GetCurrentClassLogger();

    ILiveSourceProvider connections;

    ConnectionHolder currentConnectionHolder;

    LifetimeLogger<UiQueryRunner> lifetimeLogger = new LifetimeLogger<UiQueryRunner>();

    Boolean isDisposed;

    public Boolean IsDisposed => isDisposed;

    public ConnectionHolder CurrentConnectionHolder => currentConnectionHolder;

    public UiQueryRunner(ILiveSourceProvider connections, ConnectionHolder currentConnectionHolder)
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

    public UiQueryState StartQuery(LiveSource source, String connectionName, UiQueryRequest request)
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

        var isRoot = table is null;

        var settings = connections.AppSettings;

        var state = new UiQueryState
        {
            RequestNo = request.RequestNo,
            RootName = connectionName,
            RootUrl = $"/ui/{connectionName}",
            Source = source
        };

        if (source.ExceptionOnModelBuilding is not null)
        {
            return SetError(state, source.ExceptionOnModelBuilding);
        }

        var objectName = table?.Apply(t => new ObjectName(schema, t));

        var cmTable = isRoot ? source.CircularModel.RootTable : source.CircularModel.GetTableOrNull(objectName);

        if (cmTable is null)
        {
            return SetError(state, new NoSuchObjectException(objectName));
        }

        var extentFactory = new ExtentFactory(2);

        state.Table = cmTable;

        Extent extent;

        ExtentFactory.PrincipalLocation principalLocation = null;

        if (isRoot)
        {
            state.QueryType = UiQueryType.Root;
            extent = extentFactory.CreateRootExtentForRoot(cmTable);
        }
        else
        {
            var isInsertQuery = request.OperationType == LocationQueryOperationType.Insert;

            CMIndexlike cmIndex = null;

            if (index is not null)
            {
                cmIndex = state.Index = cmTable.Indexes.GetValueOrDefault(index);

                if (cmIndex is null)
                {
                    return SetError(state, new NoSuchIndexException(objectName, index));
                }
            }

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

            UiQueryType GetQueryType()
            {
                if (isSingletonQuery)
                {
                    if (location.Column != null)
                    {
                        return UiQueryType.Column;
                    }
                    else
                    {
                        return UiQueryType.Row;
                    }
                }

                if ((keyValueCount ?? 0) == 0) return UiQueryType.Table;

                return UiQueryType.TableSlice;
            }

            state.QueryType = GetQueryType();
            state.SearchMode = searchMode;
            state.ExtentFlavorType = extentFlavorType;
            state.ValidatedColumns = columnValues;
            state.HaveValidationIssues = !(columnValues?.All(r => r.IsOk) ?? true);
            state.MayScan = defaultSearchMode == QuerySearchMode.Scan; // ie "is small table/index"

            var primaryExtent = extent.Children.Single(c => c.RelationAlias == "primary");

            state.PrimaryIdPredicateSql = source.QueryGenerator.GetIdPredicateSql(primaryExtent, "\ue000");

            if (principalLocation != null)
            {
                state.PrincipalRelation = principalLocation.Relation;
            }
        }

        state.Extent = extent;

        if (state.HaveValidationIssues) return state;

        source.SetConnectionInHolder(currentConnectionHolder);

        state.Task = RunQuery(request, state);

        state.Task.ContinueWith((task, result) =>
        {
            log.Info($"Ran {state}");
        },
        null);

        return state;
    }

    async Task<UiQueryResult> RunQueryInternal(SqlConnection connection, UiQueryRequest request, UiQueryState query)
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

        var result = new UiQueryResult(query.RequestNo, entity);

        if (query.QueryType != UiQueryType.Root)
        {
            if (result.PrimaryEntities == null) throw new Exception($"Unexpectedly no primary entities");
        }

        if (query.PrincipalRelation != null)
        {
            if (result.PrincipalEntities == null) throw new Exception($"Unexpectedly no principal entities");
        }

        if (request.AccessMode == UiQueryAccessMode.Commit && query.ChangeException == null)
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
        var replayRequired = query.ChangeException != null || request.AccessMode == UiQueryAccessMode.Rollback;

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

    async Task<UiQueryResult> RunQuery(UiQueryRequest request, UiQueryState query)
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

    UiQueryState SetError(UiQueryState state, Exception exception)
    {
        state.Exception = exception;
        state.Task = Task.FromException<UiQueryResult>(exception);
        return state;
    }

    ExtentFactory.PrincipalLocation GetPrincipalLocation(CMTable cmTable, UiQueryRequest request)
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
