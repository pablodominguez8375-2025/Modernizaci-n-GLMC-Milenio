FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend backend
COPY scripts/qa/operators/support00-migrator scripts/qa/operators/support00-migrator
WORKDIR /src/scripts/qa/operators/support00-migrator
RUN dotnet publish Support00Migrator.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Support00Migrator.dll"]
