using Microsoft.Data.SqlClient;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Squil;

public class SchemaChangedException : Exception
{
    public SchemaChangedException() { }

    public SchemaChangedException(Exception nested)
        : base("The schema has changed", nested)
    { }
}

public class LiveSource
{
    readonly string connectionString;

    volatile Model currentModel;

    Exception exceptionOnModelBuilding;

    record Model(CMRoot CMRoot, QueryGenerator QueryGenerator);

    public Exception ExceptionOnModelBuilding => exceptionOnModelBuilding;

    public CMRoot CircularModel
    {
        get
        {
            if (currentModel == null)
            {
                UpdateModel();
            }

            return currentModel.CMRoot;
        }
    }

    public QueryGenerator QueryGenerator => currentModel.QueryGenerator;

    public LiveSource(String connectionString)
    {
        this.connectionString = connectionString;
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
        if (currentModel == null)
        {
            UpdateModel();
        }

        Entity entity;

        try
        {
            entity = await QueryGenerator.QueryAsync(connection, extent);
        }
        catch (SqlException ex)
        {
            if (CheckAndUpdateIfApplicable())
            {
                throw new SchemaChangedException(ex);
            }
            else
            {
                throw;
            }
        }

        if (entity.SchemaDate > CircularModel.TimeStamp)
        {
            UpdateModel();

            return await QueryGenerator.QueryAsync(connection, extent);
        }
        else
        {
            return entity;
        }
    }

    void UpdateModel()
    {
        try
        {
            using var connection = GetConnection();

            var cmRoot = connection.GetCircularModel();

            var qg = new QueryGenerator(cmRoot);

            currentModel = new Model(cmRoot, qg);
        }
        catch (Exception ex)
        {
            exceptionOnModelBuilding = ex;

            throw;
        }

        exceptionOnModelBuilding = null;
    }

    Boolean CheckAndUpdateIfApplicable()
    {
        var connection = GetConnection();

        var modifiedAt = connection.GetSchemaModifiedAt();

        if (modifiedAt > currentModel.CMRoot.TimeStamp)
        {
            UpdateModel();

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
            log.Info($"{logIds} Requesting cancellation of running query");

            Cancel();
        }

        log.Info($"{logIds} Acquiring lock while semaphore at {semaphore.CurrentCount}");

        tcs = new CancellationTokenSource();

        var ct = tcs.Token;

        await semaphore.WaitAsync();

        try
        {
            StallDetective = null;

            if (ct.IsCancellationRequested)
            {
                log.Info($"{logIds} Already canceled, won't start");

                throw new OperationCanceledException("Operation canceled before it began");
            }

            NotifyChange();

            if (connection.State == System.Data.ConnectionState.Closed)
            {
                log.Info($"{logIds} Opening connection");

                await connection.OpenAsync();

                NotifyChange();
            }

            using var _ = StaticServiceStack.Install(ct);

            log.Info($"{logIds} Starting query");

            var task = action(connection);

            await Task.WhenAny(task, Task.Delay(1000));

            if (!task.IsCompleted)
            {
                log.Info($"{logIds} Query is delayed, spawning stall detective");

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

            log.Info($"{logIds} Query terminated, semaphore now at {semaphore.CurrentCount}");
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
