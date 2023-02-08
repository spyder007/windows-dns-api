using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.WindowsServices;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Serilog;
using spydersoft.windows.dns.Models;
using spydersoft.windows.dns.Services;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Configuration;
using System.Web.Services.Description;
using spydersoft.windows.dns.Options;
using FluentValidation;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
};

var builder = WebApplication.CreateBuilder(options);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
bool configureAuthentication = !string.IsNullOrEmpty(builder.Configuration.GetValue<string>("Identity:AuthorityUrl"));

if (configureAuthentication)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwtBearerOptions =>
            {
                jwtBearerOptions.Authority = builder.Configuration.GetValue<string>("Identity:AuthorityUrl");
                jwtBearerOptions.Audience = builder.Configuration.GetValue<string>("Identity:ApiName");

                jwtBearerOptions.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
            }
        );
    builder.Services.AddAuthorization(cfg =>
    {
        cfg.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

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
    doc.SerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
});

builder.Host.UseWindowsService();

var app = builder.Build();

app.UseOpenApi();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUi3();
}

if (configureAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/info", async (ILogger<Program> log) =>
{
    var version = "0.0.0.0";
    try
    {
        await Task.Run(() =>
        {
            var fileInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            version = fileInfo.ProductVersion;
        });
    }
    catch (Exception e)
    {
        log.LogError(e, "Error retrieving file version");
        version = "0.0.0.0";
    }

    return Results.Ok(version);
}).WithName("info").WithDisplayName("Retrieve Application Info").WithTags("Info");

app.MapGet("/dns",
        async (ILogger<Program> log, IDnsService commandService, string? zoneName) =>
        {
            log.LogInformation("Fielding DNS Records Request (/dns)");

            IEnumerable<DnsRecord>? dnsRecords = await commandService.GetRecords(zoneName);
            return dnsRecords != null ? Results.Ok(dnsRecords) : Results.BadRequest();
        })
    .WithName("GetDnsRecords").WithDisplayName("Retrieve the list of DNS A/AAAA/CNAME Records").WithTags("DNS");

app.MapGet("/dns/{hostName}", async (ILogger<Program> log, IDnsService commandService, [FromRoute] string hostName, [FromQuery] string? zoneName) =>
    {
        log.LogInformation("Fielding DNS Record Request for  {hostName} in zone {zoneName}", hostName, zoneName);
        var dnsRecord = await commandService.GetRecordsByHostname(hostName, zoneName);
        return dnsRecord == null ? Results.NotFound() : Results.Ok(dnsRecord);
    })
    .WithName("GetDnsRecord").WithDisplayName("Get DNS Records based on Host Name").WithTags("DNS");

app.MapPost("/dns", async (ILogger<Program> log, IDnsService commandService, IValidator<DnsRecord> validator, [FromBody] DnsRecord record) =>
{
    log.LogInformation("Creating DnsRecord");
    var validationResult = await validator.ValidateAsync(record);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    var newRecord = await commandService.CreateRecord(record);
    return newRecord != null ? Results.Created($"/dns/{newRecord.HostName}?zoneName={newRecord.ZoneName}", newRecord) : Results.BadRequest();
}).WithName("UpdateHost").WithDisplayName("Update Host Name Record").WithTags("DNS");

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
}).WithName("DeleteHost").WithDisplayName("Delete Host Name Record").WithTags("DNS");


app.Run();