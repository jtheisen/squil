using Squil.SchemaBuilding;
using System.Collections.Specialized;
using System.Reactive.Subjects;

namespace Squil;

public record UiQueryVmEvent;

public record UiQueryVmNavigateBackEvent : UiQueryVmEvent;

public record UiQueryVmNavigateToEvent(String Target) : UiQueryVmEvent;

public record UiQueryVmStartQueryEvent : UiQueryVmEvent;

public record UiQueryVmExceptionEvent(Exception Exception) : UiQueryVmEvent;

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

public enum DnsOperationType
{
    None,
    Default,
    Null,
    Special
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

public class LocationUiQueryVm : ObservableObject<LocationUiQueryVm>, IDisposable
{
    public UiQueryLocation Location { get; }

    public QueryUrlCreator UrlCreateor { get; }
    public UiQueryRunner Runner { get; }
    public AppSettings Settings { get; }

    Subject<UiQueryVmEvent> eventSink = new Subject<UiQueryVmEvent>();

    public IObservable<UiQueryVmEvent> Events => eventSink;

    public Int32 ListLimit { get; private set; }

    public UiQueryRequest Request { get; private set; }

    public UiQueryState Response => CurrentResponse;
    public UiQueryState CurrentResponse { get; private set; }

    public Boolean HaveResponse => CurrentResponse is not null;

    public Boolean IsQuerying => CurrentResponse?.IsRunning ?? false;

    public UiQueryResult CommittedResult { get; private set; }

    public UiQueryResult Result { get; private set; }

    public Boolean HaveSearchOptions { get; private set; }

    public Int32 ResultNumber { get; private set; }

    public Boolean InDebug { get; set; }

    public LiveSource CurrentSource { get; private set; }

    public Boolean ShowSchemaChangedException { get; private set; }

    NameValueCollection searchValues = new NameValueCollection();

    LifetimeLogger<LocationUiQueryVm> lifetimeLogger = new LifetimeLogger<LocationUiQueryVm>();

    Boolean isDisposed;

    public LocationUiQueryVm(AppSettings settings, UiQueryRunner runner, UiQueryLocation location)
    {
        Runner = runner;
        Location = location;
        Settings = settings;

        ListLimit = settings.InitialLimit;

        UrlCreateor = new QueryUrlCreator(location.Source);
    }

    UiQueryAccessMode GetAccessMode()
    {
        if (Changes is null) return UiQueryAccessMode.QueryOnly;

        if (AreSaving) return UiQueryAccessMode.Commit;

        return UiQueryAccessMode.Rollback;
    }

    public void SetSearchValues(NameValueCollection searchValues)
    {
        log.Debug("Seek values changed");

        this.searchValues = searchValues;
    }

