using System.Text.Json.Serialization;

namespace spydersoft.windows.dns.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DnsRecordType
    {
        A,
        AAAA,
        CNAME
    }
}
