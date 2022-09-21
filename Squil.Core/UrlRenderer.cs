using static System.Web.HttpUtility;

namespace Squil;

public class UrlRenderer
{
    private readonly String baseUrl;

    public UrlRenderer(String baseUrl)
    {
        this.baseUrl = baseUrl;
    }

    public String RenderUrl(IEnumerable<String> segments, IEnumerable<(String prefix, String key, String value)> queryParams)
    {
        var path = String.Join('/', from s in segments where s != null select UrlEncode(s));

        var query = String.Join('&', from p in queryParams where p.key != null select $"{p.prefix}{UrlEncode(p.key)}={p.value}");

        var lastSlashI = path.LastIndexOf('/');

        var haveDot = path.IndexOf('.', lastSlashI) >= 0;

        if (haveDot)
        {
            path = path + "/blazor-defeating-dummy-segment";
        }

        if (String.IsNullOrEmpty(query))
        {
            return $"{baseUrl}/{path}";
        }
        else
        {
            return $"{baseUrl}/{path}?{query}";
        }
    }
}
