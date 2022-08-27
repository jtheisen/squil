using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Squil
{
    public class ExtentFactory
    {
        HashSet<CMRelationEnd> path = new HashSet<CMRelationEnd>();

        private readonly int? totalLimit;

        public ExtentFactory(Int32? totalLimit)
        {
            this.totalLimit = totalLimit;
        }

        public Extent CreateRootExtent(CMTable table, ExtentFlavorType type, CMIndexlike index = null, String[] order = null, String[] values = null)
        {
            if (table == table.Root.RootTable)
            {
                return CreateExtentForRoot(table);
            }
            else
            {
                var root = table.Root.RootTable;

                var rootRelation = root.Relations[table.Name.Simple];

                return new Extent
                {
                    Limit = 2,
                    Flavor = (type, 2),
                    Children = new[] { CreateExtent(rootRelation, (type, 2), index, order, values) }
                };
            }
        }

        public Extent CreateExtentForRoot(CMTable rootTable)
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
            return parentTable.Relations.Values
                .Where(e => !path.Contains(e))
                .Select(e => CreateExtent(e, parentFlavor))
                .Where(e => e != null)
                ;
        }

        Extent CreateExtent(CMRelationEnd end, ExtentFlavor parentFlavor, CMIndexlike index = null, String[] order = null, String[] values = null)
        {
            if (order != null)
            {
                foreach (var column in order) column.Assert(o => end.Table.Columns.ContainsKey(o),
                    $"Extent order column '{column}' is not in table '{end.Table.Name.LastPart}'");
            }

            var flavor = ReduceFlavor(parentFlavor, end);

            if (flavor.type == ExtentFlavorType.None || flavor.depth < 0) return null;

            path.Add(end);

            try
            {
                return new Extent
                {
                    IndexName = (index ?? end.Table.Indexes.Values.FirstOrDefault())?.Name,
                    Flavor = flavor,
                    RelationName = end.OtherEnd.Name,
                    Limit = GetLimitInFlavor(flavor.type),
                    Children = CreateSubExtents(end.Table, flavor).ToArray(),
                    Columns = SelectColumns(flavor, end.Table).Select(c => c.Name).ToArray(),
                    Order = order,
                    Values = values
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
                    return 3;
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
                    return (ExtentFlavorType.Inline, 1);
                case ExtentFlavorType.Inline:
                    return end.IsMany || !end.IsUniquelyTyped ? (ExtentFlavorType.None, 0) : (ExtentFlavorType.Inline2, 1);
                case ExtentFlavorType.Inline2:
                default:
                    return (ExtentFlavorType.None, 0);
            }
        }
    }
}
