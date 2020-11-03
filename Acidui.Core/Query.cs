using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Acidui
{
    public class QueryGenerator
    {
        private readonly CMRoot cmRoot;
        private readonly bool selectAllColumns;
        private readonly bool makePrettyQueries;

        public QueryGenerator(CMRoot cmRoot, Boolean selectAllColumns = false, Boolean makePrettyQueries = true)
        {
            this.cmRoot = cmRoot;
            this.selectAllColumns = selectAllColumns;
            this.makePrettyQueries = makePrettyQueries;
        }

        static String CreateSymbolForIdentifier(String id)
        {
            var c = id.ToString()[0];

            if (Char.IsLetter(c)) return Char.ToLowerInvariant(c).ToString();

            return "x";
        }

        public String GetCompleteSql(Extent rootExtent)
        {
            var rootTable = cmRoot.GetTable("");

            var sql = String.Join(",\n", rootExtent.Children.Select(e => GetSql(e, rootTable, CreateSymbolForIdentifier(e.RelationName), 1)));

            return $"select\n{sql}\n for xml path ('root')";
        }

        public String GetXmlRelationName(String name)
            => name.Replace(".", "_");

        String I(Int32 indent) => new String(' ', indent);

        String GetSql(Extent extent, CMTable parentTable, String aliasPrefix, Int32 indent)
        {
            var ispace = I(indent);
            var ipspace = ispace + "  ";

            var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
                $"Can't find relation for name {extent.RelationName} in table {parentTable.Name.ToString() ?? "<root>"}"
            );

            var alias = extent.Alias ?? (aliasPrefix + CreateSymbolForIdentifier(forwardEnd.Table.Name));

            var children = (
                    from e in (extent.Children ?? Enumerable.Empty<Extent>())
                    select $"{GetSql(e, forwardEnd.Table, alias, indent + 1)}"
                )
                .ToArray();

            var joinPredicates = forwardEnd.Columns.Zip(forwardEnd.OtherEnd.Columns, (s, p) => $"{alias}.{s.Name} = {aliasPrefix}.{p.Name}");

            if ((extent.Values?.Length ?? 0) > (extent.Order?.Length ?? 0)) throw new Exception("More filter values than order columns");

            var filterPredicates = extent.Values?.Select((v, i) => $"{alias}.{extent.Order[i]} = '{v}'") ?? Enumerable.Empty<String>();

            var allPredicates = filterPredicates.Concat(joinPredicates).ToArray();

            var whereLine = allPredicates.Length > 0 ? $"{ispace}where {string.Join($"\n{ispace}  and ", allPredicates)}\n" : "";

            var selectables = new List<String>();

            if (selectAllColumns)
            {
                selectables.Add("*");
            }
            else
            {
                selectables.AddRange(forwardEnd.Table.ColumnsInOrder.Select(c => c.Name));
            }

            selectables.AddRange(children);

            if (selectables.Count == 0)
            {
                selectables.Add("42 dummy");
            }

            var selectsClause = String.Join(",\n", selectables.Select(s => $"{ipspace}{s}"));

            var orderLine = extent.Order?.Apply(o => $"{ipspace}order by {string.Join(", ", o)} {(extent.IsDescending ? "desc" : "asc")}\n") ?? "";

            var topClause = extent.Limit?.Apply(l => $" top {l}") ?? "";

            var comment = makePrettyQueries ? $" -- {extent.RelationName}" : "";

            var sql = @$"(select{topClause}{comment}
{selectsClause}
{ipspace}from {forwardEnd.Table.Name} {alias}
{whereLine}{orderLine}{ispace}for xml auto, type
{ipspace}) {GetXmlRelationName(extent.RelationName)}";

            return sql;
        }

        Entity MakeEntity(Extent extent, CMTable table, XElement element)
        {
            return new Entity
            {
                ColumnValues = extent.Columns?.ToDictionary(c => c, c => element.Attribute(c)?.Value),
                Related = extent.Children?.Select(c => MakeEntities(c, table, element.Element(XName.Get(GetXmlRelationName(c.RelationName))))).ToArray()
            };
        }

        RelatedEntities MakeEntities(Extent extent, CMTable parentTable, XElement element)
        {
            var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
                $"Can't find relation for name {extent.RelationName} in table {parentTable.Name.ToString() ?? "<root>"}"
            );

            var table = forwardEnd.OtherEnd.Table;

            return new RelatedEntities
            {
                Extent = extent,
                RelationEnd = forwardEnd,
                RelationName = extent.RelationName,
                TableName = forwardEnd.Table.Name,
                List = element?.Elements().Select(e => MakeEntity(extent, forwardEnd.Table, e)).ToArray() ?? new Entity[0]
            };
        }

        public DebugEntity InterpretXml(XElement element)
        {
            return new DebugEntity
            {
                Columns = element.Attributes()
                    .ToDictionary(a => a.Name.LocalName, a => a.Value),
                Relations = element.Elements()
                    .ToDictionary(
                        e => new String(e.Name.LocalName),
                        e => e.Elements().Select(e2 => InterpretXml(e2)).ToArray()
                    )
            };
        }

        public Entity Query(SqlConnection connection, Extent extent)
        {
            var sql = GetCompleteSql(extent);

            var xml = connection.QueryXml(sql);

            var rootTable = cmRoot.GetTable("");

            var entity = MakeEntity(extent, rootTable, xml);

            return entity;
        }

        public X Query<X>(SqlConnection connection, Extent extent)
            where X : class
        {
            var sql = GetCompleteSql(extent);

            Console.WriteLine(connection.QueryXml(sql));

            return connection.QueryXml<X>(sql);
        }
    }

    public class DebugEntity
    {
        public Dictionary<String, String> Columns { get; set; }

        public Dictionary<String, DebugEntity[]> Relations { get; set; }
    }

    public class Entity
    {
        public Dictionary<String, String> ColumnValues { get; set; }

        public RelatedEntities[] Related { get; set; }
    }

    public class RelatedEntities
    {
        // These are for debug serialization
        public String RelationName { get; set; }
        public String TableName { get; set; }

        [JsonIgnore]
        public CMRelationEnd RelationEnd { get; set; }

        [JsonIgnore]
        public Extent Extent { get; set; }

        public Entity[] List { get; set; }
    }

    [DebuggerDisplay("{DebuggerDisplay}")]
    public class Extent
    {
        public String RelationName { get; set; }

        public ExtentFlavor Flavor { get; set; }

        public Int32? Limit { get; set; }

        public String Alias { get; set; }

        public String Index { get; set; }

        public String[] Columns { get; set; }

        public String[] Order { get; set; }

        public String[] Values { get; set; }

        public Boolean IsDescending { get; set; }

        public Extent[] Children { get; set; }

        String DebuggerDisplay => RelationName ?? "<root>";
    }

    [DebuggerDisplay("{ToString()}")]
    public struct ExtentFlavor
    {
        public ExtentFlavorType type;

        public Int32 depth;

        public String GetCssValue() => type.ToString().ToLower();

        public override string ToString()
        {
            return $"{type}-{depth}";
        }

        public static implicit operator ExtentFlavor((ExtentFlavorType type, Int32 depth) flavor)
        {
            return new ExtentFlavor { type = flavor.type, depth = flavor.depth };
        }
    }

    public enum ExtentFlavorType
    {
        None,
        Existence,
        Inline,
        Block,
        Page,
        Root
    }

    public class Relation
    {
        public RelationEnd Principal { get; set; }
        public RelationEnd Dependent { get; set; }
    }

    public class RelationEnd
    {
        public static String[] EmptyColumnArrays = new String[0];

        public String Name { get; set; }

        public String TableName { get; set; }

        public String KeyName { get; set; }

        // redudant if we always demand a key
        public String[] ColumnNames { get; set; } = EmptyColumnArrays;
    }

    public static class Extensions
    {
        public static SqlCommand CreateSqlCommandFromSql(this SqlConnection connection, String sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            return command;
        }

        public static X QueryXml<X>(this SqlConnection connection, String sql)
            where X : class
        {
            var command = connection.CreateSqlCommandFromSql(sql);

            using var reader = command.ExecuteXmlReader();

            var serializer = new XmlSerializer(typeof(X));

            var result = serializer.Deserialize(reader);

            return result as X;
        }

        public static XElement QueryXml(this SqlConnection connection, String sql)
        {
            var command = connection.CreateSqlCommandFromSql(sql);

            using var reader = command.ExecuteXmlReader();

            var xmlDocument = new XmlDocument();

            var rootRow = XElement.Load(reader);

            return rootRow;
        }
    }
}
