using System.Data.SqlClient;

namespace Squil;

public class SchemaChangedException : Exception
{
    public SchemaChangedException() { }

    public SchemaChangedException(Exception nested)
        : base("The schema has changed", nested)
    { }
}

public class ConnectionContext
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

    public ConnectionContext(String connectionString)
    {
        this.connectionString = connectionString;
    }

    public SqlConnection GetConnection()
    {
        var connection = new SqlConnection(connectionString);

        connection.Open();

        return connection;
    }

    public async Task<SqlConnection> GetConnectionAsync()
    {
        var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        return connection;
    }

    public Entity Query(SqlConnection connection, Extent extent)
    {
        if (currentModel == null)
        {
            UpdateModel();
        }

        Entity entity;

        try
        {
            entity = QueryGenerator.Query(connection, extent);
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

            return QueryGenerator.Query(connection, extent);
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
