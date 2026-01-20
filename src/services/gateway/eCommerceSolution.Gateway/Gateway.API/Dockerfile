# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj file
COPY ["src/services/gateway/eCommerceSolution.Gateway/Gateway.API/Gateway.API.csproj", "Gateway.API/"]

# Restore
RUN dotnet restore "Gateway.API/Gateway.API.csproj"

# Copy source code
COPY ["src/services/gateway/eCommerceSolution.Gateway/Gateway.API/", "Gateway.API/"]

# Build
RUN dotnet build "Gateway.API/Gateway.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Gateway.API/Gateway.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gateway.API.dll"]