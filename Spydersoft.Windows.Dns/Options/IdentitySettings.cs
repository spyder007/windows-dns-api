namespace Spydersoft.Windows.Dns.Options
{
    public class IdentitySettings
    {
        public const string SectionName = "Identity";
        public string AuthorityUrl { get; set; } = default!;
        public string ApiName { get; set; } = default!;
    }
}
