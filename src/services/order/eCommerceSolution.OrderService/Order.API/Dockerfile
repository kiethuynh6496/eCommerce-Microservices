# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY ["src/contracts/Ecommerce.Contracts/Ecommerce.Contracts/Ecommerce.Contracts.csproj", "Ecommerce.Contracts/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.API/Order.API.csproj", "Order.API/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.Application/Order.Application.csproj", "Order.Application/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.Domain/Order.Domain.csproj", "Order.Domain/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.Infrastructure/Order.Infrastructure.csproj", "Order.Infrastructure/"]

# Restore
RUN dotnet restore "Order.API/Order.API.csproj"

# Copy Contracts source code
COPY src/contracts/Ecommerce.Contracts/Ecommerce.Contracts/ Ecommerce.Contracts/

# Copy source code
COPY src/services/order/eCommerceSolution.OrderService/ .

# Build
RUN dotnet build "Order.API/Order.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Order.API/Order.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Order.API.dll"]