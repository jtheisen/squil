using System.Data.SqlClient;

namespace Squil;

public class SqlServerConnectionProvider
{
    public String GetConnectionString(SqlServerConnectionConfiguration config, Boolean ignoreCatalog = false)
    {
        var builder = new SqlConnectionStringBuilder();

        builder.DataSource = config.Host;
        builder.IntegratedSecurity = config.UseWindowsAuthentication;
        if (!config.UseWindowsAuthentication)
        {
            builder.UserID = config.User;
            builder.Password = config.Password;
        }

        if (!ignoreCatalog && !String.IsNullOrEmpty(config.Catalog))
        {
            builder.InitialCatalog = config.Catalog;
        }

        builder.Pooling = false;

        return builder.ConnectionString;
    }

    public SqlConnection GetConnection(SqlServerConnectionConfiguration config, Boolean ignoreCatalog = false)
    {
        return new SqlConnection(GetConnectionString(config, ignoreCatalog));
    }

    public async Task<SqlConnection> GetOpenedConnection(SqlServerConnectionConfiguration config, Boolean ignoreCatalog = false)
    {
        var connection = GetConnection(config, ignoreCatalog);

        await connection.OpenAsync();

        return connection;
    }

    public record GreetResult(String Error, SqlConnectionExtensions.SqlCatalog[] Catalogs);

    public async Task<GreetResult> GreetConnection(SqlServerConnectionConfiguration config, Boolean ignoreCatalog = false)
    {
        try
        {
            var connection = await GetOpenedConnection(config, ignoreCatalog);

            var catalogs = connection.QueryCatalogs();

            return new GreetResult(null, catalogs);
        }
        catch (Exception ex)
        {
            return new GreetResult(ex.Message, null);
        }
    }
}
