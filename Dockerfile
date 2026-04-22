FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY IntegrationPro.sln .
COPY src/IntegrationPro.PluginBase/IntegrationPro.PluginBase.csproj src/IntegrationPro.PluginBase/
COPY src/IntegrationPro.Domain/IntegrationPro.Domain.csproj src/IntegrationPro.Domain/
COPY src/IntegrationPro.Application/IntegrationPro.Application.csproj src/IntegrationPro.Application/
COPY src/IntegrationPro.Infrastructure/IntegrationPro.Infrastructure.csproj src/IntegrationPro.Infrastructure/
COPY src/IntegrationPro.Worker/IntegrationPro.Worker.csproj src/IntegrationPro.Worker/
COPY plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj plugins/IntegrationPro.Plugin.PrismHR/
COPY plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj plugins/IntegrationPro.Plugin.Mock/
COPY plugins/IntegrationPro.Plugin.SecEdgar/IntegrationPro.Plugin.SecEdgar.csproj plugins/IntegrationPro.Plugin.SecEdgar/

RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet publish src/IntegrationPro.Worker/IntegrationPro.Worker.csproj -c Release -o /app/publish

# Publish plugins to their own directories so the PluginLoader can find them
RUN dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.PrismHR/1.0.0
RUN dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj         -c Release -o /app/plugins/IntegrationPro.Plugin.Mock/1.1.0
RUN dotnet publish plugins/IntegrationPro.Plugin.SecEdgar/IntegrationPro.Plugin.SecEdgar.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.SecEdgar/1.0.0

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=build /app/plugins ./plugins

# Create output directory for extracted data
RUN mkdir -p /app/output

EXPOSE 8080

ENTRYPOINT ["dotnet", "IntegrationPro.Worker.dll"]
