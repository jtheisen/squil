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
        private readonly Dictionary<String, Lazy<SquilContext>> contextsByName;

        public ConnectionManager(IOptions<List<ConnectionConfiguration>> configurationsOption)
        {
            this.configurations = configurationsOption.Value;

            contextsByName = configurations.ToDictionary(c => c.Name, c => new Lazy<SquilContext>(() => new SquilContext(c.ConnectionString)));
        }

        public IEnumerable<(ConnectionConfiguration config, String error)> GetConnnections()
        {
            return configurations.Select(c => (c, (String)default));
        }

        public SquilContext GetContext(String name)
        {
            if (contextsByName.TryGetValue(name, out var context))
            {
                return context.Value;
            }
            else
            {
                throw new Exception("No such connection");
            }
        }

    }
}
