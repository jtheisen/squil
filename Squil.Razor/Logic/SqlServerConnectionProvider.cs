using Microsoft.Data.SqlClient;

namespace Squil;

public class SqlServerConnectionProvider
{
    public const String CompatibilityPrefix = "Trust Server Certificate=true;";
    public const String PersistSecurityInfoSuffix = ";Persist Security Info=true";

    public String GetConnectionString(SqlServerHostConfiguration config, String catalogOverride = null, Boolean ignoreCatalog = false)
    {
        var builder = new SqlConnectionStringBuilder();

        builder.TrustServerCertificate = true;
        builder.PoolBlockingPeriod = PoolBlockingPeriod.NeverBlock;
        builder.DataSource = config.Host;
        builder.IntegratedSecurity = config.UseWindowsAuthentication;
        builder.PersistSecurityInfo = true;

        SetConnectionOverrides(builder);

        if (!config.UseWindowsAuthentication)
        {
            builder.UserID = config.User;
            builder.Password = config.Password;
        }

        if (catalogOverride != null)
        {
            builder.InitialCatalog = catalogOverride;
        }
        else if (!ignoreCatalog && !String.IsNullOrEmpty(config.Catalog))
        {
            builder.InitialCatalog = config.Catalog;
        }

        builder.Pooling = false;

        return builder.ConnectionString;
    }

    void SetConnectionOverrides(SqlConnectionStringBuilder builder)
    {
        builder.ApplicationName = "SQuiL database browser";
        builder.CommandTimeout = 0;
        //builder.ConnectRetryCount = 0; // default is 1
        builder.ConnectRetryInterval = 4; // default is 10
        builder.ConnectTimeout = 1;
    }

    public SqlConnection GetConnection(SqlServerHostConfiguration config, Boolean ignoreCatalog = false)
    {
        return new SqlConnection(GetConnectionString(config, ignoreCatalog: ignoreCatalog));
    }

    public LiveSource GetLiveSource(String connectionString, Boolean debugFailOnModelCreation = false)
    {
        var builder = new SqlConnectionStringBuilder(CompatibilityPrefix + connectionString + PersistSecurityInfoSuffix);

        SetConnectionOverrides(builder);

        return new LiveSource(builder.ConnectionString, debugFailOnModelCreation);
    }

    public async Task<SqlConnection> GetOpenedConnection(SqlServerHostConfiguration config, Boolean ignoreCatalog = false)
    {
        var connection = GetConnection(config, ignoreCatalog);

        await connection.OpenAsync();

        return connection;
    }

    public record GreetResult(SqlConnectionExtensions.SqlCatalog[] Catalogs);

    public async Task<GreetResult> GreetConnection(SqlServerHostConfiguration config, Boolean ignoreCatalog = false)
    {
        var connection = await GetOpenedConnection(config, ignoreCatalog);

        var catalogs = connection.QueryCatalogs();

        return new GreetResult(catalogs);
    }
}
