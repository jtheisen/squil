namespace Squil;

public class QueryUrlCreator
{
    public UrlRenderer UrlRenderer { get; }

    public QueryUrlCreator(String source)
    {
        UrlRenderer = MakeUrlRenderer(source);
    }

    public static UrlRenderer MakeUrlRenderer(String source)
        => new UrlRenderer("ui/" + source);

    public String RenderUrl(LocationQueryLocation l)
    {
        var query = new List<(String prefix, String key, String value)>();

        foreach (String kp in l.KeyParams)
        {
            query.Add(("$", kp, l.KeyParams[kp]));
        }

        foreach (String rp in l.RestParams)
        {
            query.Add(("", rp, l.KeyParams[rp]));
        }

        var url = UrlRenderer.RenderUrl(new[] { "tables", l.Schema, l.Table, l.Index, l.Column }, query);

        return url;
    }

    public String RenderEntityUrl(CMTable table, IMapping<String, String> values, String focusColumn = null, LocationQueryOperationType? insertMode = null)
    {
        if (table == null) return null;

        return table.PrimaryKey?.Apply(k => RenderEntityUrl(k, values, focusColumn, insertMode));
    }

    public String RenderEntityUrl(CMIndexlike key, IMapping<String, String> values, String focusColumn = null, LocationQueryOperationType? operationType = null)
    {
        return RenderEntityUrl(key, key, values, focusColumn, operationType);
    }

    public String RenderEntityUrl(CMIndexlike key, CMIndexlike keyForValues, IMapping<String, String> values, String focusColumn = null, LocationQueryOperationType? operationType = null)
    {
        var (schema, table) = key.Table.Name;

        var path = new[] { "tables", schema, table, key.Name, focusColumn };

        // Values can be null and should be ommitted when key is actually the backing index
        // of a foreign key we're using in the location of an insert operation.

        var query =
            from c in (keyForValues ?? key).Columns
            let v = values.GetValue(c.c.Name)
            where v != null
            select ("$", c.c.Name, v);

        if (operationType.HasValue)
        {
            query = ("", "operation", operationType.ToString().ToLower()).ToSingleton().Concat(query);
        }

        var url = UrlRenderer.RenderUrl(path, query);

        return url;
    }

    String RenderColumnTupleUrl(
        CMTable table,
        CMColumnTuple columnsOnTarget,
        CMColumnTuple columnsOnSource,
        IMap<String, String> columnValueSource,
        CMIndexlike index = null,
        String backRelation = null,
        QuerySearchMode? mode = null,
        String focusColumn = null
    )
    {
        var query = new List<(String prefix, String key, String value)>();

        if (columnsOnTarget != null && columnsOnSource != null)
        {
            var columnValues = columnsOnSource.Columns
                // If columnValueSource doesn't contain a value for the column at all (not even null),
                // it is a embryo during an insert operation. This function should not be called in this case.
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

        var url = UrlRenderer.RenderUrl(new[] { "tables", schema, tableName, index?.Name, focusColumn }, query);

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
