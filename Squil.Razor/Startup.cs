using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Squil;

public static class Startup
{
    public static void AddSquilDb(this IServiceCollection services, AppSettings settings, IConfiguration configuration)
    {
        var provider = configuration?["provider"];
        var ignoreAssembly = configuration?["ignore-assembly"] != null;

        provider ??= settings.SquilDbProviderName;

        if (!String.IsNullOrEmpty(provider) && provider != "none")
        {
            services.AddDbContextFactory<Db>(
                options => _ = provider switch
                {
                    "Sqlite" => options.UseSqlite(
                        settings.SquilDbSqliteConnectionString,
                        x => x.Apply(!ignoreAssembly, x2 => x2.MigrationsAssembly("Squil.Storage.Migrations.Sqlite"))),
                    "SqlServer" => options.UseSqlServer(
                        settings.SquilDbSqlServerConnectionString,
                        x => x.Apply(!ignoreAssembly, x2 => x2.MigrationsAssembly("Squil.Storage.Migrations.SqlServer"))),

                    _ => throw new Exception($"Unsupported provider: {provider}")
                });
        }
    }

    public static void AddCommonSquilServices(this IServiceCollection services, AppSettings settings)
    {
        services.AddSingleton<SqlServerConnectionProvider>();

    }

    public static void InitializeDb(this IServiceProvider services)
    {
        var dbFactory = services.GetService<IDbContextFactory<Db>>();

        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();

            db.Database.Migrate();
        }
    }
}