    public async void StartQuery(Int32 attempt = 0)
    {
        log.Info($"Starting new query asynchronously");

        var accessMode = GetAccessMode();

        LocationQueryOperationType? operationType = null;

        if (Enum.TryParse<LocationQueryOperationType>(Location.RestParams["operation"], true, out var parsedOperationType))
        {
            operationType = parsedOperationType;
        }

        var request = new UiQueryRequest(Location, searchValues, Changes, accessMode, operationType);

        request.ListLimit = ListLimit;

        var source = CurrentSource = Runner.GetLiveSource(Location.Source);

        var ensureModelTask = source.EnsureModelAsync();

        if (source.State != LiveSourceState.Ready)
        {
            NotifyChange();
        }

        await ensureModelTask;

        if (isDisposed)
        {
            log.Info($"Vm disposed after model building completed, aborting");

            return;
        }

        var response = Runner.StartQuery(source, Location.Source, request);

        Update(request, response);

        if (response.IsCompleted)
        {
            log.Debug($"Query completed synchronously");
        }
        else
        {
            log.Debug($"Query is running asynchronously");
        }

        NotifyChange();

        await response.Wait();

        var resultOrNull = response.Result;

        if (response.Exception is not null)
        {
            eventSink.OnNext(new UiQueryVmExceptionEvent(response.Exception));
        }

        if (response.Exception is SchemaChangedException)
        {
            if (attempt == 0)
            {
                log.Info($"Got SchemaChangedException at attempt #{attempt}, re-running query");

                StartQuery(attempt + 1);
            }
            else
            {
                log.Info($"Got SchemaChangedException at attempt #{attempt}, giving up");

                ShowSchemaChangedException = true;

                NotifyChange();
            }
        }
        else if (CurrentResponse == response)
        {
            var hadCommit = response.IsCompletedSuccessfully && response.Result.HasCommitted;

            UpdateResult(request, response, resultOrNull, hadCommit);

            ShowSchemaChangedException = false;

            if (hadCommit)
            {
                if (request.Changes?.FirstOrDefault() is { Type: ChangeOperationType.Delete })
                {
                    eventSink.OnNext(new UiQueryVmNavigateBackEvent());
                }
                else if (request.Changes?.FirstOrDefault() is { Type: ChangeOperationType.Insert or ChangeOperationType.Update } insertEntry)
                {
                    var values = insertEntry.EntityKey.GetKeyColumnsAndValuesDictionary().ToMap();

                    eventSink.OnNext(new UiQueryVmNavigateToEvent(UrlCreateor.RenderEntityUrl(response.Table, values)));
                }
                else
                {
                    NotifyChange();
                }
            }
            else
            {
                if (request.Changes?.FirstOrDefault() is { Type: ChangeOperationType.Insert } insertEntry &&
                    Changes.FirstOrDefault() is { Type: ChangeOperationType.Insert } ownInsertEntry &&
                    !ownInsertEntry.IsKeyed && insertEntry.IsKeyed
                    )
                {
                    ownInsertEntry.EntityKey = insertEntry.EntityKey;
                }

                NotifyChange();
            }
        }
    }

    void InitSearchOptions()
    {
        var keyKeysArray = Location.KeyParams.Cast<String>().ToArray();

        var keyValuesCount = Location.KeyValuesCount;

        var response = Response;

        var table = Response.Table;

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
                select new UnsuitableIndexesVm(g.Key, from i in g select new SearchOptionVm(i, keyValuesCount) { UnsupportedReason = g.Key })
            );
        }

        {
            var invalidPrefixReason = new CsdUnsupportedReason("Prefix mismatch", "Although supported on the table, you can't use the index to search within the subset you're looking at.", "");

            AddExclusionGroup(new UnsuitableIndexesVm(
                invalidPrefixReason,
                from i in indexesToConsider.StartsWith(keyKeysArray, not: true)
                select new SearchOptionVm(i, keyValuesCount) { UnsupportedReason = invalidPrefixReason }
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
                select new SearchOptionVm(i, keyValuesCount) { UnsupportedReason = duplicationReason }
            ));
        }

