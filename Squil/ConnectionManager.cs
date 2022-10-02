using Microsoft.Extensions.Options;

namespace Squil
{
    public class ConnectionConfiguration
    {
        public String Name { get; set; }
        public String LongName { get; set; }
        public String ConnectionString { get; set; }
        public String Description { get; set; }
    }

    public class ConnectionManager
    {
        private readonly List<ConnectionConfiguration> configurations;
        private readonly Dictionary<String, SquilContext> contextsByName;
        private readonly Dictionary<String, ConnectionConfiguration> configurationsByName;
        private readonly IOptions<AppSettings> options;

        public AppSettings AppSettings => options.Value;

        public ConnectionManager(IOptions<List<ConnectionConfiguration>> configurationsOption, IOptions<AppSettings> options)
        {
            this.configurations = configurationsOption.Value;

            contextsByName = new Dictionary<String, SquilContext>();
            configurationsByName = configurations.ToDictionary(c => c.Name, c => c);

            this.options = options;
        }

        public IEnumerable<(ConnectionConfiguration config, String error)> GetConnnections()
        {
            return configurations.Select(c => (c, (String)default)).ToArray();
        }

        public SquilContext GetContext(String name)
        {
            lock (this)
            {
                if (contextsByName.TryGetValue(name, out var context))
                {
                    return context;
                }
                else if (configurationsByName.TryGetValue(name, out var configuration))
                {
                    return contextsByName[name] = context = new SquilContext(configuration.ConnectionString);
                }
                else
                {
                    throw new Exception($"No connection found with name '{name}'");
                }
            }
        }

    }
}
