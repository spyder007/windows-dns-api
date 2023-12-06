using System.Text.Json.Serialization;

namespace Spydersoft.Windows.Dns.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DnsRecordType
    {
        A,
        AAAA,
        CNAME
    }
}
