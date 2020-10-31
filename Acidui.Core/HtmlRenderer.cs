using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Acidui
{
    public class HtmlRenderer
    {
        static readonly XElement nullSpan = new XElement("span", new XAttribute("class", "null-value"));

        public String RenderToHtml(Entity entity)
        {
            return Render(entity).ToString();
        }

        public String RenderToHtml(RelatedEntities entities)
        {
            return Render(entities).ToString();
        }

        XElement RenderLink(Entity entity, RelatedEntities relatedEntities)
        {
            var tableName = relatedEntities.TableName;

            var end = relatedEntities.RelationEnd;

            var key = end.Key;

            var parentKey = end.OtherEnd.Key;

            var keyPart = entity != null && end.Key != null
                ? $"/{key.Name}?" + String.Join("&", end.Key.Columns.Zip(end.OtherEnd.Key.Columns, (c, pc) => $"{c.Name}={entity.ColumnValues[pc.Order]}"))
                : "";

            return new XElement("a", new XAttribute("href", $"/query/{tableName}{keyPart.TrimEnd('?')}"), relatedEntities.RelationName);
        }

        XElement Render(Entity entity, String[] columns = null)
        {
            return new XElement("fieldset",
                new XAttribute("class", "entity"),
                columns?.Select((c, i) => new XElement("div",
                    new XAttribute("class", "column"),
                    new XElement("label", c),
                    new XElement("div", entity.ColumnValues[i]?.Apply(t => new XText(t) as XObject) ?? nullSpan)
                )),
                new XElement("div",
                    entity.Related.Select(r => Render(r, entity))
                )
            );
        }

        XElement Render(RelatedEntities entities, Entity parentEntity = null)
        {
            return new XElement("div",
                new XAttribute("class", "relation"),
                new XElement("label", RenderLink(parentEntity, entities)),
                new XElement("ol",
                    entities.List.Select(entity => new XElement("li", Render(entity, entities.Extent.Columns)))
                )
            );
        }
    }
}
