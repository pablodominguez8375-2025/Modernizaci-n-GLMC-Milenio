# Imágenes base: por defecto etiquetas 10.0; el workflow qa525-migrator-oci las fija por DIGEST (build-arg) y lo registra.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${SDK_IMAGE} AS build
WORKDIR /src
COPY backend backend
COPY scripts/qa/operators/workshops104-migrator scripts/qa/operators/workshops104-migrator
WORKDIR /src/scripts/qa/operators/workshops104-migrator
RUN dotnet publish Workshops104Migrator.csproj -c Release -o /out /p:UseAppHost=false \
    /p:ContinuousIntegrationBuild=true /p:Deterministic=true

FROM ${RUNTIME_IMAGE}
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Workshops104Migrator.dll"]
