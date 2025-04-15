FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first and restore dependencies
COPY *.sln .
COPY ReverseProxy/*.csproj ./ReverseProxy/
COPY WebApp/*.csproj ./WebApp/
RUN dotnet restore

# Copy the remaining source code
COPY ReverseProxy/. ./ReverseProxy/
COPY WebApp/. ./WebApp/

# Build the projects
WORKDIR /src/ReverseProxy
RUN dotnet publish -c Release -o /app/reverseproxy

WORKDIR /src/WebApp
RUN dotnet publish -c Release -o /app/webapp

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Create directory for the SQLite database
WORKDIR /app
RUN mkdir -p /app/data

# Copy published apps
COPY --from=build /app/reverseproxy ./reverseproxy
COPY --from=build /app/webapp ./webapp

# Copy the initial SQLite database if it exists in the source
COPY ReverseProxy/reverseproxy.db /app/reverseproxy/initial-db.db

# Set environment variables with default ports
ENV WEB_PORT=8000 \
    PROXY_PORT=8080

# Expose the ports
EXPOSE ${WEB_PORT} ${PROXY_PORT}

# Create an entrypoint script
RUN echo '#!/bin/sh\n\
# Check if database exists in the data directory, if not copy the initial one\n\
if [ ! -f /app/data/reverseproxy.db ]; then\n\
  # Copy the initial database if it exists\n\
  if [ -f /app/reverseproxy/initial-db.db ]; then\n\
    cp /app/reverseproxy/initial-db.db /app/data/reverseproxy.db\n\
    chmod 644 /app/data/reverseproxy.db\n\
    echo "Initialized database from template"\n\
  else\n\
    echo "No initial database found. A new one will be created."\n\
  fi\n\
fi\n\
\n\
# Start the reverse proxy\n\
cd /app/reverseproxy && dotnet ReverseProxy.dll --urls="http://0.0.0.0:${PROXY_PORT}" & \n\
# Wait a moment for the reverse proxy to start\n\
sleep 2\n\
# Start the web app\n\
cd /app/webapp && dotnet WebApp.dll --urls="http://0.0.0.0:${WEB_PORT}"\n\
# Keep container running\n\
wait\n' > /app/entrypoint.sh && \
chmod +x /app/entrypoint.sh

# Set the entrypoint
ENTRYPOINT ["/app/entrypoint.sh"]