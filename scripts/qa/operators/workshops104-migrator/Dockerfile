FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend backend
COPY scripts/qa/operators/workshops104-migrator scripts/qa/operators/workshops104-migrator
WORKDIR /src/scripts/qa/operators/workshops104-migrator
RUN dotnet publish Workshops104Migrator.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Workshops104Migrator.dll"]
