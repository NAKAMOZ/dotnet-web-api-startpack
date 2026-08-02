using Api.Extensions;
using Api.Logging;

// Before anything else, so a failure during registration or Build() is logged rather than
// printed to stderr by the runtime. Replaced by the configured logger inside
// AddObservabilityServices (§15).
SerilogSetup.Bootstrap();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddValidatedOptions()
    .AddDistributedRuntimeServices(builder.Configuration)
    .AddApiServices()
    .AddForwardedHeaderServices()
    .AddDataServices(builder.Configuration)
    .AddAuthenticationServices(builder.Configuration)
    .AddAuthorizationServices()
    .AddDomainServices(builder.Configuration)
    .AddValidationServices()
    .AddObservabilityServices(builder.Configuration, builder.Environment);

var app = builder.Build();

if (await app.RunOperationalCommandAsync(args))
{
    return;
}

await app.UseDatabaseSetupAsync();

app.UseApiPipeline();

app.Run();
