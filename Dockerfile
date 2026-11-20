# Multi-stage Dockerfile for SmartDelivery Microservices Solution
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for caching restore layer
COPY ["SmartDelivery.sln", "./"]
COPY ["src/Common/SmartDelivery.Shared/SmartDelivery.Shared.csproj", "src/Common/SmartDelivery.Shared/"]
COPY ["src/ApiGateway/SmartDelivery.ApiGateway/SmartDelivery.ApiGateway.csproj", "src/ApiGateway/SmartDelivery.ApiGateway/"]
COPY ["src/Services/Auth/SmartDelivery.AuthService/SmartDelivery.AuthService.csproj", "src/Services/Auth/SmartDelivery.AuthService/"]
COPY ["src/Services/Catalog/SmartDelivery.CatalogService/SmartDelivery.CatalogService.csproj", "src/Services/Catalog/SmartDelivery.CatalogService/"]
COPY ["src/Services/Order/SmartDelivery.OrderService/SmartDelivery.OrderService.csproj", "src/Services/Order/SmartDelivery.OrderService/"]
COPY ["src/Services/Delivery/SmartDelivery.DeliveryService/SmartDelivery.DeliveryService.csproj", "src/Services/Delivery/SmartDelivery.DeliveryService/"]
COPY ["tests/SmartDelivery.UnitTests/SmartDelivery.UnitTests.csproj", "tests/SmartDelivery.UnitTests/"]
COPY ["tests/SmartDelivery.IntegrationTests/SmartDelivery.IntegrationTests.csproj", "tests/SmartDelivery.IntegrationTests/"]

RUN dotnet restore SmartDelivery.sln

# Copy source and build
COPY . .
RUN dotnet build SmartDelivery.sln -c Release --no-restore

# Base ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Stage: Gateway
FROM build AS publish-gateway
RUN dotnet publish src/ApiGateway/SmartDelivery.ApiGateway/SmartDelivery.ApiGateway.csproj -c Release -o /app/publish --no-build

FROM base AS gateway
COPY --from=publish-gateway /app/publish .
ENTRYPOINT ["dotnet", "SmartDelivery.ApiGateway.dll"]

# Stage: AuthService
FROM build AS publish-auth
RUN dotnet publish src/Services/Auth/SmartDelivery.AuthService/SmartDelivery.AuthService.csproj -c Release -o /app/publish --no-build

FROM base AS auth
COPY --from=publish-auth /app/publish .
ENTRYPOINT ["dotnet", "SmartDelivery.AuthService.dll"]

# Stage: CatalogService
FROM build AS publish-catalog
RUN dotnet publish src/Services/Catalog/SmartDelivery.CatalogService/SmartDelivery.CatalogService.csproj -c Release -o /app/publish --no-build

FROM base AS catalog
COPY --from=publish-catalog /app/publish .
ENTRYPOINT ["dotnet", "SmartDelivery.CatalogService.dll"]

# Stage: OrderService
FROM build AS publish-order
RUN dotnet publish src/Services/Order/SmartDelivery.OrderService/SmartDelivery.OrderService.csproj -c Release -o /app/publish --no-build

FROM base AS order
COPY --from=publish-order /app/publish .
ENTRYPOINT ["dotnet", "SmartDelivery.OrderService.dll"]

# Stage: DeliveryService
FROM build AS publish-delivery
RUN dotnet publish src/Services/Delivery/SmartDelivery.DeliveryService/SmartDelivery.DeliveryService.csproj -c Release -o /app/publish --no-build

FROM base AS delivery
COPY --from=publish-delivery /app/publish .
ENTRYPOINT ["dotnet", "SmartDelivery.DeliveryService.dll"]
