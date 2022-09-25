using Squil.SchemaBuilding;

namespace Squil;

public record UnsuitableIndexesVm(CsdUnsupportedReason Reason, IEnumerable<SearchOptionVm> Indexes);

public record SearchFieldVm(String ColumnName, String DisplayName);

public enum SearchOptionType
{
    NoOption,
    Seek,
    Scan
}

public record ValidationValueVm(Boolean isInvalid = false);

public class SearchOptionVm
{
    public ValidationValueVm[] ValidationValues { get; private set; }

    public SearchOptionType OptionType { get; }

    public CMIndexlike Index { get; }

    public SearchFieldVm[] Fields { get; }

    public Boolean IsCurrent { get; set; }

    public CsdUnsupportedReason UnsupportedReason { get; set; }

    Int32 hiddenPrefixLength;

    public SearchOptionVm(CMIndexlike index, Int32 hiddenPrefixLength)
    {
        this.hiddenPrefixLength = hiddenPrefixLength;

        OptionType = SearchOptionType.Seek;
        Index = index;
        Fields = index.ColumnNames.Skip(hiddenPrefixLength).Select(n => new SearchFieldVm(n, n)).ToArray();
    }

    public SearchOptionVm(SearchOptionType optionType)
    {
        OptionType = optionType;
        Index = null;
        Fields = new[] { new SearchFieldVm("", "") };
    }

    public void SetValidatedValues(ValidationResult[] values)
    {
        ValidationValues = values.Skip(hiddenPrefixLength).Select(v => new ValidationValueVm(!v.IsOk)).ToArray();
    }
}

public class LocationQueryVm
{
    public LocationQueryRequest LastRequest { get; private set; }
    
    public LocationQueryResult Result { get; }
    public LocationQueryResult LastResult { get; private set; }

    public Entity DisplayedEntity { get; private set; }

    public Int32 KeyValuesCount { get; }

    public LocationQueryVm(LocationQueryRequest request, LocationQueryResult result)
    {
        LastRequest = request;
        Result = result;

        var relation = Result.PrimaryEntities;

        if (relation?.Extent.Flavor.type == ExtentFlavorType.Block)
        {
            var table = relation.RelationEnd.Table;

            var keyKeysArray = request.KeyParams.Cast<String>().ToArray();

            KeyValuesCount = keyKeysArray.Length;

            var accountedIndexes = new HashSet<ObjectName>();

            SearchOptions = table.Indexes.Values
                .Where(i => i.IsSupported)
                .StartsWith(keyKeysArray)
                .Where(i => i.Columns.Length > keyKeysArray.Length)
                .Select(i => new SearchOptionVm(i, KeyValuesCount) { IsCurrent = request.Index == i.Name && result.SearchMode == QuerySearchMode.Seek })
                .ToArray();

            accountedIndexes.AddRange(SearchOptions.Select(i => i.Index.ObjectName));

            var unsupportedGroups = (
                from i in table.Indexes.Values
                where !i.IsSupported
                group i by i.UnsupportedReason into g
                select new UnsuitableIndexesVm(g.Key, from i in g select new SearchOptionVm(i, KeyValuesCount) { UnsupportedReason = g.Key })
            ).ToList();

            accountedIndexes.AddRange(from i in table.Indexes.Values where !i.IsSupported select i.ObjectName);

            var unsuitableReason = new CsdUnsupportedReason("Prefix mismatch", "Although supported on the table, you can't use the index to search within the subset you're looking at.", "");

            var unsuitableIndexes = (
                from i in table.Indexes.Values
                where !accountedIndexes.Contains(i.ObjectName)
                select new SearchOptionVm(i, KeyValuesCount) { UnsupportedReason = unsuitableReason }
            ).ToArray();

            if (unsuitableIndexes.Any())
            {
                unsupportedGroups.Insert(0, new UnsuitableIndexesVm(unsuitableReason, unsuitableIndexes));
            }

            UnsuitableIndexes = unsupportedGroups.ToArray();

            CurrentIndex = SearchOptions.FirstOrDefault(i => i.IsCurrent);

            ScanOption = new SearchOptionVm(SearchOptionType.Scan) { IsCurrent = result.SearchMode == QuerySearchMode.Scan };
        }

        Update(request, result);
    }

    public void Update(LocationQueryRequest request, LocationQueryResult result)
    {
        LastRequest = request;
        LastResult = result;

        if (LastResult.IsOk)
        {
            DisplayedEntity = LastResult.Entity;
        }

        if (CurrentIndex != null)
        {
            CurrentIndex.SetValidatedValues(result.ValidatedColumns);
        }
    }

    public CanLoadMoreStatus CanLoadMore()
    {
        var r = LastResult.PrimaryEntities;

        if (r == null) return CanLoadMoreStatus.Unavailable;

        if (r.Extent.Flavor.type != ExtentFlavorType.Block) return CanLoadMoreStatus.Unavailable;

        if (r.Extent.Limit > r.List.Length) return CanLoadMoreStatus.Complete;

        return CanLoadMoreStatus.Can;
    }

    public SearchOptionVm CurrentIndex { get; }

    public SearchOptionVm NoIndex { get; } = staticNoIndex;

    public SearchOptionVm ScanOption { get; }

    static SearchOptionVm staticNoIndex = new SearchOptionVm(SearchOptionType.NoOption)
    {
        IsCurrent = true
    };

    public SearchOptionVm[] SearchOptions { get; }

    public UnsuitableIndexesVm[] UnsuitableIndexes { get; set; }
}
