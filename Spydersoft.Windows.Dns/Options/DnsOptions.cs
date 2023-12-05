namespace spydersoft.windows.dns.Options
{
    public class DnsOptions
    {
        public const string SectionName = "DnsOptions";

        public string DefaultZone { get; set; } = "example.com";
        public string? DnsServerName { get; set; }
    }
}
