# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Tạo cấu trúc thư mục giống local: /build/src/...
WORKDIR /build/src

# Copy csproj files
COPY ["src/contracts/Ecommerce.Contracts/Ecommerce.Contracts/Ecommerce.Contracts.csproj", "contracts/Ecommerce.Contracts/Ecommerce.Contracts/"]
COPY ["src/services/product/eCommerceSolution.ProductService/Product.API/Product.API.csproj", "services/product/eCommerceSolution.ProductService/Product.API/"]
COPY ["src/services/product/eCommerceSolution.ProductService/Product.Application/Product.Application.csproj", "services/product/eCommerceSolution.ProductService/Product.Application/"]
COPY ["src/services/product/eCommerceSolution.ProductService/Product.Domain/Product.Domain.csproj", "services/product/eCommerceSolution.ProductService/Product.Domain/"]
COPY ["src/services/product/eCommerceSolution.ProductService/Product.Infrastructure/Product.Infrastructure.csproj", "services/product/eCommerceSolution.ProductService/Product.Infrastructure/"]

# Restore
RUN dotnet restore "services/product/eCommerceSolution.ProductService/Product.API/Product.API.csproj"

# Copy source code
COPY ["src/contracts/Ecommerce.Contracts/Ecommerce.Contracts/", "contracts/Ecommerce.Contracts/Ecommerce.Contracts/"]
COPY ["src/services/product/eCommerceSolution.ProductService/", "services/product/eCommerceSolution.ProductService/"]

# Build
RUN dotnet build "services/product/eCommerceSolution.ProductService/Product.API/Product.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "services/product/eCommerceSolution.ProductService/Product.API/Product.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Product.API.dll"]