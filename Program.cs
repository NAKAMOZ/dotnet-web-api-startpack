using Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices()
    .AddDataServices(builder.Configuration)
    .AddAuthenticationServices(builder.Configuration)
    .AddAuthorizationServices()
    .AddValidationServices()
    .AddObservabilityServices(builder.Configuration);

var app = builder.Build();

await app.UseDatabaseSetupAsync();

app.UseApiPipeline();

app.Run();
