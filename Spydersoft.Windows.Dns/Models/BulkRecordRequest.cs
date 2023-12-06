namespace Spydersoft.Windows.Dns.Models
{
    public class BulkRecordRequest
    {
        public IEnumerable<DnsRecord> Records { get; set; } = new List<DnsRecord>();
    }
}
