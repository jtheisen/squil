// See https://aka.ms/new-console-template for more information
using Squil;
using System.Xml.Serialization;


var xml = "<investigation_root><can_view_server_state>1</can_view_server_state></investigation_root>";

var parsed = SqlConnectionExtensions.Parse<InvestigationRoot>(xml);

Console.WriteLine(parsed.CanViewServerState);

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
