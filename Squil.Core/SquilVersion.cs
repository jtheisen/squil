using NLog;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace Squil;

[XmlType("Version")]
public class SquilVersion
{
    [XmlElement("Display")]
    public String Display { get; set; }

    static Logger log = LogManager.GetCurrentClassLogger();

    public static String ReadSquilVersion()
    {
        try
        {
            var assembly = typeof(SquilVersion).Assembly;

            using var stream = assembly.GetManifestResourceStream("Squil.version.xml");

            if (stream == null)
            {
                ListResources(assembly);

                return "unknown";
            }

            var serializer = new XmlSerializer(typeof(SquilVersion));

            var version = (SquilVersion)serializer.Deserialize(stream);

            return version.Display;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Can't read squil version");

            return "unknown";
        }
    }

    static void ListResources(Assembly assembly)
    {
        var resourceNames = assembly.GetManifestResourceNames();

        log.Error($"Can't find version resource, all {resourceNames.Length} resources:");

        foreach (string resourceName in resourceNames)
        {
            log.Info("- " + resourceName);
        }
    }
}
