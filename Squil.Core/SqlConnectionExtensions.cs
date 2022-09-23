using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Xml.Linq;
using System.Xml.Serialization;

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

    public static String QueryXmlString(this SqlConnection connection, String sql)
    {
        using var _ = GetCurrentLedger().TimedScope("query");

        var command = connection.CreateSqlCommandFromSql($"select ({sql})");

        using var reader = command.ExecuteReader();

        reader.Read();

        return reader.GetString(0);
    }

    public static X Parse<X>(String xml)
        where X : class
    {
        using var _ = GetCurrentLedger().TimedScope("parsing-and-binding");

        var serializer = new XmlSerializer(typeof(X));

        var reader = new StringReader(xml);

        var result = serializer.Deserialize(reader);

        return result as X;
    }

    public static X QueryAndParseXml<X>(this SqlConnection connection, String sql)
        where X : class
    {
        return connection.QueryAndParseXmlSeparately<X>(sql);
    }

    public static X QueryAndParseXmlSeparately<X>(this SqlConnection connection, String sql)
        where X : class
    {
        var xml = connection.QueryXmlString(sql);

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

    public static XElement QueryAndParseXml(this SqlConnection connection, String sql)
    {
        using var scope = GetCurrentLedger().TimedScope("query-and-parsing");

        var command = connection.CreateSqlCommandFromSql(sql);

        using var reader = command.ExecuteXmlReader();

        var rootRow = XElement.Load(reader);

        return scope.SetResult(rootRow);
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
