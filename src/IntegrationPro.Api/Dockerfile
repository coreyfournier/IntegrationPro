FROM node:20-alpine AS ui
WORKDIR /ui
COPY src/IntegrationPro.Api.Ui/ ./
RUN npm ci && npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY IntegrationPro.sln .
COPY src/ src/
COPY plugins/ plugins/
COPY --from=ui /ui/dist/ src/IntegrationPro.Api/wwwroot/ui/
RUN dotnet publish src/IntegrationPro.Api/IntegrationPro.Api.csproj -c Release -o /app/publish /p:SkipUiBuild=true
RUN dotnet publish plugins/IntegrationPro.Plugin.PrismHR/IntegrationPro.Plugin.PrismHR.csproj -c Release -o /app/plugins/IntegrationPro.Plugin.PrismHR/1.0.0
RUN dotnet publish plugins/IntegrationPro.Plugin.Mock/IntegrationPro.Plugin.Mock.csproj         -c Release -o /app/plugins/IntegrationPro.Plugin.Mock/1.0.0

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/plugins ./plugins
EXPOSE 8081
ENTRYPOINT ["dotnet", "IntegrationPro.Api.dll"]