        SearchOptions = (
            from i in table.Indexes.Values
            join itc in indexesToConsider on i equals itc into match
            where match.Any()
            select new SearchOptionVm(i, keyValuesCount) { IsCurrent = Location.Index == i.Name && response.SearchMode == QuerySearchMode.Seek }
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

    void Update(UiQueryRequest request, UiQueryState response)
    {
        Request = request;
        CurrentResponse = response;

        if (Response.ExtentFlavorType == ExtentFlavorType.BlockList && !HaveSearchOptions)
        {
            InitSearchOptions();

            HaveSearchOptions = true;
        }

        if (CurrentIndex != null)
        {
            CurrentIndex.SetValidatedValues(response.ValidatedColumns);
        }
    }

    void UpdateResult(UiQueryRequest request, UiQueryState response, UiQueryResult result, Boolean hasCommit)
    {
        log.Info($"UpdateResult for #{response.RequestNo}");

        if (result is not null)
        {
            Result = result;

            log.Info($"UpdateResult setting for #{response.RequestNo}");

            if (CommittedResult is null || hasCommit)
            {
                CommittedResult = result;
            }

            if (request.OperationType == LocationQueryOperationType.Insert)
            {
                if (editType is null)
                {
                    var insertEntity = result.PrimaryEntities.List.Single($"Result unexpectedly missing singular entity on insert");

                    foreach (String kc in request.Location.KeyParams)
                    {
                        insertEntity.SetEditValue(kc, request.Location.KeyParams[kc]);
                    }

                    StartInsert();
                }
            }

            if (response.IsChangeOk && AreSaving)
            {
                editType = null;
            }

            AreSaving = false;
        }

        ++ResultNumber;
    }

    public UiQueryCanLoadMoreStatus CanLoadMore()
    {
        if (haveChanges) return UiQueryCanLoadMoreStatus.Unavailable;

        if (CurrentResponse.Task is null) return UiQueryCanLoadMoreStatus.Unavailable;

        if (!CurrentResponse.Task.IsCompletedSuccessfully) return UiQueryCanLoadMoreStatus.Unavailable;

        var r = CurrentResponse.Task.Result.PrimaryEntities;

        if (r == null) return UiQueryCanLoadMoreStatus.Unavailable;

        if (r.Extent.Flavor.type != ExtentFlavorType.Block) return UiQueryCanLoadMoreStatus.Unavailable;

        if (r.Extent.Limit > r.List.Length) return UiQueryCanLoadMoreStatus.Complete;

        return UiQueryCanLoadMoreStatus.Can;
    }

    public void LoadMore()
    {
        ListLimit = Settings.LoadMoreLimit;

        eventSink.OnNext(new UiQueryVmStartQueryEvent());
    }

    public SearchOptionVm CurrentIndex { get; private set; }

    public SearchOptionVm NoIndex { get; } = staticNoIndex;

    public SearchOptionVm ScanOption { get; private set; }

    public String NoScanOptionReason { get; private set; }

    static SearchOptionVm staticNoIndex = new SearchOptionVm(SearchOptionType.NoOption)
    {
        IsCurrent = true
    };

    public SearchOptionVm[] SearchOptions { get; private set; }

    public UnsuitableIndexesVm[] UnsuitableIndexes { get; set; }

    #region Editing

    ChangeOperationType? editType;

    Boolean haveChanges = false;

    List<ChangeEntry> changes;

    public ChangeOperationType? EditType => editType;

    public Boolean CanEdit
    {
        get
        {
            if (CurrentResponse is null) return false;

            if (!CurrentResponse.IsOk) return false;

            if (!CurrentResponse.Table.AccesssPermissions.HasFlag(CsdAccesssPermissions.Update)) return false;

            switch (CurrentResponse.QueryType)
            {
                case UiQueryType.Row:
                case UiQueryType.Column:
                    return true;
                default:
                    return false;
            }
        }
    }

    public Boolean CanDelete =>
        CurrentResponse is not null &&
        CurrentResponse.IsOk &&
        CurrentResponse.QueryType == UiQueryType.Row &&
        CurrentResponse.Table.AccesssPermissions.HasFlag(CsdAccesssPermissions.Delete)
        ;

    public Boolean CanInsert
    {
        get
        {
            if (CurrentResponse is null) return false;

            if (!CurrentResponse.IsOk) return false;

            if (!CurrentResponse.Table.AccesssPermissions.HasFlag(CsdAccesssPermissions.Insert)) return false;

            switch (CurrentResponse.QueryType)
            {
                case UiQueryType.Table:
                case UiQueryType.TableSlice:
                    return true;
                default:
                    return false;
            }
        }
    }

    public Boolean AreSaving { get; private set; }

    public ChangeEntry[] Changes => changes?.ToArray();

    public Boolean AreInEdit => editType is not null;
    public Boolean AreInUpdate => editType == ChangeOperationType.Update;
    public Boolean AreInDelete => editType == ChangeOperationType.Delete;
    public Boolean AreInInsert => editType == ChangeOperationType.Insert;

    public Boolean AreInUpdateOrInsert => editType == ChangeOperationType.Update || editType == ChangeOperationType.Insert;

    public void StartUpdate() => StartEdit(ChangeOperationType.Update, InitChange);
    public void StartDelete() => StartEdit(ChangeOperationType.Delete, InitDelete);
    public void StartInsert() => StartEdit(ChangeOperationType.Insert, InitChange);

    public void StartEdit(ChangeOperationType state, Action init = null)
    {
        if (AreInEdit) throw new Exception($"Edit already started");

        if (!CurrentResponse.IsCompletedSuccessfully) throw new Exception($"No result");

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

        StartQueryAfterEdit();
    }

    void InitChange()
    {
        var entity = Result.PrimaryEntities.List.Single($"Result has no singular primary entity");

        var change = GetChangeEntry(entity, editType == ChangeOperationType.Insert);

        SetChange(change);
    }

    public void CancelEdit()
    {
        if (changes?.FirstOrDefault() is { Type: ChangeOperationType.Insert })
        {
            eventSink.OnNext(new UiQueryVmNavigateBackEvent());
        }
        else
        {
            editType = null;

            changes = null;

            StartQueryAfterEdit();
        }
    }

    public void RunDry()
    {
        StartQueryAfterEdit();
    }

    public void Save()
    {
        AreSaving = true;

        StartQueryAfterEdit();
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
            return ChangeEntry.Insert(entity.Table.Name, entity.EditValues ?? new Dictionary<String, String>());
        }
        else if (asInsert)
        {
            return ChangeEntry.Insert(entity.GetEntityKey(), entity.EditValues ?? new Dictionary<String, String>());
        }
        else
        {
            return ChangeEntry.Update(entity.GetEntityKey(), entity.EditValues ?? new Dictionary<String, String>());
        }
    }

