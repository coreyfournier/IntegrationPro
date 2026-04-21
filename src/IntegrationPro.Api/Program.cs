using IntegrationPro.Api;
using IntegrationPro.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIntegrationInfrastructureForApi(builder.Configuration);
builder.Services.AddSingleton<IntegrationPro.Api.Execution.SyncExecutor>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseDefaultFiles();

PluginEndpoints.Map(app);
IntegrationEndpoints.Map(app);

app.MapHealthChecks("/healthz/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program { }  // for WebApplicationFactory<Program>
