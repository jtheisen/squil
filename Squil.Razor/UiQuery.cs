using Humanizer;
using System.Collections.Specialized;
using TaskLedgering;
using static Squil.StaticSqlAliases;

namespace Squil;

public enum UiQueryType
{
    Root,
    Row,
    Table,
    TableSlice,
    Column
}

public enum UiQueryAccessMode
{
    QueryOnly,
    Rollback,
    Commit
}

public enum UiQueryCanLoadMoreStatus
{
    Unavailable,
    Can,
    Complete
}

[DebuggerDisplay("{ToString()}")]
public class UiQueryLocation
{
    public String Source { get; init; }
    public String Schema { get; init; }
    public String Table { get; init; }
    public String Index { get; init; }
    public String Column { get; init; }

    public NameValueCollection KeyParams { get; init; }
    public NameValueCollection RestParams { get; init; }

    public Int32 KeyValuesCount { get; init; }

    public QuerySearchMode? SearchMode { get; init; }

    public String BackRelation { get; init; }

    public UiQueryLocation()
    {
        KeyParams = new NameValueCollection();
        RestParams = new NameValueCollection();
    }

    public UiQueryLocation(
        String[] segments,
        NameValueCollection queryParams
    )
    {
        String Get(Int32 i)
        {
            var segment = segments.GetOrDefault(i)?.TrimEnd('/');

            return segment != UrlRenderer.BlazorDefeatingDummySegment ? segment : null;
        }

        Debug.Assert(Get(0) == "ui");

        Source = Get(1);

        var section = Get(2);

        switch (section)
        {
            case "views":
            case "tables":
                Schema = Get(3);
                Table = Get(4);
                Index = Get(5);
                Column = Get(6);
                break;
            case "indexes":
                Schema = Get(3);
                Index = Get(4);
                Column = Get(5);
                break;
        }

        if (Enum.TryParse<QuerySearchMode>(queryParams["search"], true, out var searchMode))
        {
            SearchMode = searchMode;
        }

        (var keyParams, var restParams) = SplitParams(queryParams);

        KeyParams = keyParams;
        RestParams = restParams;

        var keyKeysArray = KeyParams.Cast<String>().ToArray();

        KeyValuesCount = keyKeysArray.Length;

        BackRelation = queryParams["from"];
    }

    static (NameValueCollection keyParams, NameValueCollection restParams) SplitParams(NameValueCollection queryParams)
    {
        var groups =
            from key in queryParams.AllKeys
            group key by key?.StartsWith('$') ?? false into g
            select g;

        var keyParams = (
            from g in groups.Where(g => g.Key == true)
            from key in g
            select (key[1..], queryParams[key])
        ).ToMap().ToNameValueCollection();

        var restParams = (
            from g in groups.Where(g => g.Key == false)
            from key in g
            select (key, queryParams[key])
        ).ToMap().ToNameValueCollection();

        return (keyParams, restParams);
    }

    public override String ToString()
    {
        var writer = new StringWriter();

        var tn = 6;

        writer.Write($"{Source.Truncate(tn)}/{Schema.Truncate(tn)}/{Table.Truncate(tn)}");

        if (Index is not null) writer.Write($"/{Index.Truncate(tn)}");
        if (Column is not null) writer.Write($"/{Column.Truncate(tn)}");

        writer.Write(RestParams.ToTruncatedString('{', '}'));

        return writer.ToString();
    }
}

[DebuggerDisplay("{ToString()}")]
public class UiQueryRequest
{
    static Int32 staticRequestCount = 0;

    Int32 requestNo = ++staticRequestCount;

    public Int32 RequestNo => requestNo;

    public UiQueryLocation Location { get; set; }

    public ChangeEntry[] Changes { get; }
    public UiQueryAccessMode AccessMode { get; }
    public LocationQueryOperationType? OperationType { get; }

    public Int32 ListLimit { get; set; }

    public NameValueCollection SearchValues { get; }

    public Boolean IsIdentitizingPending => OperationType == LocationQueryOperationType.Insert && Changes == null;

