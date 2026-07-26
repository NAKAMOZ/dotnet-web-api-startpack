using Api.Extensions;
using Api.Logging;

// Before anything else, so a failure during registration or Build() is logged rather than
// printed to stderr by the runtime. Replaced by the configured logger inside
// AddObservabilityServices (§15).
SerilogSetup.Bootstrap();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddValidatedOptions()
    .AddApiServices()
    .AddForwardedHeaderServices()
    .AddDataServices(builder.Configuration)
    .AddAuthenticationServices()
    .AddAuthorizationServices()
    .AddDomainServices()
    .AddValidationServices()
    .AddObservabilityServices(builder.Configuration);

var app = builder.Build();

if (await app.RunOperationalCommandAsync(args))
{
    return;
}

await app.UseDatabaseSetupAsync();

app.UseApiPipeline();

app.Run();
