using System.Data.SqlClient;

namespace Squil;

public class SchemaChangedException : Exception
{
    public SchemaChangedException() { }

    public SchemaChangedException(Exception nested)
        : base("The schema has changed", nested)
    { }
}

public class SquilContext
{
    private readonly string connectionString;

    private volatile Model currentModel;

    record Model(CMRoot CMRoot, QueryGenerator QueryGenerator);

    public CMRoot CircularModel => currentModel.CMRoot;

    public QueryGenerator QueryGenerator => currentModel.QueryGenerator;

    public SquilContext(String connectionString)
    {
        this.connectionString = connectionString;

        UpdateModel();
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
        using var connection = GetConnection();

        var cmRoot = connection.GetCircularModel();

        var qg = new QueryGenerator(cmRoot);

        currentModel = new Model(cmRoot, qg);
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