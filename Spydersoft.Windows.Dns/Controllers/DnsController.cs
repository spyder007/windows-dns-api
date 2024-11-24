using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Spydersoft.Windows.Dns.Models;
using Spydersoft.Windows.Dns.Services;

namespace Spydersoft.Windows.Dns.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class DnsController : ControllerBase
    {
        private readonly ILogger<DnsController> _log;
        private readonly IDnsService _commandService;
        private readonly IValidator<DnsRecord> _dnsRecordValidator;

        public DnsController(ILogger<DnsController> log, IDnsService commandService, IValidator<DnsRecord> dnsRecordValidator)
        {
            _log = log;
            _commandService = commandService;
            _dnsRecordValidator = dnsRecordValidator;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<DnsRecord>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> Get([FromQuery] string? zoneName)
        {
            _log.LogInformation("Fielding DNS Records Request (/dns)");

            IEnumerable<DnsRecord>? dnsRecords = await _commandService.GetRecords(zoneName);
            return dnsRecords != null ? Results.Ok(dnsRecords) : Results.BadRequest();
        }

        [HttpGet("{hostName}")]
        [ProducesResponseType(typeof(DnsRecord), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> Get([FromRoute] string hostName, [FromQuery] string? zoneName)
        {
            _log.LogInformation("Fielding DNS Record Request for  {HostName} in zone {ZoneName}", hostName, zoneName);
            var dnsRecord = await _commandService.GetRecordsByHostname(hostName, zoneName);
            return dnsRecord == null ? Results.NotFound() : Results.Ok(dnsRecord);
        }

        [HttpPost]
        [ProducesResponseType(typeof(DnsRecord), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> Post([FromBody] DnsRecord record)
        {
            _log.LogInformation("Creating DnsRecord");
            var validationResult = await _dnsRecordValidator.ValidateAsync(record);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var newRecord = await _commandService.CreateRecord(record);
            return newRecord != null
                ? Results.Created($"/dns/{newRecord.HostName}?zoneName={newRecord.ZoneName}", newRecord)
                : Results.BadRequest();
        }

        [HttpPost("bulk")]
        [ProducesResponseType(typeof(IEnumerable<DnsRecord>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> BulkUpdate([FromBody] BulkRecordRequest request)
        {
            _log.LogInformation("Creating DnsRecord");

            var newRecords = new List<DnsRecord>();

            foreach (var record in request.Records)
            {
                var validationResult = await _dnsRecordValidator.ValidateAsync(record);
                if (!validationResult.IsValid)
                {
                    return Results.ValidationProblem(validationResult.ToDictionary());
                }

                var newRecord = await _commandService.CreateRecord(record);
                if (newRecord != null)
                {
                    newRecords.Add(newRecord);
                }
            }

            return newRecords.Count > 0
                ? Results.Created($"/dns/", newRecords)
                : Results.BadRequest();
        }

        // DELETE api/<DnsController>/5
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IResult> Delete([FromBody] DnsRecord record)
        {
            _log.LogInformation("Deleting DnsRecord");
            var validationResult = await _dnsRecordValidator.ValidateAsync(record);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }
            var success = await _commandService.DeleteRecord(record);
            return success ? Results.Accepted() : Results.BadRequest();
        }
    }
}
