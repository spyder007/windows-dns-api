using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Spydersoft.Platform.Hosting.Options;
using Spydersoft.Platform.Hosting.StartupExtensions;
using Spydersoft.Windows.Dns.Models;
using Spydersoft.Windows.Dns.Options;
using Spydersoft.Windows.Dns.Services;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

var hostSettings = new HostSettings();
builder.Configuration.GetSection(HostSettings.SectionName).Bind(hostSettings);
builder.WebHost.UseUrls($"{hostSettings.Host}:{hostSettings.Port}");

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});


AppHealthCheckOptions healthCheckOptions = builder.AddSpydersoftHealthChecks();
builder.AddSpydersoftTelemetry(typeof(Program).Assembly)
    .AddSpydersoftSerilog();

bool authInstalled = builder.AddSpydersoftIdentity();

builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<DnsOptions>(builder.Configuration.GetSection(DnsOptions.SectionName));
builder.Services.AddSingleton<IPowershellExecutor, PowershellExecutor>();
builder.Services.AddSingleton<IDnsService, DnsService>();
builder.Services.AddScoped<IValidator<DnsRecord>, DnsRecordValidator>();

builder.Services.AddOpenApiDocument(doc =>
{
    doc.DocumentName = "windows.dns";
    doc.Title = "Windows DNS API";
    doc.Description = "API for interacting with Windows DNS";
});

builder.Host.UseWindowsService();

var app = builder.Build();

app.UseSpydersoftHealthChecks(healthCheckOptions);
app.UseOpenApi();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUi();
}

app.UseAuthentication(authInstalled).
    UseAuthorization(authInstalled);

app.MapGet("/dns",
        async (ILogger<Program> log, IDnsService commandService, string? zoneName) =>
        {
            log.LogInformation("Fielding DNS Records Request (/dns)");

            IEnumerable<DnsRecord>? dnsRecords = await commandService.GetRecords(zoneName);
            return dnsRecords != null ? Results.Ok(dnsRecords) : Results.BadRequest();
        })
    .WithName("GetDnsRecords")
    .WithDisplayName("Retrieve the list of DNS A/AAAA/CNAME Records")
    .WithTags("DNS")
    .Produces<IEnumerable<DnsRecord>>();

app.MapGet("/dns/{hostName}", async (ILogger<Program> log, IDnsService commandService, [FromRoute] string hostName, [FromQuery] string? zoneName) =>
    {
        log.LogInformation("Fielding DNS Record Request for  {HostName} in zone {ZoneName}", hostName, zoneName);
        var dnsRecord = await commandService.GetRecordsByHostname(hostName, zoneName);
        return dnsRecord == null ? Results.NotFound() : Results.Ok(dnsRecord);
    })
    .WithName("GetRecordByHostname")
    .WithDisplayName("Get DNS Records based on Host Name")
    .WithTags("DNS")
    .Produces<IEnumerable<DnsRecord>>()
    .Produces(StatusCodes.Status404NotFound);

app.MapPost("/dns",
        async (ILogger<Program> log, IDnsService commandService, IValidator<DnsRecord> validator,
            [FromBody] DnsRecord record) =>
        {
            log.LogInformation("Creating DnsRecord");
            var validationResult = await validator.ValidateAsync(record);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var newRecord = await commandService.CreateRecord(record);
            return newRecord != null
                ? Results.Created($"/dns/{newRecord.HostName}?zoneName={newRecord.ZoneName}", newRecord)
                : Results.BadRequest();
        })
    .WithName("CreateRecord")
    .WithDisplayName("Create Host Name Record")
    .WithTags("DNS")
    .Produces<DnsRecord>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest);

app.MapPost("/dns/bulk",
        async (ILogger<Program> log, IDnsService commandService, IValidator<DnsRecord> validator,
            [FromBody] BulkRecordRequest request) =>
        {
            log.LogInformation("Creating DnsRecord");

            var newRecords = new List<DnsRecord>();

            foreach (var record in request.Records)
            {
                var validationResult = await validator.ValidateAsync(record);
                if (!validationResult.IsValid)
                {
                    return Results.ValidationProblem(validationResult.ToDictionary());
                }

                var newRecord = await commandService.CreateRecord(record);
                if (newRecord != null)
                {
                    newRecords.Add(newRecord);
                }
            }

            return newRecords.Count > 0
                ? Results.Created($"/dns/", newRecords)
                : Results.BadRequest();
        })
    .WithName("CreateDnsRecords")
    .WithDisplayName("Bulk Create Host Name Records")
    .WithTags("DNS")
    .Produces<IEnumerable<DnsRecord>>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest);

app.MapDelete("/dns", async (ILogger<Program> log, IDnsService commandService, IValidator<DnsRecord> validator, [FromBody] DnsRecord record) =>
{
    log.LogInformation("Deleting DnsRecord");
    var validationResult = await validator.ValidateAsync(record);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }
    var success = await commandService.DeleteRecord(record);
    return success ? Results.Accepted() : Results.BadRequest();
})
    .WithName("DeleteRecord")
    .WithDisplayName("Delete Host Name Record")
    .WithTags("DNS")
    .Produces(StatusCodes.Status202Accepted)
    .Produces(StatusCodes.Status400BadRequest);


await app.RunAsync();
