﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Humanizer;

namespace Acidui
{
    public enum ColumnRenderClass
    {
        None,
        Name,
        String
    }

    public class HtmlRenderer
    {
        static readonly Object genericHandleDiv = new XElement("div", "◯");
        static readonly Object nullSpan = new XElement("span", new XAttribute("class", "null-value"));
        static readonly Object emptySpan = new XElement("span", new XAttribute("class", "empty-value"));
        static readonly Object wsSpan = new XElement("span", new XAttribute("class", "ws-value"));

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

            var keyPart = entity != null && end.Key != null
                ? $"/{key.Name}?" + String.Join("&", end.Key.Columns.Zip(end.OtherEnd.Key.Columns, (c, pc) => $"{c.Name}={entity.ColumnValues[pc.Name]}"))
                : "";

            var content = end.IsMany ? tableName.LastPart : tableName.LastPart.Singularize();

            return new XElement("a", new XAttribute("href", $"/query/{tableName.Escaped}{keyPart.TrimEnd('?')}"), content);
        }

        Object RenderLink(Entity entity, CMKey key, Object content)
        {
            var keyPart = entity != null && key != null
                ? $"/{key.Name}?" + String.Join("&", key.Columns.Select(c => $"{c.Name}={entity.ColumnValues[c.Name]}"))
                : "";

            return new XElement("a", new XAttribute("href", $"/query/{key.Table.Name.Escaped}{keyPart.TrimEnd('?')}"), content);
        }

        XElement Render(Entity entity, RelatedEntities parentCollection = null)
        {
            var table = parentCollection?.RelationEnd.Table;

            var flavor = parentCollection?.Extent.Flavor;

            var columns = (parentCollection?.Extent?.Columns ?? Enumerable.Empty<String>())
                .Select(cn => table.Columns[cn]).ToLookup(c => GetRenderClass(table, c));

            var columnClasses = Enum.GetValues(typeof(ColumnRenderClass)) as ColumnRenderClass[];

            var groups =
                from cl in columnClasses
                from c in columns[cl]
                where IsColumnRenderedInFlavor(c, flavor?.type ?? ExtentFlavorType.Page)
                group c by cl
                ;

            var handle = table != null ? RenderAsPrimaryLink(table, entity, new XElement("span", new XAttribute("class", "entity-handle"), table.Abbreviation)) : null;

            return new XElement("fieldset",
                new XAttribute("class", "entity"),
                handle,
                groups.Select(g => new XElement("div",
                    new XAttribute("class", "column-group"),
                    new XAttribute("data-group", g.Key.ToString().ToLower()),
                    g.Select(c => RenderColumn(entity, table, c, g.Key)))),
                new XElement("div",
                    entity.Related.Select(r => Render(r, entity))
                )
            );
        }

        Boolean IsColumnRenderedInFlavor(CMColumn column, ExtentFlavorType flavorType)
        {
            switch (flavorType)
            {
                case ExtentFlavorType.Inline:
                case ExtentFlavorType.Block:
                    return column.IsPrimaryName;
                case ExtentFlavorType.Page:
                    return true;
                default:
                    return false;
            }
        }

        Object RenderText(String value)
        {
            if (value == null) return nullSpan;

            if (value.Length == 0) return emptySpan;

            if (String.IsNullOrWhiteSpace(value)) return wsSpan;

            return value;
        }

        Object RenderAsPrimaryLink(CMTable table, Entity entity, Object content)
        {
            return table.PrimaryKey?.Apply(k => RenderLink(entity, k, content)) ?? content;
        }

        XElement RenderColumn(Entity entity, CMTable table, CMColumn column, ColumnRenderClass cls)
        {
            var content = RenderText(entity.ColumnValues[column.Name]);

            if (cls == ColumnRenderClass.Name)
            {
                content = RenderAsPrimaryLink(table, entity, content);
            }

            return new XElement("div",
                new XAttribute("class", "column"),
                new XElement("label", column.Name),
                new XElement("div", content)
            );
        }

        ColumnRenderClass GetRenderClass(CMTable table, CMColumn column)
        {
            if (table.PrimaryNameColumn == column) return ColumnRenderClass.Name;

            return ColumnRenderClass.String;
        }

        XElement Render(RelatedEntities entities, Entity parentEntity = null)
        {
            Object labelContent = entities.RelationEnd.IsMany ? entities.TableName.LastPart : entities.TableName.LastPart.Singularize();

            // In the singular case, a link is either superfluous (when there's is an entry)
            // or misleading (when there isn't), so we better not render it as a link at all.
            if(entities.RelationEnd.IsMany && parentEntity != null)
            {
                labelContent = RenderLink(parentEntity, entities);
            }

            return new XElement("div",
                new XAttribute("class", "relation"),
                new XElement("label", labelContent),
                new XElement("ol",
                    entities.List.Length == 0 ? new XAttribute("class", "is-empty") : null,
                    entities.Extent.Flavor.Apply(f => new XAttribute("data-flavor", f.GetCssValue())),
                    entities.List.Select(entity => new XElement("li", Render(entity, entities)))
                )
            );
        }
    }
}
