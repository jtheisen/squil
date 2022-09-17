using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Squil
{
    public class ExtentFactory
    {
        HashSet<CMRelationEnd> path = new HashSet<CMRelationEnd>();

        private readonly int? totalLimit;

        public record PrincipalLocation(String Relation, DirectedColumnName[] KeyColumns, String[] KeyValues);

        public ExtentFactory(Int32? totalLimit)
        {
            this.totalLimit = totalLimit;
        }

        public Extent CreateRootExtentForTable(
            CMTable table, ExtentFlavorType type,
            CMIndexlike index = null, DirectedColumnName[] order = null, String[] values = null, Int32? keyValueCount = null,
            Int32? listLimit = null, PrincipalLocation principal = null
        )
        {
            Debug.Assert(table.Root.RootTable != table);

            var root = table.Root.RootTable;

            var rootRelation = root.Relations[table.Name.Simple];

            var extents = new List<Extent>();

            extents.Add(CreateExtent(rootRelation, (type, 2), index, order, values, keyValueCount ?? 0, listLimit));

            if (principal != null)
            {
                var tableToPrincipal = table.Relations[principal.Relation];

                var rootToPrincipal = root.Relations[tableToPrincipal.Table.Name.Simple];

                if (rootToPrincipal != null)
                {
                    extents.Add(CreateExtent(rootToPrincipal, (ExtentFlavorType.BlockList, 2), order: principal.KeyColumns, values: principal.KeyValues, keyValueCount: principal.KeyColumns.Length));
                }
            }

            return new Extent
            {
                Limit = 2,
                Flavor = (type, 2),
                Children = extents.ToArray()
            };
        }

        public Extent CreateRootExtentForRoot(CMTable rootTable)
        {
            return new Extent
            {
                Limit = 2,
                Flavor = (ExtentFlavorType.Page, 1),
                Children = CreateSubExtents(rootTable, (ExtentFlavorType.Page, 1)).ToArray()
            };
        }

        IEnumerable<Extent> CreateSubExtents(CMTable parentTable, ExtentFlavor parentFlavor)
        {
            return
                from end in parentTable.Relations.Values
                where !path.Contains(end)
                let index = end.GetIndex()
                where index != null
                let extent = CreateExtent(end, parentFlavor, index)
                where extent != null
                select extent;
        }

        Extent CreateExtent(CMRelationEnd end, ExtentFlavor parentFlavor, CMIndexlike index = null, DirectedColumnName[] order = null, String[] values = null, Int32 keyValueCount = 0, Int32? listLimit = null)
        {
            if (order != null)
            {
                foreach (var column in order) column.Assert(o => end.Table.Columns.ContainsKey(o),
                    $"Extent order column '{column.c}' is not in table '{end.Table.Name.LastPart}'");
            }

            var flavor = ReduceFlavor(parentFlavor, end);

            if (flavor.type == ExtentFlavorType.None || flavor.depth < 0) return null;

            path.Add(end);

            try
            {
                return new Extent
                {
                    IndexName = index?.Name,
                    Flavor = flavor,
                    RelationName = end.OtherEnd.Name,
                    Limit = listLimit ?? GetLimitInFlavor(flavor.type),
                    Children = CreateSubExtents(end.Table, flavor).ToArray(),
                    Columns = SelectColumns(flavor, end.Table).Select(c => c.Name).ToArray(),
                    Order = order,
                    Values = values,
                    KeyValueCount = keyValueCount
                };
            }
            finally
            {
                path.Remove(end);
            }
        }

        IEnumerable<CMColumn> SelectColumns(ExtentFlavor flavor, CMTable table)
        {
            // We need to request all key columns in any case, as we need those
            // to form hyperlinks to the entity

            switch (flavor.type)
            {
                default:
                    return table.ColumnsInOrder;
            }
        }

        Int32 GetLimitInFlavor(ExtentFlavorType type)
        {
            switch (type)
            {
                case ExtentFlavorType.Existence:
                    return 1;
                case ExtentFlavorType.Inline2:
                    return 2;
                case ExtentFlavorType.Inline:
                    return 4;
                case ExtentFlavorType.Block:
                case ExtentFlavorType.Page:
                    return 4;
                case ExtentFlavorType.BlockList:
                    return 10;
                case ExtentFlavorType.PageList:
                    return 2;
                default:
                    return totalLimit ?? 2;
            }
        }

        ExtentFlavor ReduceFlavor(ExtentFlavor flavor, CMRelationEnd end)
        {
            switch (flavor.type)
            {
                case ExtentFlavorType.PageList:
                    return (ExtentFlavorType.Page, 1);
                case ExtentFlavorType.BlockList:
                    return (ExtentFlavorType.Block, 1);
                case ExtentFlavorType.Page:
                    return end.IsMany ? (ExtentFlavorType.Inline, 1) : (ExtentFlavorType.Inline, 1);
                case ExtentFlavorType.Block:
                    return (ExtentFlavorType.None, 0);
                case ExtentFlavorType.Inline:
                    return end.IsMany || !end.IsUniquelyTyped ? (ExtentFlavorType.None, 0) : (ExtentFlavorType.Inline2, 1);
                case ExtentFlavorType.Inline2:
                default:
                    return (ExtentFlavorType.None, 0);
            }
        }
    }
}
