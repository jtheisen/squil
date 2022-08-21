namespace Squil
{
    public class QueryContext
    {
        public UrlRenderer RenderUrl { get; }

        public QueryContext(UrlRenderer renderUrl)
        {
            RenderUrl = renderUrl;
        }

        public String RenderEntityUrl(CMTable table, Entity entity)
        {
            if (table == null) return null;

            return table.PrimaryKey?.Apply(k => RenderEntityUrl(entity, k));
        }

        public String RenderEntityUrl(Entity entity, CMIndexlike key)
        {
            var keyPart = entity != null && key != null
                ? $"/{key.Name}?" + String.Join("&", key.Columns.Select(c => $"{c.c.Name}={entity.ColumnValues[c.c.Name]}"))
                : "";

            var url = RenderUrl($"{key.Table.Name.Escaped}{keyPart.TrimEnd('?')}");

            return url;
        }

        public String RenderEntitiesUrl(Entity parentEntity, RelatedEntities relatedEntities)
        {
            if (parentEntity == null || !relatedEntities.RelationEnd.IsMany) return null;

            var tableName = relatedEntities.TableName;

            var end = relatedEntities.RelationEnd;

            var index = end.Key is CMForeignKey fk ? fk.GetIndexes()?.FirstOrDefault() : end.Key;

            // We're allowing no index if we're looking at an entire table
            if (end.Key.Name != "" && index == null) return null;

            var keyPart = parentEntity != null && end.Key != null
            ? $"/{index?.Name}?" + String.Join("&", end.Key.Columns.Zip(end.OtherEnd.Key.Columns, (c, pc) => $"{c.c.Name}={parentEntity.ColumnValues[pc.c.Name]}"))
            : "";

            var url = RenderUrl($"{tableName.Escaped}{keyPart.TrimEnd('?')}");

            return url;
        }
    }
}
