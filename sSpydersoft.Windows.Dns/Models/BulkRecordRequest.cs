namespace spydersoft.windows.dns.Models
{
    public class BulkRecordRequest
    {
        public IEnumerable<DnsRecord> Records { get; set; } = new List<DnsRecord>();
    }
}
