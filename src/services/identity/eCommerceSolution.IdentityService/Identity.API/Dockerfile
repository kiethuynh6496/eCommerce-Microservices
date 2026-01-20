# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY ["src/services/identity/eCommerceSolution.IdentityService/Identity.API/Identity.API.csproj", "Identity.API/"]
COPY ["src/services/identity/eCommerceSolution.IdentityService/Identity.Application/Identity.Application.csproj", "Identity.Application/"]
COPY ["src/services/identity/eCommerceSolution.IdentityService/Identity.Domain/Identity.Domain.csproj", "Identity.Domain/"]
COPY ["src/services/identity/eCommerceSolution.IdentityService/Identity.Infrastructure/Identity.Infrastructure.csproj", "Identity.Infrastructure/"]

# Restore
RUN dotnet restore "Identity.API/Identity.API.csproj"

# Copy source code
COPY src/services/identity/eCommerceSolution.IdentityService/ .

# Build
RUN dotnet build "Identity.API/Identity.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Identity.API/Identity.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]