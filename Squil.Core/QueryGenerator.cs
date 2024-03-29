﻿using Microsoft.Data.SqlClient;
using System.Data;
using System.Xml.Linq;

namespace Squil;

public record QuerySql(String Sql) : TaskLedgering.IReportResult
{
    public String ToReportString() => Sql;
}

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

    public String GetIdPredicateSql(Extent primaryExtent, String aliasPrefix)
    {
        var valuesLength = primaryExtent.Values?.Length;

        if (primaryExtent.KeyValueCount == 0) return null;

        String MakePredicate(String value, Int32 i)
        {
            var column = primaryExtent.Order[i];

            return $"{aliasPrefix}{column.Sql} = {value.ToSqlServerStringLiteral()}";
        }

        var sql = String.Join(" AND ", primaryExtent.Values.Take(primaryExtent.KeyValueCount).Select(MakePredicate));

        return sql;
    }

    public QuerySql GetCompleteSql(Extent rootExtent)
    {
        using var scope = GetCurrentLedger().TimedScope("create-query");

        var rootTable = cmRoot.GetTable(ObjectName.RootName);

        var selectables =
            $"(select max(modify_date) from sys.objects) [@{SchemaDateAlias}]".ToSingleton()
            .Concat(rootExtent.Children.Select(e => GetSql(e, rootTable, "_" + CreateSymbolForIdentifier(e.RelationName), 1)));

        var sql = String.Join(",\n", selectables);

        return scope.SetResult(new QuerySql($"select\n{sql}\n for xml path ('root')"));
    }

    String I(Int32 indent) => new String(' ', indent);

    String GetColumnSelectable(CMColumn column)
    {
        if (column.IsAssemblyType)
        {
            return $"cast({column.Escaped} as varchar(8000)) {column.Escaped}";
        }
        else
        {
            return $"{column.Escaped}";
        }
    }

    String GetSql(Extent extent, CMTable parentTable, String aliasPrefix, Int32 indent)
    {
        var ispace = I(indent);
        var ipspace = ispace + "  ";

        var forwardEnd = parentTable.Relations.GetValueOrDefault(extent.RelationName) ?? throw new Exception(
            $"Can't find relation for name '{extent.RelationName}' in table '{parentTable.Name.LastPart ?? "<root>"}'"
        );

        var alias = extent.Alias ?? (aliasPrefix + CreateSymbolForIdentifier(forwardEnd.Table.Name.LastPart));

        var joinPredicates = forwardEnd.Columns.Zip(forwardEnd.OtherEnd.Columns, (s, p) => $"{alias}.{s.Name} = {aliasPrefix}.{p.Name}");

        if ((extent.Values?.Length ?? 0) > (extent.Order?.Length ?? 0)) throw new Exception("More filter values than order columns");

        var valuesLength = extent.Values?.Length;

        (DirectedColumnName column, String value, String op, Boolean isKeyValue) MakeFilterItem(String value, Int32 i)
        {
            var column = extent.Order[i];
            // We currently only allow the last value to be unequal as otherwise we need a convoluted filter
            // predicate with uncertain performance characteristics.
            var op = column.d.GetOperator(i < valuesLength - 1 || i < extent.KeyValueCount);

            return (column, value, op, i < extent.KeyValueCount);
        }

        var filterItems = extent.Values?.Select(MakeFilterItem);

        var predicates = Enumerable.Empty<String>();
            
        predicates = predicates.Concat(filterItems?.Select(i => $"{alias}.{i.column.Sql} {i.op} {i.value.ToSqlServerStringLiteral()}") ?? Enumerable.Empty<String>());
        predicates = predicates.Concat(forwardEnd.Columns.Zip(forwardEnd.OtherEnd.Columns, (s, p) => $"{alias}.{s.Escaped} = {aliasPrefix}.{p.Escaped}"));

        String RenderScanMatchOption(ScanMatchOption o)
        {
            switch (o.Operator)
            {
                case ScanOperator.Equal:
                    return $"{alias}.{o.Column.Sql} = {o.Value.ToSqlServerStringLiteral()}";
                case ScanOperator.Substring:
                    return $"{alias}.{o.Column.Sql} like '%{o.Value.ToSqlServerLikeLiteralContent()}%'";
                default:
                    throw new Exception($"Unknown operator {o.Operator}");
            }
        }

        if (extent.ScanMatchOptions?.Length > 0)
        {
            var predicate = $"({String.Join(" or ", extent.ScanMatchOptions.Select(RenderScanMatchOption))})";

            predicates = predicates.Concat(predicate.ToSingleton());
        }
        else if (extent.ScanMatchOptions?.Length == 0 && !String.IsNullOrEmpty(extent.ScanValue))
        {
            // FIXME: a better way would be to make no request at all
            predicates = predicates.Concat("0 = 1".ToSingleton());
        }

        var allPredicates = predicates.ToArray();

        var whereLine = allPredicates.Length > 0 ? $"{ispace}where {string.Join($"\n{ispace}  and ", allPredicates)}\n" : "";

        var children = (
            from e in (extent.Children ?? Enumerable.Empty<Extent>())
            select $"{GetSql(e, forwardEnd.Table, alias, indent + 1)}"
        )
        .ToArray();

        var selectables = new List<String>();

        var columns = forwardEnd.Table.ColumnsInOrder.Where(c => !c.IsIgnoredByDefault).ToArray();

        if (selectAllColumns)
        {
            selectables.Add("*");
        }
        else
        {
            selectables.AddRange(columns.Select(GetColumnSelectable));
        }

        if (extent.Values?.Length > 0)
        {
            var predicate = String.Join(" and ", from i in filterItems select $"{alias}.{i.column.Sql} = {i.value.ToSqlServerStringLiteral()}");

            selectables.Add($"case when {predicate} then 1 else 0 end [{IsMatchingAlias}]");
        }

        selectables.AddRange(children);

        if (extent.SqlSelectables != null)
        {
            selectables.AddRange(from s in extent.SqlSelectables select $"({s.GetSql(alias)}) {s.Alias}");
        }

        if (selectables.Count == 0)
        {
            selectables.Add("42 dummy");
        }

        var selectsClause = String.Join(",\n", selectables.Select(s => $"{ipspace}{s}"));

        var orderLine = extent.Order?.Apply(o => $"{ipspace}order by {string.Join(", ", o.Select(c => $"{c.Sql}{c.d.GetSqlSuffix()}"))}\n") ?? "";

        var topClause = extent.Limit?.Apply(l => $" top {l}") ?? "";

        var comment = makePrettyQueries ? $" -- {extent.RelationName}" : "";

        var base64Option = columns.Any(c => c.Type is BinaryColumnType) ? ", binary base64" : "";

        var indexLine = extent.IndexName?.Apply(i => $"{ipspace}with (index ({i.EscapeNamePart()}))\n");

        var sql = @$"(select{topClause}{comment}
{selectsClause}
{ipspace}from {forwardEnd.Table.Name.Escaped} {alias}
{indexLine}{whereLine}{orderLine}{ispace}for xml auto{base64Option}, type
{ipspace}) [{extent.GetRelationAlias() /* further name part escaping not required, [ is already replaced */}]";

        return sql;
    }

    static String IsMatchingAlias = "__is-matching";
    static String SchemaDateAlias = "__schema-date";

    Entity MakeEntity(Extent extent, CMTable table, XElement element, Boolean isRoot = false)
    {
        var data = element.Attributes().ToDictionary(a => a.Name.LocalName.UnescapeSqlServerXmlName(), a => a.Value);

        return new Entity
        {
            SchemaDate = isRoot ? data.GetOrDefault(SchemaDateAlias)?.Apply(DateTime.Parse) : null,
            IsMatching = data.GetOrDefault(IsMatchingAlias)?.Apply(im => im == "1"),
            ColumnValues = extent.Columns?.ToDictionary(c => c, c => data.GetValueOrDefault(c)) ?? Empties<String, String>.Dictionary,
            Related = extent.Children?.Select(c => MakeEntities(c, table, element.Element(XName.Get(c.GetRelationAlias())))).ToArray()
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

    public async Task<Entity> QueryAsync(SqlConnection connection, Extent extent)
    {
        var sql = GetCompleteSql(extent);

        var resultXml = await connection.QueryAndParseXmlAsync(sql.Sql);

        var rootTable = cmRoot.GetTable(ObjectName.RootName);

        var entity = MakeEntity(extent, rootTable, resultXml, true);

        return entity;
    }

    public Entity QueryDummy(Extent extent)
    {
        var rootTable = cmRoot.GetTable(ObjectName.RootName);

        return MakeDummyEntity(extent, rootTable);
    }

    public X Query<X>(SqlConnection connection, Extent extent, out String xml)
        where X : class
    {
        var sql = GetCompleteSql(extent);

        return connection.QueryAndParseXml<X>(sql.Sql, out xml);
    }
}
