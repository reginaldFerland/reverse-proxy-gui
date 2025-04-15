FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first and restore dependencies
COPY *.sln .
COPY WebApp/*.csproj ./WebApp/
RUN dotnet restore

# Copy the remaining source code
COPY WebApp/. ./WebApp/

# Build the project
WORKDIR /src/WebApp
RUN dotnet publish -c Release -o /app/webapp

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Create directory for the SQLite database
WORKDIR /app
RUN mkdir -p /app/data

# Copy published app
COPY --from=build /app/webapp ./webapp

# Copy the initial SQLite database if it exists in the source
COPY WebApp/reverseproxy.db /app/data/reverseproxy.db

# Set environment variables with default ports
ENV WEB_PORT=8000 \
    PROXY_PORT=8080

# Expose the ports
EXPOSE ${WEB_PORT} ${PROXY_PORT}

# Set working directory to the webapp
WORKDIR /app/webapp

# Start the application
ENTRYPOINT ["dotnet", "WebApp.dll"]