    public UiQueryRequest(
        UiQueryLocation location,
        NameValueCollection searchValues,
        ChangeEntry[] changes = null,
        UiQueryAccessMode accessMode = default,
        LocationQueryOperationType? operationType = null
    )
    {
        Location = location;

        Changes = changes?.Select(c => c.Clone()).ToArray();
        AccessMode = accessMode;
        OperationType = operationType;

        SearchValues = searchValues;
    }

    public override String ToString()
    {
        var writer = new StringWriter();

        var cs = Changes?.Length > 0 ? "*" : "";
        var ams = AccessMode.ToString().ToLower().FirstOrDefault();
        var os = OperationType?.Apply(ot => ot.ToString().FirstOrDefault()) ?? '?';

        var ss = SearchValues.ToTruncatedString();

        return $"request #{requestNo} {ams}{cs}{os}{ss} at {Location}";
    }
}

[DebuggerDisplay("{ToString()}")]
public class UiQueryState
{
    public Int32 RequestNo { get; set; }

    public UiQueryType QueryType { get; set; }
    public QuerySearchMode? SearchMode { get; set; }
    public ExtentFlavorType ExtentFlavorType { get; set; }
    public Boolean MayScan { get; set; }
    public String RootUrl { get; set; }
    public String RootName { get; set; }
    public CMTable Table { get; set; }
    public CMIndexlike Index { get; set; }
    public CMRelationEnd PrincipalRelation { get; set; }
    public Boolean HaveValidationIssues { get; set; }
    public ValidationResult[] ValidatedColumns { get; set; }
    public String PrimaryIdPredicateSql { get; set; }

    public Extent Extent { get; set; }
    public LiveSource Source { get; set; }

    public Task<UiQueryResult> Task { get; set; }

    public TaskLedger Ledger { get; set; }
    public Exception ChangeException { get; set; }
    public Exception Exception { get; set; }

    public Boolean IsChangeOk => ChangeException == null;

    public Boolean IsOk => !HaveValidationIssues && Exception == null;

    public Boolean IsRunning => Task is not null && !Task.IsCompleted;
    public Boolean IsCompleted => Task is not null && Task.IsCompleted;

    public Boolean IsCompletedSuccessfully => Task?.IsCompletedSuccessfully ?? false;

    public Boolean IsCanceled => Exception is OperationCanceledException;

    public UiQueryResult Result => Task?.IsCompletedSuccessfully == true ? Task.Result : null;

    public async Task Wait()
    {
        if (Task is null) return;

        try
        {
            await Task;
        }
        catch (Exception)
        {
        }
    }

    public override String ToString()
    {
        return $"response #{RequestNo} {ToStringInner()}";
    }

    String ToStringInner()
    {
        if (IsCanceled) return "is canceled";

        if (Exception != null) return $"has exception: {Exception.Message}";

        if (HaveValidationIssues) return $"has validation issues";

        if (Task == null) return "is not started";

        if (!Task.IsCompleted) return $"has status {Task.Status}";

        if (IsChangeOk) return "is ok";

        return $"has change exception: {ChangeException.Message}";
    }

    public String GetPrimaryIdPredicateSql(String alias)
    {
        var aliasPrefix = String.IsNullOrWhiteSpace(alias) ? "" : alias.EscapeNamePart() + ".";

        return PrimaryIdPredicateSql?.Replace("\ue000", aliasPrefix);
    }
}

[DebuggerDisplay("{ToString()}")]
public class UiQueryResult
{
    public Int32 RequestNo { get; }

    public Entity Entity { get; }
    public RelatedEntities PrimaryEntities { get; }
    public RelatedEntities PrincipalEntities { get; }
    public Boolean HasCommitted { get; set; }

    public UiQueryResult(Int32 requestNo, Entity entity)
    {
        RequestNo = requestNo;
        Entity = entity;
        PrimaryEntities = entity.Related.GetRelatedEntitiesOrNull(PrimariesRelationAlias);
        PrincipalEntities = entity.Related.GetRelatedEntitiesOrNull(PrincipalRelationAlias);
    }

    public override String ToString()
    {
        var items = new[] { "entity".If(Entity != null), "primaries".If(PrimaryEntities != null), "principals".If(PrincipalEntities != null) };

        return $"result with {String.Join(", ", items.Where(i => i != null))}";
    }
}
