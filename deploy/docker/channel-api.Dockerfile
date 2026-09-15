# syntax=docker/dockerfile:1
#
# channel-api: BFF y SPA desde el mismo origen. Es lo unico expuesto al ingress.
# Build desde la raiz del repositorio:
#   docker build -f deploy/docker/channel-api.Dockerfile -t modscore/channel-api .

# -------------------------------------------------------------------- SPA
FROM node:24-alpine AS web
WORKDIR /web

# npm ci instala exactamente el lock; .npmrc impide scripts de instalacion.
COPY src/web/package.json src/web/package-lock.json src/web/.npmrc ./
RUN npm ci --ignore-scripts

COPY src/web/ ./
RUN npm run build

# ------------------------------------------------------------------ build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# La Channel referencia los contratos de la System: necesita ambos arboles.
COPY Directory.Build.props Directory.Packages.props ./
COPY contracts/ contracts/
COPY src/shared/ src/shared/
COPY src/system/ src/system/
COPY src/channel/ src/channel/
RUN dotnet restore src/channel/Modernization.ChannelApi/Modernization.ChannelApi.csproj --locked-mode

RUN dotnet publish src/channel/Modernization.ChannelApi/Modernization.ChannelApi.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

# ---------------------------------------------------------------- runtime
# Sin HEALTHCHECK: la imagen no tiene shell ni curl para ejecutarlo, y Kubernetes no
# lo usa; la salud la vigilan las probes de /health del Deployment.
# checkov:skip=CKV_DOCKER_2:Imagen chiseled sin shell; la salud la cubren las probes de Kubernetes
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app ./

# El build de la SPA se sirve con la CSP estricta del channel-api.
COPY --from=web /web/dist ./wwwroot

USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "Modernization.ChannelApi.dll"]
