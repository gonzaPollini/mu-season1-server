# Imagen oficial de OpenMU + nuestros plugins (plugins/), cargados con un startup hook. No se recompila OpenMU.
ARG OPENMU_IMAGE=munique/openmu:0.9.10

FROM ${OPENMU_IMAGE} AS openmu

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
COPY --from=openmu /app /openmu
WORKDIR /src
COPY plugins/ ./
RUN dotnet publish Season1Plugins.csproj -c Release -o /out -p:OpenMuDir=/openmu

FROM ${OPENMU_IMAGE}
COPY --from=build /out/Season1.OpenMU.Plugins.dll /app/plugins/
ENV DOTNET_STARTUP_HOOKS=/app/plugins/Season1.OpenMU.Plugins.dll
