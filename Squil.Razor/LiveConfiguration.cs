using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Squil;

public class ProminentSourceConfiguration
{
    public String Name { get; set; }
    public String LongName { get; set; }
    public String ConnectionString { get; set; }
    public String Description { get; set; }
    public String DescriptionSnippetType { get; set; }
}

public class LiveSqlServerHost : ObservableObject
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

            var connectionString = provider.GetConnectionString(configuration);

            liveSources[catalog] = liveSource = new LiveSource(connectionString);
        }

        return liveSource;
    }
}

public class SquilConfiguration
{
    public ProminentSourceConfiguration[] ProminentSources { get; set; } = Empties<ProminentSourceConfiguration>.Array;

    public SqlServerHostConfiguration[] SqlServerHosts { get; set; } = Empties<SqlServerHostConfiguration>.Array;
}

public interface ISquilConfigStore
{
    Boolean CanSave { get; }

    SquilConfiguration Load();

    void Save(SquilConfiguration config);
}

public class AppSettingsSquilConfigStore : ISquilConfigStore
{
    private readonly IOptions<List<ProminentSourceConfiguration>> configurationsOption;

    public AppSettingsSquilConfigStore(IOptions<List<ProminentSourceConfiguration>> configurationsOption)
    {
        this.configurationsOption = configurationsOption;
    }

    public Boolean CanSave => false;

    public SquilConfiguration Load() => new SquilConfiguration { ProminentSources = configurationsOption.Value.ToArray() };

    public void Save(SquilConfiguration config) => throw new NotImplementedException();
}

public class LocalFileSquilConfigStore : ISquilConfigStore
{
    String squilFolder;
    String configFilePath;

    public LocalFileSquilConfigStore()
    {
        squilFolder = GetSquilFolder();
        configFilePath = System.IO.Path.Combine(squilFolder, "config.json");
    }

    public Boolean CanSave => true;

    public SquilConfiguration Load()
    {
        if (File.Exists(configFilePath))
        {
            var text = File.ReadAllText(configFilePath);

            return JsonConvert.DeserializeObject<SquilConfiguration>(text);
        }
        else
        {
            return new SquilConfiguration();
        }
    }

    public void Save(SquilConfiguration config)
    {
        Directory.CreateDirectory(squilFolder);

        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config));
    }

    String GetSquilFolder()
        => Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "squil");
}



public class LiveConfiguration
{
    ISquilConfigStore configStore;

    AssocList<String, (ProminentSourceConfiguration config, LiveSource context)> prominentSources;

    Dictionary<Guid, LiveSqlServerHost> liveHosts;
    Dictionary<String, LiveSqlServerHost> liveHostsByName;

    IOptions<AppSettings> options;
    IDbFactory dbf;
    SqlServerConnectionProvider sqlServerConnectionProvider;
    SquilConfiguration lastLoadedConfiguration;

    Exception lastLoadException;

    Boolean inBatch;

    public SqlServerConnectionProvider SqlServerConnectionProvider => sqlServerConnectionProvider;

    public AppSettings AppSettings => options.Value;

    public LiveConfiguration(ISquilConfigStore configStore, IOptions<AppSettings> options, IDbFactory dbf, SqlServerConnectionProvider sqlServerConnectionProvider)
    {
        this.configStore = configStore;
        this.options = options;
        this.dbf = dbf;
        this.sqlServerConnectionProvider = sqlServerConnectionProvider;

        prominentSources = new AssocList<String, (ProminentSourceConfiguration, LiveSource)>("prominent source");

        Load();
    }

    public ProminentSourceConfiguration[] GetProminentSourceConfigurations()
        => Get(() => prominentSources.Select(c => c.Value.config).ToArray());

    public void AddProminentSource(ProminentSourceConfiguration connection)
        => Modify(() => prominentSources.Append(connection.Name, (connection, new LiveSource(connection.ConnectionString))));

    public void RemoveProminentSource(String name)
        => Modify(() => prominentSources.Remove(name));

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
            select (h, l: l ?? new LiveSqlServerHost(h, sqlServerConnectionProvider))
        ).ToDictionary(p => p.h.Id, p => p.l);

        var newLiveHostsByName = newLiveHosts.Values.ToDictionary(h => h.Name, h => h);

        lock (this)
        {
            liveHosts = newLiveHosts;
            liveHostsByName = newLiveHostsByName;
        }
    }

    T Get<T>(Func<T> select)
    {
        lock (this)
        {
            return select();
        }
    }

    void Modify(Action action)
    {
        if (inBatch)
        {
            action();
        }
        else
        {
            lock (this)
            {
                EnsureCanSave();

                action();

                Save();
            }
        }
    }

    void Load()
    {
        lock (this)
        {
            inBatch = true;

            try
            {
                lastLoadedConfiguration = configStore.Load();

                foreach (var connection in lastLoadedConfiguration.ProminentSources)
                {
                    AddProminentSource(connection);
                }
            }
            catch (Exception ex)
            {
                lastLoadException = ex;
            }
            finally
            {
                inBatch = false;
            }
        }
    }

    void EnsureCanSave()
    {
        if (!configStore.CanSave) throw new Exception($"Trying to modify configuration without writable store");
    }

    void Save()
    {
        configStore.Save(new SquilConfiguration
        {
            ProminentSources = GetProminentSourceConfigurations()
        });
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

    public LiveSource GetLiveSource(String name)
    {
        if (TryParseHostSourceName(name, out var host, out var catalog))
        {
            return Get(() => liveHostsByName[host]).GetSourceContext(catalog);
        }
        else
        {
            return Get(() => prominentSources[name].context);
        }
    }
}
