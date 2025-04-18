FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Install Node.js for any frontend dependencies
RUN apt-get update && \
    apt-get install -y nodejs npm && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create data directory with proper permissions
# Since we're running as root, ensure root has appropriate permissions
RUN mkdir -p /app/data && \
    chmod 777 /app/data

# Expose both the UI and proxy ports
EXPOSE 8000
EXPOSE 8080

# Set user as root
USER root

ENTRYPOINT ["dotnet", "WebApp.dll"]