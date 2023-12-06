using System.Management.Automation;

namespace Spydersoft.Windows.Dns.Services
{
    public interface IPowershellExecutor
    {
        Task<PSDataCollection<PSObject>> ExecuteCommandAndGetPipeline(string command);

        Task<bool> ExecuteCommand(string command);
    }
}