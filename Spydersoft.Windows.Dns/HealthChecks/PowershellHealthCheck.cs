using Microsoft.Extensions.Diagnostics.HealthChecks;
using Spydersoft.Platform.Attributes;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Spydersoft.Windows.Dns.HealthChecks
{
    [SpydersoftHealthCheck(nameof(PowershellHealthCheck), failureStatus: HealthStatus.Unhealthy, "ready")]
    public class PowershellHealthCheck : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var defaultSessionState = InitialSessionState.CreateDefault();
            defaultSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
            defaultSessionState.ImportPSModule("DnsServer");
            using PowerShell ps = PowerShell.Create(defaultSessionState);

            ps.AddScript("(Get-Module DnsServer) -ne $null");
            var pipeline = await ps.InvokeAsync().ConfigureAwait(false);

            var powershellCheckData = new Dictionary<string, object>()
            {
                { "Warnings", ps.Streams.Warning.Count }
            };

            if (ps.HadErrors)
            {
                powershellCheckData.Add("Errors", ps.Streams.Error.Count);
                powershellCheckData.Add("FirstException", ps.Streams.Error[0].Exception.Message);
                return new HealthCheckResult(context.Registration.FailureStatus, "Powershell Error", data: powershellCheckData);
            }

            bool foundModule = pipeline.Select(result => result.BaseObject).OfType<bool>().First();

            if (!foundModule)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "DnsServer Module Not Found", data: powershellCheckData);
            }


            return new HealthCheckResult(HealthStatus.Healthy, data: powershellCheckData);
        }
    }
}
