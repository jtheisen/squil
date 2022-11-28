using System.Data;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using System.Xml.Serialization;
using Microsoft.Data.SqlClient;

namespace Squil;

public static class SqlConnectionExtensions
{
    public static SqlCommand CreateSqlCommandFromSql(this SqlConnection connection, String sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        return command;
    }

    public static String QueryXmlString(this SqlConnection connection, String sql, Boolean dontWrap = false)
    {
        using var scope = GetCurrentLedger().TimedScope("query");

        if (!dontWrap)
        {
            sql = $"select ({sql})";
        }

        var command = connection.CreateSqlCommandFromSql(sql);

        using var reader = command.ExecuteReader();

        reader.Read();

        return scope.SetResult(reader.GetString(0));
    }

    public static async Task<String> QueryXmlStringAsync(this SqlConnection connection, String sql, Boolean dontWrap = false)
    {
        using var scope = GetCurrentLedger().TimedScope("query");

        if (!dontWrap)
        {
            sql = $"select ({sql})";
        }

        var command = connection.CreateSqlCommandFromSql(sql);

        using var reader = await command.ExecuteReaderAsync();

        await reader.ReadAsync();

        return scope.SetResult(reader.GetString(0));
    }

    public static X Parse<X>(String xml)
        where X : class
    {
        using var scope = GetCurrentLedger().TimedScope("parsing-and-binding");

        var serializer = new XmlSerializer(typeof(X));

        var reader = new StringReader(xml);

        var result = serializer.Deserialize(reader);

        return scope.SetResult(result as X);
    }

    public static X QueryAndParseXml<X>(this SqlConnection connection, String sql, out String xml)
        where X : class
    {
        using var scope = GetCurrentLedger().TimedScope("querying-parsing-and-binding");

        return scope.SetResult(connection.QueryAndParseXmlSeparately<X>(sql, out xml));
    }

    public static X QueryAndParseXml<X>(this SqlConnection connection, String sql, Boolean dontWrap = false)
        where X : class
    {
        using var scope = GetCurrentLedger().TimedScope("querying-parsing-and-binding");

        return scope.SetResult(connection.QueryAndParseXmlSeparately<X>(sql, out _, dontWrap));
    }

    public static X QueryAndParseXmlSeparately<X>(this SqlConnection connection, String sql, out String xml, Boolean dontWrap = false)
        where X : class
    {
        xml = connection.QueryXmlString(sql, dontWrap);

        return Parse<X>(xml);
    }

    public static X QueryAndParseXmlCombined<X>(this SqlConnection connection, String sql)
        where X : class
    {
        using var _ = GetCurrentLedger().TimedScope("query-parsing-and-binding");

        var command = connection.CreateSqlCommandFromSql(sql);

        using var reader = command.ExecuteXmlReader();

        var serializer = new XmlSerializer(typeof(X));

        var result = serializer.Deserialize(reader);

        return result as X;
    }

    public static async Task<X> QueryAndParseXmlAsync<X>(this SqlConnection connection, String sql)
        where X : class
    {
        var xml = await connection.QueryXmlStringAsync(sql);

        return Parse<X>(xml);
    }

    public static XElement QueryAndParseXml(this SqlConnection connection, String sql)
    {
        using var scope = GetCurrentLedger().TimedScope("query-and-parsing");

        var command = connection.CreateSqlCommandFromSql(sql);

        using var reader = command.ExecuteXmlReader();

        var rootRow = XElement.Load(reader);

        return scope.SetResult(rootRow);
    }

    public static async Task<XElement> QueryAndParseXmlAsync(this SqlConnection connection, String sql)
    {
        using var scope = GetCurrentLedger().TimedScope("query-and-parsing");

        var command = connection.CreateSqlCommandFromSql(sql);

        var ct = StaticServiceStack.Get<CancellationToken>();

        using var reader = await command.ExecuteXmlReaderAsync(StaticServiceStack.Get<CancellationToken>());

        // FIXME: This isn't really async as the reader doesn't support that.
        var rootRow = XElement.Load(reader);

        return scope.SetResult(rootRow);
    }

    [XmlRoot("results")]
    public class ResultRoot<T>
    {
        [XmlArray("items")]
        public T[] Results { get; set; }
    }

    [XmlType("i")]
    public class SqlCatalog
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("has_dbaccess")]
        public Boolean HasAccess { get; set; }

        [XmlAttribute("is_system_object")]
        public Boolean IsSystemObject { get; set; }
    }

    public static String WrapSimpleQuery(String sql)
    {
        return @$"
select (
{sql}
for xml auto, type
) items for xml path ('results')
";
    }

    public static SqlCatalog[] QueryCatalogs(this SqlConnection connection)
    {
        var sql = @"
select
	name,
    has_dbaccess(name) has_dbaccess,
	cast(case when i.name in ('master','model','msdb','tempdb') then 1 else i.is_distributor end as bit) is_system_object
from
	sys.databases i
";

        var result = connection.QueryAndParseXml<ResultRoot<SqlCatalog>>(WrapSimpleQuery(sql));

        return result.Results;
    }

    public static CMIndexlike ChooseIndex(this RelatedEntities relatedEntities)
    {
        var end = relatedEntities.RelationEnd;
        var table = end.Table;
        var key = end.Key;

        var extentIndexName = relatedEntities.Extent.IndexName;

        if (extentIndexName != null)
        {
            return table.Indexes[extentIndexName];
        }

        if (key is CMForeignKey fk)
        {
            return fk.BackingIndexes.FirstOrDefault();
        }

        if (key is CMIndexlike ix)
        {
            return ix;
        }

        return null;
    }

    public static RelatedEntitiesListItemAnnotationInfo GetListAnnotationInfo(this RelatedEntities entities)
    {
        var values = entities.Extent.Values;

        if (values == null) return default;

        var valueCount = entities.Extent.Values!.Length;

        if (valueCount == 0) return default;

        var lastValueI = valueCount - 1;

        var matchCount = entities.List.Count(e => e.IsMatching == true);
        var afterCount = entities.List.Count(e => e.IsMatching == false);

        var column = entities.Extent.Order[lastValueI];

        return new RelatedEntitiesListItemAnnotationInfo
        {
            wasSearch = entities.Extent.KeyValueCount < valueCount,

            matchCount = entities.List.Count(e => e.IsMatching == true),
            afterCount = entities.List.Count(e => e.IsMatching == false),

            direction = column.d,

            column = column,
            value = values[lastValueI],
        };
    }
}
