using Microsoft.Extensions.Options;

namespace Squil;

public class ProminentSourceConfiguration : LiveSourceDebugOptions
{
    public String Name { get; set; }
    public String LongName { get; set; }
    public String ConnectionString { get; set; }
    public String Description { get; set; }
    public String DescriptionSnippetType { get; set; }
}

public class LiveSqlServerHost : ObservableObject<LiveSqlServerHost>
{
    private readonly SqlServerHostConfiguration configuration;
    private readonly SqlServerConnectionProvider provider;

    Task<SqlServerConnectionProvider.GreetResult> currentGreet;
    SqlServerConnectionProvider.GreetResult lastGreetResult;
    Exception lastError;

    Dictionary<String, LiveSource> liveSources = new Dictionary<String, LiveSource>();

    public LiveSqlServerHost(SqlServerHostConfiguration configuration, SqlServerConnectionProvider provider)
    {
        this.configuration = configuration;
        this.provider = provider;
    }

    public String Name => configuration.Name;

    public Guid Id => configuration.Id;

    public SqlServerHostConfiguration Configuration => configuration;

    public Boolean IsRefreshing => currentGreet != null;

    public async Task Refresh()
    {
        lock (this)
        {
            if (currentGreet != null) return;

            currentGreet = provider.GreetConnection(configuration);
        }

        NotifyChange();

        try
        {
            lastGreetResult = await currentGreet;
            lastError = null;
        }
        catch (Exception ex)
        {
            lastGreetResult = null;
            lastError = ex;
        }

        currentGreet = null;

        NotifyChange();
    }

    public Boolean IsLoading => currentGreet?.Status == TaskStatus.Running;

    public Exception Error => lastError;

    public SqlConnectionExtensions.SqlCatalog[] AllCatalogs => lastGreetResult?.Catalogs;

    public SqlConnectionExtensions.SqlCatalog[] FilteredCatalogs
        => lastGreetResult?.Catalogs.Where(IsCatalogEligible).ToArray();

    public SqlConnectionExtensions.SqlCatalog[] SelectedCatalogs
        => configuration.Catalog is String catalog ? FilteredCatalogs?.Where(c => c.Name == catalog).ToArray() : FilteredCatalogs;

    Boolean IsCatalogEligible(SqlConnectionExtensions.SqlCatalog catalog)
        => !catalog.IsSystemObject && catalog.HasAccess;

    public LiveSource GetSourceContext(String catalog)
    {
        if (!liveSources.TryGetValue(catalog, out var liveSource))
        {
            var config = configuration.Clone();

            config.Catalog = catalog;

            var connectionString = provider.GetConnectionString(configuration, catalogOverride: catalog);

            liveSources[catalog] = liveSource = new LiveSource(connectionString, config.DebugOptions);
        }

        return liveSource;
    }
}

public interface ILiveSourceProvider
{
    AppSettings AppSettings { get; }

    LiveSource GetLiveSource(String name);
}

public class LightLiveConfiguration : ILiveSourceProvider
{
    AssocList<String, (ProminentSourceConfiguration config, LiveSource context)> prominentSources;

    IOptions<AppSettings> options;
    SqlServerConnectionProvider sqlServerConnectionProvider;

    public AppSettings AppSettings => options.Value;

    public SqlServerConnectionProvider SqlServerConnectionProvider => sqlServerConnectionProvider;

    public ProminentSourceConfiguration[] GetProminentSourceConfigurations()
        => Get(() => prominentSources.Select(c => c.Value.config).ToArray());

    public LightLiveConfiguration(
        IOptions<AppSettings> options,
        IOptions<List<ProminentSourceConfiguration>> prominentSourceConfigurationsOptions,
        SqlServerConnectionProvider sqlServerConnectionProvider)
    {
        this.options = options;
        this.sqlServerConnectionProvider = sqlServerConnectionProvider;

        prominentSources = new AssocList<String, (ProminentSourceConfiguration, LiveSource)>("prominent source");

        if (prominentSourceConfigurationsOptions.Value is List<ProminentSourceConfiguration> prominentSourceConfigurations)
        {
            foreach (var configuration in prominentSourceConfigurations)
            {
                var source = sqlServerConnectionProvider.GetLiveSource(configuration.ConnectionString, configuration);

                prominentSources.Append(configuration.Name, (configuration, source));
            }
        }
    }

    protected T Get<T>(Func<T> select)
    {
        lock (this)
        {
            return select();
        }
    }

    public virtual LiveSource GetLiveSource(String name)
    {
        return Get(() => prominentSources[name].context);
    }
}

public class LiveConfiguration : LightLiveConfiguration
{
    Dictionary<Guid, LiveSqlServerHost> liveHosts;
    Dictionary<String, LiveSqlServerHost> liveHostsByName;

    IDbFactory dbf;

    Exception lastLoadException;

    Boolean inBatch;

    public LiveConfiguration(IOptions<AppSettings> options, IOptions<List<ProminentSourceConfiguration>> prominentSourceConfigurationsOptions, IDbFactory dbf, SqlServerConnectionProvider sqlServerConnectionProvider)
        : base(options, prominentSourceConfigurationsOptions, sqlServerConnectionProvider)
    {
        this.dbf = dbf;
    }

    public LiveSqlServerHost[] LiveSqlServerHosts
        => Get(() => liveHosts?.Values.ToArray());

    public async Task UpdateLiveHosts(Func<IDbFactory, Task> action)
    {
        await action(dbf);

        await RefreshHosts();
    }

    public async Task RefreshHosts(Boolean onlyIfUnloaded = false)
    {
        if (onlyIfUnloaded && liveHosts != null) return;

        var hosts = await dbf.DoAsync(db => db.SqlServerHostConfigurations.ToArrayAsync());

        var newLiveHosts = (
            from h in hosts
            join l in liveHosts?.Values ?? Empties<LiveSqlServerHost>.Enumerable on h.Id equals l.Id into existing
            from l in existing.DefaultIfEmpty()
            select (h, l: l?.Configuration.ModifiedAt == h.ModifiedAt ? l : new LiveSqlServerHost(h, SqlServerConnectionProvider))
        ).ToDictionary(p => p.h.Id, p => p.l);

        var newLiveHostsByName = newLiveHosts.Values.ToDictionary(h => h.Name, h => h);

        lock (this)
        {
            liveHosts = newLiveHosts;
            liveHostsByName = newLiveHostsByName;
        }
    }

    Boolean TryParseHostSourceName(String name, out String host, out String catalog)
    {
        if (name.Contains('@'))
        {
            var split = name.Split('@');

            catalog = split[0];
            host = split[1];

            if (split.Length != 2 || String.IsNullOrWhiteSpace(catalog) || String.IsNullOrWhiteSpace(host))
            {
                throw new Exception($"Potential host source name '{name}' is malformed");
            }

            return true;
        }
        else
        {
            host = catalog = null;

            return false;
        }
    }

    public override LiveSource GetLiveSource(String name)
    {
        if (TryParseHostSourceName(name, out var host, out var catalog))
        {
            return Get(() => liveHostsByName[host]).GetSourceContext(catalog);
        }
        else
        {
            return base.GetLiveSource(name);
        }
    }
}
