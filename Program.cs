using Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices()
    .AddDataServices(builder.Configuration)
    .AddAuthenticationServices(builder.Configuration)
    .AddValidationServices()
    .AddObservabilityServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();

app.Run();
