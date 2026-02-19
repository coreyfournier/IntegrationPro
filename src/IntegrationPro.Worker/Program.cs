using IntegrationPro.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddIntegrationInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
