namespace Spydersoft.Windows.Dns.Options
{
    public class HostSettings
    {
        public const string SectionName = "HostSettings";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 5000;
    }
}
