# syntax=docker/dockerfile:1
#
# system-api: reglas de negocio y fuentes de datos. Nunca expuesta a internet.
# Build desde la raiz del repositorio:
#   docker build -f deploy/docker/system-api.Dockerfile -t modscore/system-api .

# ------------------------------------------------------------------ build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero lo que define dependencias: la capa de restore se reutiliza mientras no
# cambien. --locked-mode falla si el arbol no coincide con packages.lock.json.
COPY Directory.Build.props Directory.Packages.props ./
COPY contracts/ contracts/
COPY src/shared/ src/shared/
COPY src/system/ src/system/
RUN dotnet restore src/system/Modernization.SystemApi/Modernization.SystemApi.csproj --locked-mode

RUN dotnet publish src/system/Modernization.SystemApi/Modernization.SystemApi.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

# ---------------------------------------------------------------- runtime
# Chiseled: sin shell, sin gestor de paquetes y con usuario no root por defecto.
# Lo que no esta en la imagen no se puede explotar.
#
# Sin HEALTHCHECK: la imagen no tiene shell ni curl para ejecutarlo, y Kubernetes no
# lo usa; la salud la vigilan las probes de /health del Deployment.
# checkov:skip=CKV_DOCKER_2:Imagen chiseled sin shell; la salud la cubren las probes de Kubernetes
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app ./

USER $APP_UID

# Sin puertos de diagnostico: ni debugger, ni profiler, ni EventPipe. Tambien
# evita que el runtime intente escribir en un filesystem de solo lectura.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
ENTRYPOINT ["dotnet", "Modernization.SystemApi.dll"]
