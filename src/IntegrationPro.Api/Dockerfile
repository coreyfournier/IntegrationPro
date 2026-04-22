FROM node:20-alpine AS ui
WORKDIR /ui
COPY src/IntegrationPro.Api.Ui/ ./
RUN npm ci && npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files first so `dotnet restore` is cacheable
COPY IntegrationPro.sln .
COPY src/IntegrationPro.PluginBase/IntegrationPro.PluginBase.csproj src/IntegrationPro.PluginBase/
COPY src/IntegrationPro.Domain/IntegrationPro.Domain.csproj src/IntegrationPro.Domain/
COPY src/IntegrationPro.Application/IntegrationPro.Application.csproj src/IntegrationPro.Application/
COPY src/IntegrationPro.Infrastructure/IntegrationPro.Infrastructure.csproj src/IntegrationPro.Infrastructure/
COPY src/IntegrationPro.Worker/IntegrationPro.Worker.csproj src/IntegrationPro.Worker/
COPY src/IntegrationPro.Api/IntegrationPro.Api.csproj src/IntegrationPro.Api/
COPY plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj plugins/IntegrationPro.Plugin.PrismHR/
COPY plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj plugins/IntegrationPro.Plugin.Mock/
COPY plugins/IntegrationPro.Plugin.SecEdgar/IntegrationPro.Plugin.SecEdgar.csproj plugins/IntegrationPro.Plugin.SecEdgar/
COPY tests/IntegrationPro.Application.Tests/IntegrationPro.Application.Tests.csproj tests/IntegrationPro.Application.Tests/
COPY tests/IntegrationPro.Api.Tests/IntegrationPro.Api.Tests.csproj tests/IntegrationPro.Api.Tests/
COPY tests/IntegrationPro.TestHarness/IntegrationPro.TestHarness.csproj tests/IntegrationPro.TestHarness/

RUN dotnet restore IntegrationPro.sln

# Now copy everything else + the UI dist
COPY src/ src/
COPY plugins/ plugins/
COPY --from=ui /ui/dist/ src/IntegrationPro.Api/wwwroot/ui/

RUN dotnet publish src/IntegrationPro.Api/IntegrationPro.Api.csproj -c Release -o /app/publish /p:SkipUiBuild=true --no-restore
RUN dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.PrismHR/1.0.0 --no-restore
RUN dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj         -c Release -o /app/plugins/IntegrationPro.Plugin.Mock/1.1.0 --no-restore
RUN dotnet publish plugins/IntegrationPro.Plugin.SecEdgar/IntegrationPro.Plugin.SecEdgar.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.SecEdgar/1.0.0 --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/plugins ./plugins
EXPOSE 8081
USER app
ENTRYPOINT ["dotnet", "IntegrationPro.Api.dll"]