    public void NoteEditTouch(Entity entity)
    {
        if (entity.EditState == EntityEditState.Validated)
        {
            entity.EditState = EntityEditState.Modified;
        }
    }

    public void AddChange(Entity entity)
    {
        if (entity.EditState == EntityEditState.Original) return;

        var change = changes.Single("Unexpectedly no singular change entry");

        change.EditValues = entity.EditValues;

        haveChanges = true;

        NotifyChange();
    }

    public Boolean ShouldRedirectAfterInsert(out String url)
    {
        var request = Request;
        var response = CurrentResponse;

        url = null;

        if (request.OperationType == LocationQueryOperationType.Insert && response.IsChangeOk && request.AccessMode == UiQueryAccessMode.Commit)
        {
            var table = response.Table;
            var change = Changes.Single();
            var keyColumnsAndValues = change.EntityKey.KeyColumnsAndValues.ToMap();

            url = UrlCreateor.RenderEntityUrl(table, keyColumnsAndValues);

            return true;
        }

        return false;
    }

    void StartQueryAfterEdit()
    {
        eventSink.OnNext(new UiQueryVmStartQueryEvent());
    }

    public DnsOperationType GetDnsOperationType(Entity entity, String name)
    {
        var column = entity.Table.Columns[name];

        var special = column.Type.SpecialValueOrNull;

        entity.ColumnValues.TryGetValue(name, out var originalValue);

        String editValue = null;

        var isEdited = entity.EditValues?.TryGetValue(name, out editValue) ?? false;

        if (isEdited)
        {
            if (editValue is null && special is not null)
            {
                return DnsOperationType.Special;
            }
            else
            {
                return DnsOperationType.Default;
            }
        }
        else
        {
            if (originalValue is null && special is not null)
            {
                return DnsOperationType.Special;
            }
            else if (column.IsNullable)
            {
                return DnsOperationType.Null;
            }
            else if (originalValue != special && special is not null)
            {
                return DnsOperationType.Special;
            }
            else
            {
                return DnsOperationType.None;
            }
        }
    }

    public void ApplyDnsOperation(Entity entity, String name, DnsOperationType type)
    {
        switch (type)
        {
            case DnsOperationType.Default:
                entity.ClearEditValue(name);
                
                break;

            case DnsOperationType.Null:
                entity.SetEditValue(name, null);
                
                break;

            case DnsOperationType.Special:
                var column = entity.Table.Columns[name];

                var special = column.Type.SpecialValueOrNull;

                entity.SetEditValue(name, special);

                break;

            default:
                throw new Exception($"Invalid dns operation type {type}");
        }

        AddChange(entity);
    }

    #endregion

    public void Dispose()
    {
        isDisposed = true;

        lifetimeLogger.Dispose();
    }
}
