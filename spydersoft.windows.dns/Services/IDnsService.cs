using spydersoft.windows.dns.Models;

namespace spydersoft.windows.dns.Services
{
    public interface IDnsService
    {
        Task<IEnumerable<DnsRecord>?> GetDnsRecords(string? zoneName);

        Task<bool> DeleteRecord(string hostName, string? zoneName);

        Task<DnsRecord?> GetRecord(string hostName, string? zoneName);

        Task<DnsRecord?> UpdateRecord(DnsRecord record);
    }
}