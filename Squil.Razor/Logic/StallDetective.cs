using Microsoft.Data.SqlClient;
using System.Xml.Serialization;

namespace Squil;

public enum StallInvestigationResultType
{
    Initial,
    CantConnect,
    NoPermission,
    NoInformation,
    Blocked,
    Unblocked
}

public record StallInvestigationPublicResult(StallInvestigationResultType Type, Int32 progress);

public record StallInvestigationResult(StallInvestigationResultType Type, Int32 CpuTime)
{
    public static implicit operator StallInvestigationResult(StallInvestigationResultType type)
        => new StallInvestigationResult(type, 0);
}

public class StallDetective : ObservableObject<StallDetective>
{
    static Logger log = LogManager.GetCurrentClassLogger();

    private readonly String connectionString;
    private readonly SqlConnection connection;

    StallInvestigationResult lastResult;
    public StallInvestigationPublicResult Result { get; private set; }

    public StallDetective(SqlConnection connection)
    {
        this.connection = connection
            ?? throw new Exception($"Unexpectedly having no connection on stall investigation");

        var cb = new SqlConnectionStringBuilder(connection.ConnectionString);
        cb.ConnectTimeout = 6;
        connectionString = cb.ConnectionString;

        Result = new StallInvestigationPublicResult(StallInvestigationResultType.Initial, 0);
    }

    public async Task Investigate()
    {
        var ct = StaticServiceStack.Get<CancellationToken>();

        while (!ct.IsCancellationRequested)
        {
            var result = await CheckSession();

            var haveChange = false;

            var type = Result.Type;
            var progress = Result.progress;

            if (lastResult?.CpuTime < result.CpuTime)
            {
                ++progress;
                haveChange = true;
            }

            if (type != result.Type)
            {
                type = result.Type;
                haveChange = true;
            }

            lastResult = result;
            Result = new StallInvestigationPublicResult(type, progress);

            if (haveChange)
            {
                NotifyChange();
            }

            if (type == StallInvestigationResultType.NoPermission)
            {
                log.Info($"Terminating stall investigation after realizing we have no permission to get further information");

                return;
            }

            await Task.Delay(1000);
        }

        log.Info($"Terminating stall investigation after cancellation");
    }

    [XmlRoot("investigation_root")]
    public class InvestigationRoot
    {
        [XmlAttribute("can_view_server_state")]
        public Boolean CanViewServerState { get; set; }

        [XmlArray("requests")]
        public DmExecRequest[] Requests { get; set; }
    }

    [XmlType("r")]
    public class DmExecRequest
    {
        [XmlAttribute("session_id")]
        public Int16 SessionId { get; set; }

        [XmlAttribute("request_id")]
        public Int32 RequestId { get; set; }

        [XmlAttribute("blocking_session_id")]
        public Int16 BlockingSessionId { get; set; }

        [XmlAttribute("cpu_time")]
        public Int32 CpuTime { get; set; }

        [XmlAttribute("reads")]
        public Int64 Reads { get; set; }

        [XmlAttribute("logical_reads")]
        public Int64 LogicalReads { get; set; }
    }

    async Task<StallInvestigationResult> CheckSession()
    {
        using var db = new SqlConnection(connectionString);

        try
        {
            await db.OpenAsync();
        }
        catch (Exception ex)
        {
            log.Info(ex, "Can't connect");

            return StallInvestigationResultType.CantConnect;
        }

        var sql = $@"
select (
	select session_id, request_id, blocking_session_id, cpu_time, reads, logical_reads
	from sys.dm_exec_requests r
    where session_id = {connection.ServerProcessId}
	for xml auto, type
) requests, (
    select
    count(*)
    from fn_my_permissions(NULL, 'SERVER')
    where permission_name = 'VIEW SERVER STATE'
) can_view_server_state
for xml path('investigation_root')
";

        var investigationRoot = await db.QueryAndParseXmlAsync<InvestigationRoot>(sql);

        var request = investigationRoot.Requests?.SingleOrDefault("Unexpectedly got multiple requests");

        if (request == null)
        {
            if (investigationRoot.CanViewServerState)
            {
                return StallInvestigationResultType.NoInformation;
            }
            else
            {
                return StallInvestigationResultType.NoPermission;
            }
        }

        var blockingSessionId = request?.BlockingSessionId ?? 0;

        log.Info($"CheckIsUnblocked: spid={connection.ServerProcessId} has blockingSessionId={blockingSessionId}");

        return new StallInvestigationResult(
            blockingSessionId != 0 ? StallInvestigationResultType.Blocked : StallInvestigationResultType.Unblocked,
            request.CpuTime);
    }
}
