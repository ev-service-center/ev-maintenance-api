FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["EVServiceCenterMaintenanceAPI/EVServiceCenterMaintenanceAPI.csproj", "EVServiceCenterMaintenanceAPI/"]
RUN dotnet restore "EVServiceCenterMaintenanceAPI/EVServiceCenterMaintenanceAPI.csproj"

COPY EVServiceCenterMaintenanceAPI/ EVServiceCenterMaintenanceAPI/
WORKDIR "/src/EVServiceCenterMaintenanceAPI"

RUN dotnet build "EVServiceCenterMaintenanceAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EVServiceCenterMaintenanceAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "EVServiceCenterMaintenanceAPI.dll"]
