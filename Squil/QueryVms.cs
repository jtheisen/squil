﻿using Squil.SchemaBuilding;

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

            var indexesToConsider = new HashSet<CMIndexlike>(table.Indexes.Values);

            var exclusionGroups = new List<UnsuitableIndexesVm>();

            void AddExclusionGroups(IEnumerable<UnsuitableIndexesVm> groups)
            {
                exclusionGroups.AddRange(from g in groups where g.Indexes.Any() select g);

                foreach (var index in from g in groups from i in g.Indexes select i.Index)
                {
                    indexesToConsider.Remove(index);
                }
            }

            void AddExclusionGroup(UnsuitableIndexesVm group) => AddExclusionGroups(group.ToSingleton());

            {
                AddExclusionGroups(
                    from i in indexesToConsider
                    where !i.IsSupported
                    group i by i.UnsupportedReason into g
                    select new UnsuitableIndexesVm(g.Key, from i in g select new SearchOptionVm(i, KeyValuesCount) { UnsupportedReason = g.Key })
                );
            }

            {
                var invalidPrefixReason = new CsdUnsupportedReason("Prefix mismatch", "Although supported on the table, you can't use the index to search within the subset you're looking at.", "");

                AddExclusionGroup(new UnsuitableIndexesVm(
                    invalidPrefixReason,
                    from i in indexesToConsider.StartsWith(keyKeysArray, not: true)
                    select new SearchOptionVm(i, KeyValuesCount) { UnsupportedReason = invalidPrefixReason }
                ));
            }

            {
                var duplicationReason = new CsdUnsupportedReason("Duplicate", "Although supported, the index's order is already covered by another one.", "");

                AddExclusionGroup(new UnsuitableIndexesVm(
                    duplicationReason,
                    from i in table.Indexes.Values
                    group i by i.SerializedColumnNames into g
                    where g.Count() >= 2
                    from i in g.Skip(1)
                    select new SearchOptionVm(i, KeyValuesCount) { UnsupportedReason = duplicationReason }
                ));
            }

            SearchOptions = (
                from i in table.Indexes.Values
                join itc in indexesToConsider on i equals itc into match
                where match.Any()
                select new SearchOptionVm(i, KeyValuesCount) { IsCurrent = request.Index == i.Name && result.SearchMode == QuerySearchMode.Seek }
            ).ToArray();

            if (Result.MayScan)
            {
                ScanOption = new SearchOptionVm(SearchOptionType.Scan) { IsCurrent = result.SearchMode == QuerySearchMode.Scan };
            }
            else
            {
                NoScanOptionReason = "The scanning search option is disabled for large tables.";
            }

            UnsuitableIndexes = exclusionGroups.ToArray();

            CurrentIndex = SearchOptions.FirstOrDefault(i => i.IsCurrent);
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

    public String NoScanOptionReason { get; }

    static SearchOptionVm staticNoIndex = new SearchOptionVm(SearchOptionType.NoOption)
    {
        IsCurrent = true
    };

    public SearchOptionVm[] SearchOptions { get; }

    public UnsuitableIndexesVm[] UnsuitableIndexes { get; set; }
}
