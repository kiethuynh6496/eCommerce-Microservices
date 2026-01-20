# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj file
COPY ["src/services/gateway/Ocelot.ApiGateway/Ocelot.ApiGateway.csproj", "Ocelot.ApiGateway/"]

# Restore
RUN dotnet restore "Ocelot.ApiGateway/Ocelot.ApiGateway.csproj"

# Copy source code
COPY src/services/gateway/Ocelot.ApiGateway/ Ocelot.ApiGateway/

# Build
RUN dotnet build "Ocelot.ApiGateway/Ocelot.ApiGateway.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Ocelot.ApiGateway/Ocelot.ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ocelot.ApiGateway.dll"]