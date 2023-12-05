using spydersoft.windows.dns.Models;

namespace spydersoft.windows.dns.Services
{
    public interface IDnsService
    {
        Task<IEnumerable<DnsRecord>?> GetRecords(string? zoneName);

        Task<bool> DeleteRecord(DnsRecord record);

        Task<IEnumerable<DnsRecord>?> GetRecordsByHostname(string hostName, string? zoneName);

        Task<DnsRecord?> GetRecord(DnsRecord record);

        Task<DnsRecord?> CreateRecord(DnsRecord record);
    }
}