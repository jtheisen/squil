using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Acidui
{
    public class ConnectionManager
    {
        private readonly List<ConnectionConfiguration> configurations;
        private readonly Dictionary<String, Lazy<AciduiContext>> contextsByName;

        public ConnectionManager(IOptions<List<ConnectionConfiguration>> configurationsOption)
        {
            this.configurations = configurationsOption.Value;

            contextsByName = configurations.ToDictionary(c => c.Name, c => new Lazy<AciduiContext>(() => new AciduiContext(c.ConnectionString)));
        }

        public IEnumerable<(ConnectionConfiguration config, String error)> GetConnnections()
        {
            return configurations.Select(c => (c, (String)default));
        }

        public AciduiContext GetContext(String name)
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
