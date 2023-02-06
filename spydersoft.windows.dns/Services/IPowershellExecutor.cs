using System.Management.Automation;

namespace spydersoft.windows.dns.Services
{
    public interface IPowershellExecutor
    {
        Task<PSDataCollection<PSObject>> ExecuteCommandAndGetPipeline(string command);

        Task<bool> ExecuteCommand(string command);
    }
}