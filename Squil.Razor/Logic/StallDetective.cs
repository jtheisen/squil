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

public record StallInvestigationPublicResult(
    StallInvestigationResultType Type,
    Int32? SessionId = null,
    Int32 progress = 0,
    Int32? headBlockerSessionId = null
);

public record StallInvestigationResult(StallInvestigationResultType Type, Int32 CpuTime, Int32? HeadBlockerSessionId = null)
{
    public static implicit operator StallInvestigationResult(StallInvestigationResultType type)
        => new StallInvestigationResult(type, 0);
}

public class StallDetective : ObservableObject<StallDetective>
{
    private readonly String connectionString;
    private readonly SqlConnection connection;

    Int32? lastSessionId;
    StallInvestigationResult lastResult;
    public StallInvestigationPublicResult Result { get; private set; }

    public StallDetective(SqlConnection connection)
    {
        this.connection = connection
            ?? throw new Exception($"Unexpectedly having no connection on stall investigation");

        var cb = new SqlConnectionStringBuilder(connection.ConnectionString);
        cb.ConnectTimeout = 6;
        connectionString = cb.ConnectionString;

        Result = new StallInvestigationPublicResult(StallInvestigationResultType.Initial);
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
            var headBlockerSessionId = Result.headBlockerSessionId;

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

            if (headBlockerSessionId != result.HeadBlockerSessionId)
            {
                headBlockerSessionId = result.HeadBlockerSessionId;
                haveChange = true;
            }

            lastResult = result;
            Result = new StallInvestigationPublicResult(type, lastSessionId, progress, headBlockerSessionId);

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
        [XmlElement("can_view_server_state")]
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
        lastSessionId = null;

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

        lastSessionId = connection.ServerProcessId;

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

        Int32? headBlockerSessionId = null;

        if (blockingSessionId != 0)
        {
            var blockingReport = await GlobalBlockingReport.CreateAsync(db);

            headBlockerSessionId = blockingReport.GetHeadBlockerSessionId(connection.ServerProcessId);
        }

        return new StallInvestigationResult(
            blockingSessionId != 0 ? StallInvestigationResultType.Blocked : StallInvestigationResultType.Unblocked,
            request.CpuTime, headBlockerSessionId);
    }
}

public class GlobalBlockingReport
{
    static readonly String sql = @";WITH cteHead ( session_id,request_id,wait_type,wait_resource,last_wait_type,is_user_process,request_cpu_time
,request_logical_reads,request_reads,request_writes,wait_time,blocking_session_id,memory_usage
,session_cpu_time,session_reads,session_writes,session_logical_reads
,percent_complete,est_completion_time,request_start_time,request_status,command
,plan_handle,sql_handle,statement_start_offset,statement_end_offset,most_recent_sql_handle
,session_status,group_id,query_hash,query_plan_hash) 
AS ( SELECT sess.session_id, req.request_id, LEFT (ISNULL (req.wait_type, ''), 50) AS 'wait_type'
    , LEFT (ISNULL (req.wait_resource, ''), 40) AS 'wait_resource', LEFT (req.last_wait_type, 50) AS 'last_wait_type'
    , sess.is_user_process, req.cpu_time AS 'request_cpu_time', req.logical_reads AS 'request_logical_reads'
    , req.reads AS 'request_reads', req.writes AS 'request_writes', req.wait_time, req.blocking_session_id,sess.memory_usage
    , sess.cpu_time AS 'session_cpu_time', sess.reads AS 'session_reads', sess.writes AS 'session_writes', sess.logical_reads AS 'session_logical_reads'
    , CONVERT (decimal(5,2), req.percent_complete) AS 'percent_complete', req.estimated_completion_time AS 'est_completion_time'
    , req.start_time AS 'request_start_time', LEFT (req.status, 15) AS 'request_status', req.command
    , req.plan_handle, req.[sql_handle], req.statement_start_offset, req.statement_end_offset, conn.most_recent_sql_handle
    , LEFT (sess.status, 15) AS 'session_status', sess.group_id, req.query_hash, req.query_plan_hash
    FROM sys.dm_exec_sessions AS sess
    LEFT OUTER JOIN sys.dm_exec_requests AS req ON sess.session_id = req.session_id
    LEFT OUTER JOIN sys.dm_exec_connections AS conn on conn.session_id = sess.session_id 
    )
, cteBlockingHierarchy (head_blocker_session_id, session_id, blocking_session_id, wait_type, wait_duration_ms,
wait_resource, statement_start_offset, statement_end_offset, plan_handle, sql_handle, most_recent_sql_handle, [Level])
AS ( SELECT head.session_id AS head_blocker_session_id, head.session_id AS session_id, head.blocking_session_id
    , head.wait_type, head.wait_time, head.wait_resource, head.statement_start_offset, head.statement_end_offset
    , head.plan_handle, head.sql_handle, head.most_recent_sql_handle, 0 AS [Level]
    FROM cteHead AS head
    WHERE (head.blocking_session_id IS NULL OR head.blocking_session_id = 0)
    AND head.session_id IN (SELECT DISTINCT blocking_session_id FROM cteHead WHERE blocking_session_id != 0)
    UNION ALL
    SELECT h.head_blocker_session_id, blocked.session_id, blocked.blocking_session_id, blocked.wait_type,
    blocked.wait_time, blocked.wait_resource, h.statement_start_offset, h.statement_end_offset,
    h.plan_handle, h.sql_handle, h.most_recent_sql_handle, [Level] + 1
    FROM cteHead AS blocked
    INNER JOIN cteBlockingHierarchy AS h ON h.session_id = blocked.blocking_session_id and h.session_id!=blocked.session_id --avoid infinite recursion for latch type of blocking
    WHERE h.wait_type COLLATE Latin1_General_BIN NOT IN ('EXCHANGE', 'CXPACKET') or h.wait_type is null
    )
SELECT item.* --, item.text AS blocker_query_or_most_recent_query 
FROM cteBlockingHierarchy AS item -- name is ignored if the cross apply is taken in
--OUTER APPLY sys.dm_exec_sql_text (ISNULL ([sql_handle], most_recent_sql_handle)) AS item
for xml auto, binary base64, root('sessions')
";

    [XmlType("item")]
    public class Entry
    {
        [XmlAttribute("session_id")]
        public Int32 SessionId { get; set; }

        [XmlAttribute("head_blocker_session_id")]
        public Int32 HeadBlockerSessionId { get; set; }

        [XmlAttribute("blocking_session_id")]
        public Int32 BlockingSessionId { get; set; }

        [XmlAttribute("Level")]
        public Int32 Level { get; set; }

        //[XmlAttribute("blocker_query_or_most_recent_query")]
        //public String Query { get; set; }
    }

    Dictionary<Int32, Entry> entries;

    GlobalBlockingReport(Entry[] entries)
    {
        this.entries = entries.ToDictionary(e => e.SessionId, e => e);
    }

    public Int32? GetHeadBlockerSessionId(Int32 sessionId)
    {
        if (!entries.TryGetValue(sessionId, out var blockedSession)) return null;

        return blockedSession.HeadBlockerSessionId;
    }

    public static async Task<GlobalBlockingReport> CreateAsync(SqlConnection connection)
    {
        var xml = await connection.QueryXmlStringAsync(sql, dontWrap: true);

        var serializer = new XmlSerializer(typeof(Entry[]), new XmlRootAttribute("sessions"));

        var reader = new StringReader(xml);

        var sessions = (Entry[])serializer.Deserialize(reader);

        return new GlobalBlockingReport(sessions);
    }
}