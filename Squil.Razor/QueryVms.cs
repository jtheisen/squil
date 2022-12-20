using Azure.Core;
using Azure;
using Microsoft.AspNetCore.Components;
using Squil.SchemaBuilding;

namespace Squil;

public record UnsuitableIndexesVm(CsdUnsupportedReason Reason, SearchOptionVm[] Indexes)
{
    public UnsuitableIndexesVm(CsdUnsupportedReason reason, IEnumerable<SearchOptionVm> indexes)
        : this(reason, indexes.ToArray())
    {
    }
}

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

public class LocationQueryVm : ObservableObject<LocationQueryVm>
{
    public LocationQueryRequest LastRequest { get; private set; }
    
    public LocationQueryResponse Response { get; }
    public LocationQueryResponse LastResponse { get; private set; }

    public LocationQueryResult Result { get; private set; }

    public Int32 ResultNumber { get; private set; }

    public Int32 KeyValuesCount { get; }

    public Boolean InDebug { get; set; }

    public QueryUrlCreator UrlCreateor { get; }

    public LocationQueryVm(LocationQueryRequest request, LocationQueryResponse response)
    {
        LastRequest = request;
        Response = response;

        UrlCreateor = new QueryUrlCreator(request.Source);

        if (response.ExtentFlavorType == ExtentFlavorType.BlockList)
        {
            var table = response.Table;

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
                select new SearchOptionVm(i, KeyValuesCount) { IsCurrent = request.Index == i.Name && response.SearchMode == QuerySearchMode.Seek }
            ).ToArray();

            if (Response.MayScan)
            {
                ScanOption = new SearchOptionVm(SearchOptionType.Scan) { IsCurrent = response.SearchMode == QuerySearchMode.Scan };
            }
            else
            {
                NoScanOptionReason = "The scanning search option is disabled for large tables.";
            }

            UnsuitableIndexes = exclusionGroups.ToArray();

            CurrentIndex = SearchOptions.FirstOrDefault(i => i.IsCurrent);
        }

        Update(request, response);
    }

    public void Update(LocationQueryRequest request, LocationQueryResponse response)
    {
        LastRequest = request;
        LastResponse = response;

        if (CurrentIndex != null)
        {
            CurrentIndex.SetValidatedValues(response.ValidatedColumns);
        }

        // FIXME: shouldn't this be in UpdateResult?
        if (response.IsOk)
        {
            IsQueryRequired = false;

            if (request.OperationType == LocationQueryOperationType.Insert)
            {
                editType = EditType.Insert;

                if (changes == null)
                {
                    SetChange(ChangeEntry.Insert(response.Table.Name, new Dictionary<String, String>()));
                }
            }

            if (response.IsChangeOk && AreSaving)
            {
                editType = EditType.NotEditing;
            }
        }

        AreSaving = false;
    }

    public void UpdateResult(LocationQueryResult result)
    {
        if (result != null)
        {
            Result = result;
        }

        ++ResultNumber;
    }

    public CanLoadMoreStatus CanLoadMore()
    {
        if (haveChanges) return CanLoadMoreStatus.Unavailable;

        if (LastResponse.Task == null) return CanLoadMoreStatus.Unavailable;

        if (!LastResponse.Task.IsCompletedSuccessfully) return CanLoadMoreStatus.Unavailable;

        var r = LastResponse.Task.Result.PrimaryEntities;

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

    #region Editing

    public enum EditType
    {
        NotEditing,
        Update,
        Insert,
        Delete
    }

    EditType editType;

    Boolean haveChanges = false;

    List<ChangeEntry> changes;

    public Boolean CanEdit
    {
        get
        {
            if (!LastResponse.IsOk) return false;

            switch (LastResponse.QueryType)
            {
                case QueryControllerQueryType.Row:
                case QueryControllerQueryType.Column:
                    return true;
                default:
                    return false;
            }
        }
    }

    public Boolean CanDelete => LastResponse.IsOk && LastResponse.QueryType == QueryControllerQueryType.Row;

    public Boolean CanInsert
    {
        get
        {
            if (!LastResponse.IsOk) return false;

            switch (LastResponse.QueryType)
            {
                case QueryControllerQueryType.Table:
                case QueryControllerQueryType.TableSlice:
                    return true;
                default:
                    return false;
            }
        }
    }

    public Boolean IsQueryRequired { get; private set; }

    public Boolean AreSaving { get; private set; }

    public ChangeEntry[] Changes => changes?.ToArray();

    public Boolean AreInEdit => editType != EditType.NotEditing;
    public Boolean AreInUpdate => editType == EditType.Update;
    public Boolean AreInDelete => editType == EditType.Delete;
    public Boolean AreInInsert => editType == EditType.Insert;

    public Boolean AreInUpdateOrInsert => editType == EditType.Update || editType == EditType.Insert;

    public void StartUpdate() => StartEdit(EditType.Update);
    public void StartDelete() => StartEdit(EditType.Delete, InitDelete);
    public void StartInsert() => StartEdit(EditType.Insert);

    public void StartEdit(EditType state, Action init = null)
    {
        if (AreInEdit) throw new Exception($"Edit already started");

        if (!LastResponse.IsResultOk) throw new Exception($"No result");

        editType = state;

        init?.Invoke();

        NotifyChange();
    }

    void InitDelete()
    {
        var entity = Result.PrimaryEntities.List.FirstOrDefault();

        if (entity == null) throw new Exception($"Result has no primary entity");

        var key = entity.GetEntityKey();

        SetChange(ChangeEntry.Delete(key));

        NotifyQueryRequired();
    }

    public void CancelEdit()
    {
        editType = EditType.NotEditing;

        changes = null;

        NotifyQueryRequired();
    }

    public void Save()
    {
        AreSaving = true;

        NotifyQueryRequired();
    }

    void SetChange(ChangeEntry change)
    {
        changes = new List<ChangeEntry>
        {
            change
        };
    }

    ChangeEntry GetChangeEntry(Entity entity, Boolean asInsert)
    {
        if (entity.IsUnkeyed)
        {
            return ChangeEntry.Insert(entity.Table.Name, entity.EditValues);
        }
        else if (asInsert)
        {
            return ChangeEntry.Insert(entity.GetEntityKey(), entity.EditValues);
        }
        else
        {
            return ChangeEntry.Update(entity.GetEntityKey(), entity.EditValues);
        }
    }

    public void AddChange(Entity entity)
    {
        if (entity.EditState == EntityEditState.Original) return;

        var change = GetChangeEntry(entity, editType == EditType.Insert);

        SetChange(change);

        entity.EditState = EntityEditState.Closed;

        haveChanges = true;

        NotifyQueryRequired();
    }

    public Boolean ShouldRedirectAfterInsert(out String url)
    {
        var request = LastRequest;
        var response = LastResponse;

        url = null;

        if (request.OperationType == LocationQueryOperationType.Insert && response.IsChangeOk && request.AccessMode == LocationQueryAccessMode.Commit)
        {
            var table = response.Table;
            var change = Changes.Single();
            var keyColumnsAndValues = change.EntityKey.KeyColumnsAndValues.ToMap();

            url = UrlCreateor.RenderEntityUrl(table, keyColumnsAndValues);

            return true;
        }

        return false;
    }

    void NotifyQueryRequired()
    {
        IsQueryRequired = true;

        NotifyChange();
    }

    #endregion
}
