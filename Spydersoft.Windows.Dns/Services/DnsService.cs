using Microsoft.Extensions.Options;
using Spydersoft.Windows.Dns.Models;
using Spydersoft.Windows.Dns.Options;
using System.Management.Automation;

namespace Spydersoft.Windows.Dns.Services
{
    public class DnsService(ILogger<DnsService> logger, IPowershellExecutor executor, IOptions<DnsOptions> options) : IDnsService
    {
        private const string GetDnsRecordsTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} | ?{{ $_.RecordType -in \"A\", \"AAAA\", \"CNAME\" }}";

        private const string GetDnsRecordTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} -Name {2} {3}";

        private const string DnsRecordExpansion = " | Select-Object HostName, RecordType, @{l=\"IPv4Address\";e={$_.RecordData.IPv4Address}}, @{l=\"HostNameAlias\";e={$_.RecordData.HostNameAlias}}, @{l=\"IPv6Address\";e={$_.RecordData.IPv6Address}}";

        private const string CreateDnsARecordTemplate = "Add-DnsServerResourceRecordA -Name \"{2}\" -zoneName {0} -allowupdateany -Ipv4Address \"{3}\" -ComputerName {1}";

        private const string CreateDnsAAAARecordTemplate = "Add-DnsServerResourceRecordAAAA -Name \"{2}\" -zoneName {0} -allowupdateany -Ipv6Address \"{3}\" -ComputerName {1}";

        private const string CreateDnsCNAMERecordTemplate = "Add-DnsServerResourceRecordA -Name \"{2}\" -zoneName {0} -allowupdateany -HostNameAlias \"{3}\" -ComputerName {1}";

        private const string GetDnsARecordTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} -name {2} -RRType A | ? {{$_.RecordData.IPv4Address -eq \"{3}\"}}";

        private const string GetDnsAAAARecordTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} -name {2} -RRType AAAA | ? {{$_.RecordData.IPv6Address -eq \"{3}\"}}";

        private const string GetDnsCNAMERecordTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} -name {2} -RRType CNAME | ? {{$_.RecordData.HostNameAlias -eq \"{3}\"}}";

        private const string DeleteDnsRecordTemplate = "{2} | Remove-DnsServerResourceRecord -zonename {0} -computername {1} -Force";

        private readonly ILogger<DnsService> _logger = logger;

        private readonly IPowershellExecutor _executor = executor;

        private readonly DnsOptions _dnsOptions = options.Value;

        public async Task<DnsRecord?> CreateRecord(DnsRecord record)
        {
            var existingRecord = await GetRecord(record);

            if (existingRecord != null)
            {
                return existingRecord;
            }

            string command = GetCreateCommandForRecord(record);

            if (string.IsNullOrWhiteSpace(command))
            {
                _logger.LogWarning("Could not build command for record");
                return null;
            }

            try
            {
                var success = await _executor.ExecuteCommand(command);
                return success ? await GetRecord(record) : null;

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Updating DNS Records");
            }

            return null;


        }

        public async Task<bool> DeleteRecord(DnsRecord record)
        {
            var recordQuery = GetRecordQueryCommandForRecord(record, false);
            var deleteCommand = string.Format(DeleteDnsRecordTemplate, record.ZoneName, _dnsOptions.DnsServerName,
                recordQuery);

            try
            {
                return await _executor.ExecuteCommand(deleteCommand);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Updating DNS Records");
                return false;
            }
        }

        public async Task<DnsRecord?> GetRecord(DnsRecord record)
        {
            string command = GetRecordQueryCommandForRecord(record, true);

            try
            {
                var pipelineObjects = await _executor.ExecuteCommandAndGetPipeline(command);
                _logger.LogDebug("Found {Objects} objects", pipelineObjects.Count);
                if (pipelineObjects.Count == 0)
                {
                    return null;
                }

                var dnsRecords = pipelineObjects.Select(psRecord => BuildDnsRecordFromObject(record.ZoneName, psRecord));

                return dnsRecords.First();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving DNS Record");
            }

            return null;
        }

        public async Task<IEnumerable<DnsRecord>?> GetRecords(string? zoneName)
        {
            string zone = string.IsNullOrWhiteSpace(zoneName) ? _dnsOptions.DefaultZone : zoneName;

            try
            {
                var command = string.Format(GetDnsRecordsTemplate, zone, _dnsOptions.DnsServerName) +
                              DnsRecordExpansion;

                var pipelineObjects = await _executor.ExecuteCommandAndGetPipeline(command);
                _logger.LogDebug("Found {Objects} objects", pipelineObjects.Count);
                var dnsRecords = pipelineObjects.Select(record => BuildDnsRecordFromObject(zone, record));

                return dnsRecords;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving DNS Records");
            }

            return null;
        }

        public async Task<IEnumerable<DnsRecord>?> GetRecordsByHostname(string hostName, string? zoneName)
        {
            string zone = string.IsNullOrWhiteSpace(zoneName) ? _dnsOptions.DefaultZone : zoneName;

            try
            {
                var pipelineObjects = await _executor.ExecuteCommandAndGetPipeline(string.Format(GetDnsRecordTemplate, zone, _dnsOptions.DnsServerName, hostName, DnsRecordExpansion));
                _logger.LogDebug("Found {Objects} objects", pipelineObjects.Count);
                if (pipelineObjects.Count == 0)
                {
                    return null;
                }

                var dnsRecords = pipelineObjects.Select(record => BuildDnsRecordFromObject(zone, record));

                return dnsRecords;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving DNS Record");
            }

            return null;
        }

        private string GetRecordQueryCommandForRecord(DnsRecord record, bool expand)
        {
            string template = "";
            switch (record.RecordType)
            {
                case DnsRecordType.A:
                    template = GetDnsARecordTemplate;
                    break;

                case DnsRecordType.AAAA:
                    template = GetDnsAAAARecordTemplate;
                    break;

                case DnsRecordType.CNAME:
                    template = GetDnsCNAMERecordTemplate;
                    break;
            }
            var command = !string.IsNullOrWhiteSpace(template) ? string.Format(template, record.ZoneName, _dnsOptions.DnsServerName, record.HostName, record.Data) : string.Empty;
            if (expand)
            {
                command += DnsRecordExpansion;
            }
            return command;
        }
        private string GetCreateCommandForRecord(DnsRecord record)
        {
            string template = "";
            switch (record.RecordType)
            {
                case DnsRecordType.A:
                    template = CreateDnsARecordTemplate;
                    break;

                case DnsRecordType.AAAA:
                    template = CreateDnsAAAARecordTemplate;
                    break;

                case DnsRecordType.CNAME:
                    template = CreateDnsCNAMERecordTemplate;
                    break;
            }
            return !string.IsNullOrWhiteSpace(template) ? string.Format(template, record.ZoneName, _dnsOptions.DnsServerName, record.HostName, record.Data) : string.Empty;
        }

        private static DnsRecord BuildDnsRecordFromObject(string zoneName, PSObject record)
        {
            if (!Enum.TryParse<DnsRecordType>(record.Properties["RecordType"].Value.ToString(), out var recordType))
            {
                recordType = DnsRecordType.A;
            }

            string dataProperty = "";
            switch (recordType)
            {
                case DnsRecordType.A:
                case DnsRecordType.AAAA:
                    dataProperty = "IPv4Address";
                    break;
                case DnsRecordType.CNAME:
                    dataProperty = "HostNameAlias";
                    break;

            }

            return new DnsRecord(zoneName,
                record.Properties["HostName"].Value?.ToString() ?? string.Empty,
                recordType,
                record.Properties[dataProperty].Value?.ToString() ?? string.Empty);
        }

    }
}
