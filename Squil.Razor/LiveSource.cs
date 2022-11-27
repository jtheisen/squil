using Microsoft.Data.SqlClient;
using Nito.AsyncEx;

namespace Squil;

public class SchemaChangedException : Exception
{
    public SchemaChangedException() { }

    public SchemaChangedException(Exception nested)
        : base("The schema has changed", nested)
    { }
}

public enum LiveSourceState
{
    Stale,
    Building,
    Broken,
    Ready
}

public class LiveSource
{
    readonly string connectionString;

    volatile Model currentModel;

    Exception exceptionOnModelBuilding;

    LiveSourceState state = LiveSourceState.Stale;

    AsyncLock modelBuildingLock = new AsyncLock();

    AsyncManualResetEvent isModelReady = new AsyncManualResetEvent();

    record Model(CMRoot CMRoot, QueryGenerator QueryGenerator);

    public LiveSourceState State => state;

    public Exception ExceptionOnModelBuilding => exceptionOnModelBuilding;

    public CMRoot CircularModel => currentModel.CMRoot;

    public QueryGenerator QueryGenerator => currentModel.QueryGenerator;

    public LiveSource(String connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task EnsureModelAsync()
    {
        BeginUpdateModelIfApplicable();

        await isModelReady.WaitAsync();
    }

    public SqlConnection GetConnection()
    {
        var connection = new SqlConnection(connectionString);

        connection.Open();

        return connection;
    }

    public void SetConnectionInHolder(ConnectionHolder holder)
    {
        if (holder.Connection?.ConnectionString != connectionString)
        {
            holder.Connection = new SqlConnection(connectionString);
        }
    }

    public async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        return connection;
    }

    public async Task<Entity> QueryAsync(SqlConnection connection, Extent extent)
    {
        if (currentModel == null || !isModelReady.IsSet)
        {
            throw new SchemaChangedException();
        }

        Entity entity;

        try
        {
            entity = await QueryGenerator.QueryAsync(connection, extent);
        }
        catch (SqlException ex)
        {
            if (CheckModelInvalid())
            {
                InvalidateModel();

                throw new SchemaChangedException(ex);
            }
            else
            {
                throw;
            }
        }

        if (entity.SchemaDate > CircularModel.TimeStamp)
        {
            InvalidateModel();

            throw new SchemaChangedException();
        }
        else
        {
            return entity;
        }
    }

    void InvalidateModel()
    {
        lock (this)
        {
            state = LiveSourceState.Stale;

            isModelReady.Reset();
        }
    }

    async void BeginUpdateModelIfApplicable()
    {
        if (isModelReady.IsSet) return;

        var lockHandle = modelBuildingLock.LockAsync();

        if (isModelReady.IsSet) return;

        state = LiveSourceState.Building;

        await Task.Factory.Run(UpdateModel);
    }

    void UpdateModel()
    {
        try
        {
            using var connection = GetConnection();

            var cmRoot = connection.GetCircularModel();

            var qg = new QueryGenerator(cmRoot);

            currentModel = new Model(cmRoot, qg);

            lock (this)
            {
                exceptionOnModelBuilding = null;

                state = LiveSourceState.Ready;

                isModelReady.Set();
            }
        }
        catch (Exception ex)
        {
            lock (this)
            {
                state = LiveSourceState.Broken;

                exceptionOnModelBuilding = ex;
            }
        }
    }

    Boolean CheckModelInvalid()
    {
        var connection = GetConnection();

        var modifiedAt = connection.GetSchemaModifiedAt();

        if (modifiedAt > currentModel.CMRoot.TimeStamp)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}

public class ConnectionHolder : ObservableObject<ConnectionHolder>, IDisposable
{
    SqlConnection connection;

    CancellationTokenSource tcs = new CancellationTokenSource();

    SemaphoreSlim semaphore = new SemaphoreSlim(1);

    public SqlConnection Connection
    {
        get => connection;
        set
        {
            if (connection != null)
            {
                connection.Dispose();
            }

            connection = value;
        }
    }

    public StallDetective StallDetective { get; private set; }

    public async Task<T> RunAsync<T>(Func<SqlConnection, Task<T>> action)
    {
        var logIds = LogIds;

        if (semaphore.CurrentCount == 0)
        {
            log.Debug($"{logIds} Requesting cancellation of running query");

            Cancel();
        }

        log.Debug($"{logIds} Acquiring lock while semaphore at {semaphore.CurrentCount}");

        tcs = new CancellationTokenSource();

        var ct = tcs.Token;

        await semaphore.WaitAsync();

        try
        {
            StallDetective = null;

            if (ct.IsCancellationRequested)
            {
                log.Debug($"{logIds} Already canceled, won't start");

                throw new OperationCanceledException("Operation canceled before it began");
            }

            NotifyChange();

            if (connection.State == System.Data.ConnectionState.Closed)
            {
                log.Debug($"{logIds} Opening connection");

                await connection.OpenAsync();

                NotifyChange();
            }

            using var _ = StaticServiceStack.Install(ct);

            log.Debug($"{logIds} Starting query");

            var task = action(connection);

            await Task.WhenAny(task, Task.Delay(1000));

            if (!task.IsCompleted)
            {
                log.Debug($"{logIds} Query is delayed, spawning stall detective");

                StallDetective = new StallDetective(connection);

                NotifyChange();

                StallDetective.Investigate().Ignore();
            }

            await task;

            NotifyChange();

            return task.Result;
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException("Operation canceled", ex);
            }
            else
            {
                throw;
            }
        }
        finally
        {
            var previousCount = semaphore.Release();

            log.Debug($"{logIds} Query terminated, semaphore now at {semaphore.CurrentCount}");
        }
    }

    public void Cancel()
    {
        tcs.Cancel();
    }

    public void Dispose()
    {
        Connection = null;
    }
}
