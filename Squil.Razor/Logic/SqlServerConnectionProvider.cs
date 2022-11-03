﻿using Microsoft.Data.SqlClient;

namespace Squil;

public class SqlServerConnectionProvider
{
    public const String CompatibilityPrefix = "Trust Server Certificate=true;";

    public String GetConnectionString(SqlServerHostConfiguration config, String catalogOverride = null, Boolean ignoreCatalog = false)
    {
        var builder = new SqlConnectionStringBuilder();

        builder.TrustServerCertificate = true;
        builder.DataSource = config.Host;
        builder.IntegratedSecurity = config.UseWindowsAuthentication;
        builder.ApplicationName = "SQuiL database browser";
        builder.ConnectTimeout = 4;

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

    public SqlConnection GetConnection(SqlServerHostConfiguration config, Boolean ignoreCatalog = false)
    {
        return new SqlConnection(GetConnectionString(config, ignoreCatalog: ignoreCatalog));
    }

    public LiveSource GetLiveSource(String connectionString)
    {
        return new LiveSource(CompatibilityPrefix + connectionString);
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
