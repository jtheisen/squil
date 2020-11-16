using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Xml.Linq;
using System.Xml.Xsl;
using Humanizer;

namespace Acidui
{
    public enum ColumnRenderClass
    {
        None,
        PrimaryName,
        Data
    }

    public class HtmlRenderer
    {
        static readonly Object nullSpan = new XElement("span", new XAttribute("class", "null-value"));
        static readonly Object emptySpan = new XElement("span", new XAttribute("class", "empty-value"));
        static readonly Object wsSpan = new XElement("span", new XAttribute("class", "ws-value"));

        Int32 debugId = 0;

        public String RenderToHtml(Entity entity)
        {
            return new XElement("div",
                RenderEntityContent(entity)
            ).ToString(SaveOptions.DisableFormatting);
        }

        XElement RenderLink(Entity entity, RelatedEntities relatedEntities, Object content)
        {
            var tableName = relatedEntities.TableName;

            var end = relatedEntities.RelationEnd;

            var key = end.Key;

            var keyPart = entity != null && end.Key != null
                ? $"/{key.Name}?" + String.Join("&", end.Key.Columns.Zip(end.OtherEnd.Key.Columns, (c, pc) => $"{c.Name}={entity.ColumnValues[pc.Name]}"))
                : "";

            return new XElement("a", new XAttribute("href", $"/query/{tableName.Escaped}{keyPart.TrimEnd('?')}"), content);
        }

        Object RenderRelationName(RelatedEntities relatedEntities)
        {
            var tableName = relatedEntities.TableName;

            var end = relatedEntities.RelationEnd;

            var parts = tableName.GetDistinguishedParts(end.OtherEnd.Table.Name);

            if (parts.Length == 0) throw new Exception("Unexpectedly no parts for relation name");

            var result = parts.Reverse().Select((p, i) => new XElement("span", i == 0 && !end.IsMany ? p.Singularize() : p)).ToList();

            var fk = end.GetForeignKey()?.Name;

            if (!end.IsUniquelyTyped && !String.IsNullOrWhiteSpace(fk))
            {
                result.Add(new XElement("span", new XAttribute("class", "entity-relation-name-fk"), fk));
            }

            return result;
        }

        Object RenderLink(Entity entity, CMKey key, Object content)
        {
            var keyPart = entity != null && key != null
                ? $"/{key.Name}?" + String.Join("&", key.Columns.Select(c => $"{c.Name}={entity.ColumnValues[c.Name]}"))
                : "";

            return new XElement("a", new XAttribute("href", $"/query/{key.Table.Name.Escaped}{keyPart.TrimEnd('?')}"), content);
        }

        Object RenderEntityContent(Entity entity, RelatedEntities parentCollection = null)
        {
            var fieldsets = ProduceFieldsets(entity, parentCollection);

            return new[] {
                parentCollection?.RelationEnd.Table.Apply(t => RenderEntityHeader(t, entity)),
                fieldsets.Select(fs => new XElement("fieldset",
                    new XAttribute("data-name", fs.legend.ToString()),
                    fs.layout?.Apply(l => new XAttribute("data-layout", l)),
                    fs.items.FirstOrDefault() == null ? new XAttribute("class", "fieldset-empty") : null,
                    new XElement("legend", fs.legend),
                    fs.items.ToArray()
                ))
            };
        }

        IEnumerable<Fieldset> ProduceFieldsets(Entity entity, RelatedEntities parentCollection = null)
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
                group c by cl into g
                select new Fieldset { legend = g.Key.ToString().ToLower(), layout = "grid3", items = g.Select(c => RenderColumn(entity, table, c, g.Key)) }
                ;

            var relations = (from r in entity.Related group r by r.RelationEnd.IsMany).ToArray();

            var singulars = relations.FirstOrDefault(r => !r.Key)?.ToArray();
            var plurals = relations.FirstOrDefault(r => r.Key)?.ToArray();

            var groupsList = groups.ToList();

            if (singulars != null)
            {
                groupsList.Add(new Fieldset { legend = "singulars", items = singulars.Select(r => RenderRelation(r, entity)) });
            }

            if (plurals != null)
            {
                groupsList.Add(new Fieldset { legend = "plurals", items = plurals.Select(r => RenderRelation(r, entity)) });
            }

            return groupsList;
        }

        struct Fieldset
        {
            public Object legend;
            public String layout;
            public IEnumerable<Object> items;
        }


        Object RenderEntityHeader(CMTable table, Entity entity)
        {
            var name = table.PrimaryNameColumn?.Apply(nc => entity.ColumnValues[nc.Name]);

            var content = RenderAsPrimaryLink(table, entity, new object[]
            {
                new XElement("span", new XAttribute("class", "entity-thumb"), table.Abbreviation),
                name?.Apply(n => new XElement("span", new XAttribute("class", "entity-name"), n))
            });

            return new XElement("header", content);
        }

        Boolean IsColumnRenderedInFlavor(CMColumn column, ExtentFlavorType flavorType)
        {
            switch (flavorType)
            {
                case ExtentFlavorType.Page:
                    return !column.IsPrimaryName;
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

            if (cls == ColumnRenderClass.PrimaryName)
            {
                content = RenderAsPrimaryLink(table, entity, content);
            }

            return new XElement("div",
                new XAttribute("class", "entity-column"),
                new XAttribute("data-x-name", column.Name),
                new XElement("label", column.Name),
                new XElement("div", content)
            );
        }

        ColumnRenderClass GetRenderClass(CMTable table, CMColumn column)
        {
            if (table.PrimaryNameColumn == column) return ColumnRenderClass.PrimaryName;

            return ColumnRenderClass.Data;
        }

        XElement RenderRelation(RelatedEntities entities, Entity parentEntity = null)
        {
            var labelContent = RenderRelationName(entities);

            // In the singular case, a link is either superfluous (when there's is an entry)
            // or misleading (when there isn't), so we better not render it as a link at all.
            if(entities.RelationEnd.IsMany && parentEntity != null)
            {
                labelContent = RenderLink(parentEntity, entities, labelContent);
            }

            var lastIndex = entities.Extent.Limit?.Apply(l => l - 1);

            var classes = new List<String>();

            classes.Add("entity-relation");
            classes.Add(entities.RelationEnd.IsMany ? "entity-relation-plural" : "entity-relation-singular");
            if (entities.List.Length == 0) classes.Add("is-empty");


            return new XElement("div",
                new XAttribute("class", String.Join(" ", classes)),

                //new XAttribute("data-debug-id", ++debugId),
                //new XAttribute("data-debug-target-table", entities.RelationEnd.Table.Name.Escaped),
                //entities.RelationEnd.GetForeignKey()?.Apply(k => new XAttribute("data-debug-fk", k.Name)),
                new XAttribute("data-debug-not-uniquely-typed-witness", entities.RelationEnd.AmbiguouslyTypedWitness?.Name ?? "-"),
                //new XAttribute("data-debug-shared-target", entities.RelationEnd.OtherEnd.Table.RelationsForTable[entities.RelationEnd.Table.Name].Count()),

                new XElement("label", labelContent),
                new XElement("ol",
                    entities.Extent.Flavor.Apply(f => new XAttribute("data-flavor", f.GetCssValue())),
                    entities.List.Select((entity, i) => new XElement("li",
                        i == lastIndex ? new XAttribute("class", "potentially-last") : null,
                        RenderEntityContent(entity, entities))
                    )
                )
            );
        }
    }
}
