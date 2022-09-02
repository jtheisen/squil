using Squil.Core;
using Humanizer;
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
using TaskLedgering;
using NLog;

namespace Squil
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
            var rootTable = cmRoot.GetTable(ObjectName.RootName);

            var sql = String.Join(",\n", rootExtent.Children.Select(e => GetSql(e, rootTable, "_" + CreateSymbolForIdentifier(e.RelationName), 1)));

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
                $"Can't find relation for name '{extent.RelationName}' in table '{parentTable.Name.LastPart ?? "<root>"}'"
            );

            var alias = extent.Alias ?? (aliasPrefix + CreateSymbolForIdentifier(forwardEnd.Table.Name.LastPart));

            var children = (
                    from e in (extent.Children ?? Enumerable.Empty<Extent>())
                    select $"{GetSql(e, forwardEnd.Table, alias, indent + 1)}"
                )
                .ToArray();

            var joinPredicates = forwardEnd.Columns.Zip(forwardEnd.OtherEnd.Columns, (s, p) => $"{alias}.{s.Name} = {aliasPrefix}.{p.Name}");

            if ((extent.Values?.Length ?? 0) > (extent.Order?.Length ?? 0)) throw new Exception("More filter values than order columns");

            var valuesLength = extent.Values?.Length;

            // We currently only allow the last value to be unequal as otherwise we need a convoluted filter
            // predicate with uncertain performance characteristics.
            var filterItems = extent.Values?.Select((v, i) => (column: extent.Order[i], value: v, op: i < valuesLength - 1 || i < extent.KeyValueCount ? "=" : ">="));

            var filterPredicates = filterItems?.Select(i => $"{alias}.{i.column} {i.op} '{i.value}'") ?? Enumerable.Empty<String>();

            var allPredicates = filterPredicates.Concat(joinPredicates).ToArray();

            var whereLine = allPredicates.Length > 0 ? $"{ispace}where {string.Join($"\n{ispace}  and ", allPredicates)}\n" : "";

            var selectables = new List<String>();

            if (selectAllColumns)
            {
                selectables.Add("*");
            }
            else
            {
                selectables.AddRange(forwardEnd.Table.ColumnsInOrder.Select(c => c.Escaped));
            }

            if (extent.Values?.Length > 0)
            {
                var predicate = String.Join(" and ", from i in filterItems select $"{alias}.{i.column} = '{i.value}'");

                selectables.Add($"case when {predicate} then 1 else 0 end [{IsMatchingAlias}]");
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
{ipspace}from {forwardEnd.Table.Name.Escaped} {alias}
{whereLine}{orderLine}{ispace}for xml auto, type
{ipspace}) {GetXmlRelationName(extent.RelationName)}";

            return sql;
        }

        static String IsMatchingAlias = "__is-matching";

        Entity MakeEntity(Extent extent, CMTable table, XElement element)
        {
            var data = element.Attributes().ToDictionary(a => a.Name.LocalName.UnescapeSqlServerXmlName(), a => a.Value);

            return new Entity
            {
                IsMatching = data.GetOrDefault(IsMatchingAlias)?.Apply(im => im == "1"),
                ColumnValues = extent.Columns?.ToDictionary(c => c, c => data.GetValueOrDefault(c)) ?? Empties<String, String>.Dictionary,
                Related = extent.Children?.Select(c => MakeEntities(c, table, element.Element(XName.Get(GetXmlRelationName(c.RelationName))))).ToArray()
            };
        }

        RelatedEntities MakeEntities(Extent extent, CMTable parentTable, XElement element)
        {
            var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
                $"Can't find relation for name {extent.RelationName} in table {parentTable.Name.LastPart ?? "<root>"}"
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

        Entity MakeDummyEntity(Extent extent, CMTable table)
        {
            return new Entity
            {
                Related = extent.Children?.Select(c => MakeDummyEntities(c, table)).ToArray()
            };
        }

        RelatedEntities MakeDummyEntities(Extent extent, CMTable parentTable)
        {
            var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
                $"Can't find relation for name {extent.RelationName} in table {parentTable.Name.LastPart ?? "<root>"}"
            );

            var table = forwardEnd.OtherEnd.Table;

            return new RelatedEntities
            {
                Extent = extent,
                RelationEnd = forwardEnd,
                RelationName = extent.RelationName,
                TableName = forwardEnd.Table.Name,
                List = new[] { MakeDummyEntity(extent, forwardEnd.Table) }
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

        public (Entity entity, String sql, XElement resultXml) Query(SqlConnection connection, Extent extent)
        {
            var sql = GetCompleteSql(extent);

            var resultXml = connection.QueryXml(sql);

            var rootTable = cmRoot.GetTable(ObjectName.RootName);

            var entity = MakeEntity(extent, rootTable, resultXml);

            return (entity, sql, resultXml);
        }

        public Entity QueryDummy(Extent extent)
        {
            var rootTable = cmRoot.GetTable(ObjectName.RootName);

            return MakeDummyEntity(extent, rootTable);
        }

        public X Query<X>(SqlConnection connection, Extent extent)
            where X : class
        {
            var sql = GetCompleteSql(extent);

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
        public Boolean? IsMatching { get; set; }

        public Dictionary<String, String> ColumnValues { get; set; }

        public RelatedEntities[] Related { get; set; }
    }

    public class RelatedEntities
    {
        public String RelationName { get; set; }

        public ObjectName TableName { get; set; }

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

        public String RelationPrettyName { get; set; }

        public ExtentFlavor Flavor { get; set; }

        public String IndexName { get; set; }

        public Int32? Limit { get; set; }

        public String Alias { get; set; }

        public String Index { get; set; }

        public String[] Columns { get; set; }

        public String[] Order { get; set; }

        public String[] Values { get; set; }

        public Int32 KeyValueCount { get; set; }

        public Boolean IsDescending { get; set; }

        public Extent[] Children { get; set; }

        String DebuggerDisplay => RelationName ?? "<root>";
    }

    [DebuggerDisplay("{ToString()}")]
    public struct ExtentFlavor
    {
        public ExtentFlavorType type;

        public Int32 depth;

        public String GetCssValue(Boolean isLeaf)
        {
            return type.ToString().Kebaberize();
        }

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
        Inline2,
        Inline,
        Block,
        Page,
        BlockList, // list of blocks
        PageList // list of pages
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

        public ObjectName TableName { get; set; }

        public String KeyName { get; set; }

        // redudant if we always demand a key
        public String[] ColumnNames { get; set; } = EmptyColumnArrays;
    }

    public struct RelatedEntitiesListItemAnnotationInfo
    {
        public bool wasSearch;

        public int matchCount;
        public int afterCount;

        public string column;
        public string value;
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

        public static String QueryXmlString(this SqlConnection connection, String sql)
        {
            using var _ = GetCurrentLedger().TimedScope("query");

            var command = connection.CreateSqlCommandFromSql(sql);

            using var reader = command.ExecuteXmlReader();

            reader.Read();

            var xml = reader.ReadOuterXml();

            return xml;
        }

        public static X QueryXml<X>(this SqlConnection connection, String sql)
            where X : class
        {
            using var _ = GetCurrentLedger().TimedScope("query-parsing-and-binding");

            var command = connection.CreateSqlCommandFromSql(sql);

            using var reader = command.ExecuteXmlReader();

            var serializer = new XmlSerializer(typeof(X));

            var result = serializer.Deserialize(reader);

            return result as X;
        }

        public static XElement QueryXml(this SqlConnection connection, String sql)
        {
            using var _ = GetCurrentLedger().TimedScope("query-and-parsing");

            var command = connection.CreateSqlCommandFromSql(sql);

            using var reader = command.ExecuteXmlReader();

            var rootRow = XElement.Load(reader);

            return rootRow;
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
                return fk.GetIndexes()?.FirstOrDefault();
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

            return new RelatedEntitiesListItemAnnotationInfo
            {
                wasSearch = entities.Extent.KeyValueCount < valueCount,

                matchCount = entities.List.Count(e => e.IsMatching == true),
                afterCount = entities.List.Count(e => e.IsMatching == false),

                column = entities.Extent.Order[lastValueI],
                value = values[lastValueI],
            };
        }
    }
}
