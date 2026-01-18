FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files và restore dependencies
COPY ["Product.API/Product.API.csproj", "Product.API/"]
COPY ["Product.Application/Product.Application.csproj", "Product.Application/"]
COPY ["Product.Domain/Product.Domain.csproj", "Product.Domain/"]
COPY ["Product.Infrastructure/Product.Infrastructure.csproj", "Product.Infrastructure/"]

RUN dotnet restore "./Product.API/Product.API.csproj"

# Copy toàn bộ source code
COPY . .
WORKDIR "/src/Product.API"
RUN dotnet build "./Product.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Product.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Product.API.dll"]