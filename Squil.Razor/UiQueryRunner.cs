using Microsoft.Data.SqlClient;
using System.Data;
using TaskLedgering;

namespace Squil;

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

        var isRoot = table == null;

        var settings = connections.AppSettings;

        var query = new UiQueryState
        {
            RequestNo = request.RequestNo,
            RootName = connectionName,
            RootUrl = $"/ui/{connectionName}",
            Source = source
        };

        if (source.ExceptionOnModelBuilding != null)
        {
            query.Exception = source.ExceptionOnModelBuilding;
            query.Task = Task.FromException<UiQueryResult>(source.ExceptionOnModelBuilding);

            return query;
        }

        var cmTable = isRoot ? source.CircularModel.RootTable : source.CircularModel.GetTable(new ObjectName(schema, table));

        var extentFactory = new ExtentFactory(2);

        query.Table = cmTable;

        Extent extent;

        ExtentFactory.PrincipalLocation principalLocation = null;

        if (isRoot)
        {
            query.QueryType = UiQueryType.Root;
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
