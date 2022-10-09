using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Squil;

public class ConnectionConfiguration
{
    public String Name { get; set; }
    public String LongName { get; set; }
    public String ConnectionString { get; set; }
    public String Description { get; set; }
    public String DescriptionSnippetType { get; set; }
}

public class SqlServerConnectionConfiguration
{
    [Required]
    public String Name { get; set; } = "new";

    [Required]
    public String Host { get; set; } = ".\\";

    public Boolean UseWindowsAuthentication { get; set; } = true;

    public String User { get; set; }
    public String Password { get; set; }

    public String Catalog { get; set; }
}

public class SquilConfiguration
{
    public ConnectionConfiguration[] Connections { get; set; } = Empties<ConnectionConfiguration>.Array;
}

public interface ISquilConfigStore
{
    Boolean CanSave { get; }

    SquilConfiguration Load();

    void Save(SquilConfiguration config);
}

public class AppSettingsSquilConfigStore : ISquilConfigStore
{
    private readonly IOptions<List<ConnectionConfiguration>> configurationsOption;

    public AppSettingsSquilConfigStore(IOptions<List<ConnectionConfiguration>> configurationsOption)
    {
        this.configurationsOption = configurationsOption;
    }

    public Boolean CanSave => false;

    public SquilConfiguration Load() => new SquilConfiguration { Connections = configurationsOption.Value.ToArray() };

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
        => System.IO.Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "squil");

    String GetHomePath() => Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
}


public class LiveConfiguration
{
    ISquilConfigStore configStore;

    AssocList<String, (ConnectionConfiguration config, ConnectionContext context)> connectionsByName;

    IOptions<AppSettings> options;

    SquilConfiguration lastLoadedConfiguration;

    Exception lastLoadException;

    public AppSettings AppSettings => options.Value;

    public LiveConfiguration(ISquilConfigStore configStore, IOptions<AppSettings> options)
    {
        this.configStore = configStore;
        this.options = options;

        connectionsByName = new AssocList<String, (ConnectionConfiguration, ConnectionContext)>();

        Load();
    }

    public IEnumerable<ConnectionConfiguration> GetConnnectionConfigurations()
        => connectionsByName.Select(c => c.Value.config);

    public void AddConnection(ConnectionConfiguration connection)
    {
        lock (this)
        {
            EnsureCanSave();

            AddConnectionInternal(connection);

            Save();
        }
    }

    void AddConnectionInternal(ConnectionConfiguration connection)
    {
        connectionsByName.Append(connection.Name, (connection, new ConnectionContext(connection.ConnectionString)));
    }

    public void RemoveConnection(String name)
    {
        lock (this)
        {
            EnsureCanSave();

            connectionsByName.Remove(name);

            Save();
        }
    }

    void Load()
    {
        lock (this)
        {
            try
            {
                lastLoadedConfiguration = configStore.Load();

                foreach (var connection in lastLoadedConfiguration.Connections)
                {
                    AddConnectionInternal(connection);
                }
            }
            catch (Exception ex)
            {
                lastLoadException = ex;
            }
        }
    }

    void EnsureCanSave()
    {
        if (!configStore.CanSave) throw new Exception($"Trying to modify configuration without writable store");
    }

    void Save()
    {
        configStore.Save(new SquilConfiguration { Connections = GetConnnectionConfigurations().ToArray() });
    }

    public ConnectionContext GetContext(String name)
    {
        lock (this)
        {
            if (connectionsByName.TryGetValue(name, out var connection))
            {
                return connection.context;
            }
            else
            {
                throw new Exception($"No connection found with name '{name}'");
            }
        }
    }
}
