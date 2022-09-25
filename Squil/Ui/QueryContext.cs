namespace Squil;

public class QueryContext
{
    public Boolean InDebug { get; set; }

    public UrlRenderer UrlRenderer { get; }

    public QueryContext(UrlRenderer urlRenderer)
    {
        UrlRenderer = urlRenderer;
    }

    public String RenderEntityUrl(CMTable table, Entity entity)
    {
        if (table == null) return null;

        return table.PrimaryKey?.Apply(k => RenderEntityUrl(entity, k));
    }

    public String RenderEntityUrl(Entity entity, CMIndexlike key)
    {
        var (schema, table) = key.Table.Name;

        var url = UrlRenderer.RenderUrl(
            new[] { "tables", schema, table, key.Name },
            from c in key.Columns select ("$", c.c.Name, entity.ColumnValues[c.c.Name])
        );

        return url;
    }

    String RenderColumnTupleUrl(
        CMTable table,
        CMColumnTuple columnsOnTarget,
        CMColumnTuple columnsOnSource,
        IMap<String, String> columnValueSource,
        CMIndexlike index = null,
        String backRelation = null,
        QuerySearchMode? mode = null
    )
    {
        var query = new List<(String prefix, String key, String value)>();

        if (columnsOnTarget != null && columnsOnSource != null)
        {
            var columnValues = columnsOnSource.Columns
                .Select(c => columnValueSource[c.c.Name])
                .TakeWhile(c => c != null);

            query.AddRange(columnsOnTarget.Columns.Zip(columnValues, (ic, cv) => ("$", ic.c.Name, cv)));
        }

        if (backRelation != null)
        {
            query.Add(("", "from", backRelation));
        }

        if (mode != null)
        {
            query.Add(("", "search", mode.ToString().ToLower()));
        }

        var (schema, tableName) = table.Name;

        var url = UrlRenderer.RenderUrl(new[] { "tables", schema, tableName, index?.Name }, query);

        return url;
    }

    public String RenderEntitiesUrl(CMRelationEnd end, CMIndexlike index, IMap<String, String> values)
    {
        // We're allowing no index if we're looking at an entire table
        if (end.Key.Name != "" && index == null) return null;

        return RenderColumnTupleUrl(end.Table, end.Key, end.OtherEnd.Key, values, index, end.Name);
    }

    public String RenderEntitiesUrl(Entity parentEntity, RelatedEntities relatedEntities)
    {
        if (parentEntity == null || !relatedEntities.RelationEnd.IsMany) return null;

        var end = relatedEntities.RelationEnd;

        var index = relatedEntities.ChooseIndex();

        return RenderEntitiesUrl(end, index, parentEntity.ColumnValues.AsMap());
    }

    public String RenderIndexOrTableUrl(CMTable table, CMIndexlike index, IMap<String, String> columnValueSource, String backRelation, QuerySearchMode? mode = null)
    {
        return RenderColumnTupleUrl(table, index, index, columnValueSource, index, backRelation, mode);
    }

    public String RenderTableUrl(CMTable table)
    {
        return RenderColumnTupleUrl(table, null, null, Empties<String, String>.Map);
    }
}
