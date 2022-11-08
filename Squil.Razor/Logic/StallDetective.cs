using Microsoft.Data.SqlClient;
using Squil.SchemaBuilding;
using System.Data.Common;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Squil;

public class StallProbe
{
    public String Name { get; set; }

    public Task<StallProbeResult> Task { get; set; }

    public Func<Task<StallProbeResult>> CreateProbe { get; set; }

    public async Task Run()
    {
        Task = CreateProbe();

        await Task;
    }
}

public class StallProbeResult
{
    public Boolean IsStallReason { get; set; }

    public String Message { get; set; }
}

public class StallDetective : ObservableObject
{
    static Logger log = LogManager.GetCurrentClassLogger();

    private readonly String connectionString;
    private readonly SqlConnection connection;

    public List<StallProbe> Probes { get; }

    public StallDetective(SqlConnection connection)
    {
        this.connection = connection
            ?? throw new Exception($"Unexpectedly having no connection on stall investigation");
        connectionString = connection.ConnectionString;

        Probes = new List<StallProbe>();

        AddProbe("Data source reachable?", CheckIsServerReachable);

        AddProbe("Query blocked?", CheckIsUnblocked);
    }

    public async Task Investigate()
    {
        foreach (var probe in Probes)
        {
            var task = probe.Run();

            NotifyChange();

            await task;
        }

        NotifyChange();
    }

    void AddProbe(String name, Func<Task<StallProbeResult>> createProbe)
    {
        Probes.Add(new StallProbe { Name = name, CreateProbe = createProbe });
    }

    async Task<StallProbeResult> CheckIsServerReachable()
    {
        using var db = new SqlConnection(connectionString);

        await db.OpenAsync();

        var c = db.CreateSqlCommandFromSql("select @@spid");

        try
        {
            await c.ExecuteScalarAsync();

            return NoReason("Data source is available");
        }
        catch (DbException)
        {
            return IsReason("Can't reach data source");
        }
    }

    [XmlRoot("investigation_root")]
    public class InvestigationRoot
    {
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

    async Task<StallProbeResult> CheckIsUnblocked()
    {
        using var db = new SqlConnection(connectionString);

        await db.OpenAsync();

        var sql = $@"
select (
	select session_id, request_id, blocking_session_id, cpu_time, reads, logical_reads
	from sys.dm_exec_requests r
    where session_id = {connection.ServerProcessId}
	for xml auto, type
) requests for xml path('investigation_root')
";

        var investigationRoot = await db.QueryAndParseXmlAsync<InvestigationRoot>(sql);

        var request = investigationRoot.Requests.SingleOrDefault("Unexpectedly got multiple requests");

        var blockingSessionId = request?.BlockingSessionId;

        log.Info($"CheckIsUnblocked: spid={connection.ServerProcessId} has blockingSessionId={blockingSessionId}");

        return (blockingSessionId ?? 0) != 0 ? IsReason("Query is blocked") : NoReason("Query is not blocked");
    }

    public static StallProbeResult NoReason(String message)
        => new StallProbeResult { IsStallReason = false, Message = message };

    public static StallProbeResult IsReason(String message)
        => new StallProbeResult { IsStallReason = true, Message = message };
}
