using FluentValidation;
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
builder.Services.AddControllers();

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

app.MapControllers();

await app.RunAsync();
