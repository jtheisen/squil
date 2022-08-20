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

            var key = end.Key;

            var keyPart = parentEntity != null && end.Key != null
            ? $"/{key.Name}?" + String.Join("&", end.Key.Columns.Zip(end.OtherEnd.Key.Columns, (c, pc) => $"{c.c.Name}={parentEntity.ColumnValues[pc.c.Name]}"))
            : "";

            var url = RenderUrl($"{tableName.Escaped}{keyPart.TrimEnd('?')}");

            return url;
        }
    }

    public enum ColumnRenderClass
    {
        None,
        PrimaryName,
        Data
    }

    public delegate String UrlRenderer(String rest);

    public class EntityFieldUi
    {
        public Entity Entity { get; set; }

        public CMTable Table { get; set; }
    }

    public class EntityRelationFieldUi : EntityFieldUi
    {
        public RelatedEntities RelatedEntites { get; set; }
    }

    public class EntityColumnFieldUi : EntityFieldUi
    {
        public CMColumn Column { get; set; }

        public ColumnRenderClass RenderClass { get; set; }
    }
}
