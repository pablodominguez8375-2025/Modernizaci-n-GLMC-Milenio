FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/PMGM.Api/PMGM.Api.csproj src/PMGM.Api/
RUN dotnet restore src/PMGM.Api/PMGM.Api.csproj

COPY . .
WORKDIR /src/src/PMGM.Api
RUN dotnet publish PMGM.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PMGM.Api.dll"]
