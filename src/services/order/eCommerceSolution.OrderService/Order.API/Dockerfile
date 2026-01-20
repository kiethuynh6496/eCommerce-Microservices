# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files
COPY ["src/contracts/Ecommerce.Contracts/Ecommerce.Contracts/Ecommerce.Contracts.csproj", "contracts/Ecommerce.Contracts/Ecommerce.Contracts/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.API/Order.API.csproj", "services/order/eCommerceSolution.OrderService/Order.API/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.Application/Order.Application.csproj", "services/order/eCommerceSolution.OrderService/Order.Application/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.Domain/Order.Domain.csproj", "services/order/eCommerceSolution.OrderService/Order.Domain/"]
COPY ["src/services/order/eCommerceSolution.OrderService/Order.Infrastructure/Order.Infrastructure.csproj", "services/order/eCommerceSolution.OrderService/Order.Infrastructure/"]

# Restore
RUN dotnet restore "services/order/eCommerceSolution.OrderService/Order.API/Order.API.csproj"

# Copy Contracts and source code
COPY ["src/contracts/Ecommerce.Contracts/Ecommerce.Contracts/", "contracts/Ecommerce.Contracts/Ecommerce.Contracts/"]
COPY ["src/services/order/eCommerceSolution.OrderService/", "services/order/eCommerceSolution.OrderService/"]

# Build
RUN dotnet build "services/order/eCommerceSolution.OrderService/Order.API/Order.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "services/order/eCommerceSolution.OrderService/Order.API/Order.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Order.API.dll"]