FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
WORKDIR /app

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_ENVIRONMENT="Production"

COPY Properties/appsettings.json ./Properties/
COPY Properties/appsettings.Development.json ./Properties/
COPY Properties/appsettings.Production.json ./Properties/
COPY image-archive-api-ca.crt /usr/local/share/ca-certificates/image-archive-api-ca.crt

RUN mkdir -p ./Archives
RUN update-ca-certificates

USER app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ImageProjectBackend.csproj", "."]
RUN dotnet restore "./ImageProjectBackend.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./ImageProjectBackend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ImageProjectBackend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ImageProjectBackend.dll"]