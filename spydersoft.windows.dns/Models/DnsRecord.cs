namespace spydersoft.windows.dns.Models
{
    public class DnsRecord
    {
        public DnsRecord(string zoneName, string hostName, DnsRecordType recordType, string data)
        {
            ZoneName = zoneName;
            HostName = hostName;
            RecordType = recordType;
            Data = data;
        }

        public string ZoneName { get; set; }

        public string HostName { get; set; }

        public DnsRecordType RecordType { get; set; }

        public string Data { get; set; }

    }
}
