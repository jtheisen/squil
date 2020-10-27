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

        public String RenderToHtml(RelatedEntities entities)
        {
            return Render(entities).ToString();
        }

        XElement RenderLink(String tableName, Object content)
        {
            return new XElement("a", new XAttribute("href", $"/query/{tableName}"), content);
        }

        XElement Render(Extent extent, Entity entity)
        {
            return new XElement("fieldset",
                new XAttribute("class", "entity"),
                extent.Columns.Select((c, i) => new XElement("div",
                    new XAttribute("class", "column"),
                    new XElement("label", c),
                    new XElement("div", entity.ColumnValues[i]?.Apply(t => new XText(t) as XObject) ?? nullSpan)
                )),
                new XElement("div",
                    entity.Related.Select(r => Render(r))
                )
            );
        }

        XElement Render(RelatedEntities entities)
        {
            return new XElement("div",
                new XAttribute("class", "relation"),
                new XElement("label", RenderLink(entities.TableName, entities.RelationName)),
                new XElement("ol",
                    entities.List.Select(entity => new XElement("li", Render(entities.Extent, entity)))
                )
            );
        }
    }
}
