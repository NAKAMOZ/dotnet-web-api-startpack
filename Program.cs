using Api.Extensions;
using Api.Logging;

// Before anything else, so a failure during registration or Build() is logged rather than
// printed to stderr by the runtime. Replaced by the configured logger inside
// AddObservabilityServices (§15).
SerilogSetup.Bootstrap();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices()
    .AddDataServices(builder.Configuration)
    .AddAuthenticationServices(builder.Configuration)
    .AddAuthorizationServices()
    .AddDomainServices()
    .AddValidationServices()
    .AddObservabilityServices(builder.Configuration);

var app = builder.Build();

await app.UseDatabaseSetupAsync();

app.UseApiPipeline();

app.Run();
