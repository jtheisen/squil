namespace Squil;

public class ExtentFactory
{
    HashSet<CMRelationEnd> path = new HashSet<CMRelationEnd>();

    private readonly int? totalLimit;

    public record PrincipalLocation(CMRelationEnd Relation, DirectedColumnName[] KeyColumns, String[] KeyValues);

    public ExtentFactory(Int32? totalLimit)
    {
        this.totalLimit = totalLimit;
    }

    public Extent CreateRootExtentForTable(
        CMTable table, ExtentFlavorType primaryFlavor,
        CMIndexlike index = null, DirectedColumnName[] order = null, String[] values = null, Int32? keyValueCount = null,
        Int32? listLimit = null, PrincipalLocation principal = null
    )
    {
        Debug.Assert(table.Root.RootTable != table);

        var root = table.Root.RootTable;

        var rootToPrimary = root.Relations[table.Name.Simple];

        var extents = new List<Extent>();

        if (principal != null)
        {
            var tableToPrincipal = table.Relations[principal.Relation.Name];

            var rootToPrincipal = root.Relations[tableToPrincipal.Table.Name.Simple];

            if (rootToPrincipal != null)
            {
                var principalExtent = CreateExtent(rootToPrincipal, (ExtentFlavorType.BreadcrumbList, 2), order: principal.KeyColumns, values: principal.KeyValues);

                principalExtent.RelationAlias = "principal";
                principalExtent.KeyValueCount = principal.KeyColumns.Length;
                principalExtent.IgnoreOnRender = true;

                extents.Add(principalExtent);
            }
        }

        var primaryExtent = CreateExtent(rootToPrimary, (primaryFlavor, 2), index, order, values);

        primaryExtent.IndexName = index?.Name;
        primaryExtent.RelationAlias = "primary";
        primaryExtent.KeyValueCount = keyValueCount ?? 0;
        primaryExtent.Limit = listLimit ?? primaryExtent.Limit;

        extents.Add(primaryExtent);

        return new Extent
        {
            Limit = 2,
            Flavor = (primaryFlavor, 2),
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

    Extent CreateExtent(CMRelationEnd end, ExtentFlavor parentFlavor, CMIndexlike index = null, DirectedColumnName[] order = null, String[] values = null)
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
            case ExtentFlavorType.Inline:
            case ExtentFlavorType.Breadcrumb:
            case ExtentFlavorType.Flow1:
                return 2;
            case ExtentFlavorType.Flow3:
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
            case ExtentFlavorType.BreadcrumbList:
                return (ExtentFlavorType.Breadcrumb, 1);
            case ExtentFlavorType.PageList:
                return (ExtentFlavorType.Page, 1);
            case ExtentFlavorType.BlockList:
                return (ExtentFlavorType.Block, 1);
            case ExtentFlavorType.Page:
                return end.IsMany ? (ExtentFlavorType.Flow3, 1) : (ExtentFlavorType.Flow1, 1);
            case ExtentFlavorType.Block:
                return (ExtentFlavorType.None, 0);
            case ExtentFlavorType.Flow1:
            case ExtentFlavorType.Flow3:
            case ExtentFlavorType.Breadcrumb:
                return end.IsMany || !end.IsUniquelyTyped ? (ExtentFlavorType.None, 0) : (ExtentFlavorType.Inline, 1);
            case ExtentFlavorType.Inline:
            default:
                return (ExtentFlavorType.None, 0);
        }
    }
}
