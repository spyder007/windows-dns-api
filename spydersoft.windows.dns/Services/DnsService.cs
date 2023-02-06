using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Management.Infrastructure;
using Namotion.Reflection;
using Serilog;
using spydersoft.windows.dns.Models;
using spydersoft.windows.dns.Options;
using System.Management.Automation;
using System.Security.Policy;
using System.Text.Json;
using System.Xml.Linq;

namespace spydersoft.windows.dns.Services
{
    public class DnsService : IDnsService
    {
        private const string GetDnsRecordsTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} | ?{{ $_.RecordType -in \"A\", \"AAAA\", \"CNAME\" }} {2}";

        private const string GetDnsRecordTemplate = "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} -Name {2} {3}";

        private const string DnsRecordExpansion = " | Select-Object HostName, RecordType, @{l=\"IPv4Address\";e={$_.RecordData.IPv4Address}}, @{l=\"HostNameAlias\";e={$_.RecordData.HostNameAlias}}, @{l=\"IPv6Address\";e={$_.RecordData.IPv6Address}}";

        private const string CreateDnsARecordTemplate = "Add-DnsServerResourceRecordA -Name \"{2}\" -zoneName {0} -allowupdateany -Ipv4Address \"{3}\" -ComputerName {1}";

        private const string CreateDnsAAAARecordTemplate = "Add-DnsServerResourceRecordAAAA -Name \"{2}\" -zoneName {0} -allowupdateany -Ipv6Address \"{3}\" -ComputerName {1}";

        private const string CreateDnsCNAMERecordTemplate = "Add-DnsServerResourceRecordA -Name \"{2}\" -zoneName {0} -allowupdateany -HostNameAlias \"{3}\" -ComputerName {1}";

        private const string DeleteByHostnameTemplate =
            "Get-DnsServerResourceRecord -zonename {0} -ComputerName {1} -name {2} | Remove-DnsServerResourceRecord -zonename {0} -computername {1} -Force";

        private const string RefreshAutomaticStartDelayTemplate = "Get-VM | Select Name, State, AutomaticStartDelay, @{{n='startGroup';e= {{(ConvertFrom-Json $_.Notes).startGroup}}}}, @{{n='delayOffset';e= {{(ConvertFrom-Json $_.Notes).delayOffset}}}} |? {{$_.startGroup -gt 0}} | % {{ Set-VM -name $_.name -AutomaticStartDelay ((($_.startGroup - 1) * {0}) + $_.delayOffset) }}";

        private readonly ILogger<DnsService> _logger;

        private readonly IPowershellExecutor _executor;

        private readonly DnsOptions _dnsOptions;

        public DnsService(ILogger<DnsService> logger, IPowershellExecutor executor, IOptions<DnsOptions> options)
        {
            _logger = logger;
            _executor = executor;
            _dnsOptions = options.Value;
        }

        public async Task<DnsRecord?> UpdateRecord(DnsRecord record)
        {
            var existingRecord = await GetRecord(record.HostName, record.ZoneName);

            if (existingRecord != null)
            {
                await DeleteRecord(record.HostName, record.ZoneName);
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
                return success ? await GetRecord(record.HostName, record.ZoneName) : null;

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Updating DNS Records");
            }

            return null;


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

        public async Task<bool> DeleteRecord( string hostName, string? zoneName)
        {
            var zone = string.IsNullOrWhiteSpace(zoneName) ? _dnsOptions.DefaultZone : zoneName;
            var command = string.Format(DeleteByHostnameTemplate, zone, _dnsOptions.DnsServerName, hostName);

            try
            {
                return await _executor.ExecuteCommand(command);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Updating DNS Records");
                return false;
            }
        }

        public async Task<IEnumerable<DnsRecord>?> GetDnsRecords(string? zoneName)
        {
            string zone = string.IsNullOrWhiteSpace(zoneName) ? _dnsOptions.DefaultZone : zoneName;

            try
            {
                var pipelineObjects = await _executor.ExecuteCommandAndGetPipeline(string.Format(GetDnsRecordsTemplate, zone, _dnsOptions.DnsServerName, DnsRecordExpansion));
                _logger.LogDebug("Found {0} objects", pipelineObjects.Count);
                var dnsRecords = pipelineObjects.Select(record => BuildDnsRecordFromObject(zone, record));

                return dnsRecords;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving DNS Records");
            }

            return null;
        }

        public async Task<DnsRecord?> GetRecord(string hostName, string? zoneName)
        {
            string zone = string.IsNullOrWhiteSpace(zoneName) ? _dnsOptions.DefaultZone : zoneName;

            try
            {
                var pipelineObjects = await _executor.ExecuteCommandAndGetPipeline(string.Format(GetDnsRecordTemplate, zone, _dnsOptions.DnsServerName, hostName, DnsRecordExpansion));
                _logger.LogDebug("Found {0} objects", pipelineObjects.Count);
                if (pipelineObjects.Count == 0)
                {
                    return null;
                }

                var dnsRecords = pipelineObjects.Select(record => BuildDnsRecordFromObject(zone, record));

                return dnsRecords.First();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error retrieving DNS Record");
            }

            return null;
        }

        private DnsRecord BuildDnsRecordFromObject(string zoneName, PSObject record)
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