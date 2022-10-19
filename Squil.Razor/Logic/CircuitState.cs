using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Squil;

public class CircuitState
{
    IMap<String, IMap<String, String>> searchValuesByLocation;

    public IMap<String, IMap<String, String>> SearchValuesByLocation => searchValuesByLocation;

    public CircuitState()
    {
        searchValuesByLocation = new Dictionary<String, IMap<String, String>>()
            .AsMap(createDefault: () => new ExpandoObject().AsMap(withDefaults: true).Convert(o => o as String, o => o));
    }
}
