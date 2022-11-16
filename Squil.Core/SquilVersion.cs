using NLog;
using System.IO;
using System.Reflection;
using System.Text;

namespace Squil;

public static class SquilVersion
{
    static Logger log = LogManager.GetCurrentClassLogger();

    public static String ReadSquilVersion()
    {
        try
        {
            var assembly = typeof(SquilVersion).Assembly;

            using var stream = assembly.GetManifestResourceStream("Squil.version.txt");

            if (stream == null)
            {
                ListResources(assembly);

                return "unknown";
            }

            var ms = new MemoryStream();

            stream.CopyTo(ms);

            return Encoding.UTF8.GetString(ms.ToArray());
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
