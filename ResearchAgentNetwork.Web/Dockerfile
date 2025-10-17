# Multi-stage Dockerfile for ResearchAgentNetwork.Web
# Supports both development (with hot reload) and production builds

# ============================================================================
# Stage 1: Build
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for dependency restoration
COPY ["ResearchAgentNetwork.Core/ResearchAgentNetwork.Core.csproj", "ResearchAgentNetwork.Core/"]
COPY ["ResearchAgentNetwork.Infrastructure/ResearchAgentNetwork.Infrastructure.csproj", "ResearchAgentNetwork.Infrastructure/"]
COPY ["ResearchAgentNetwork.Persistence/ResearchAgentNetwork.Persistence.csproj", "ResearchAgentNetwork.Persistence/"]
COPY ["ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj", "ResearchAgentNetwork.Web/"]

# Restore dependencies
RUN dotnet restore "ResearchAgentNetwork.Web/ResearchAgentNetwork.Web.csproj"

# Copy all source code
COPY . .

# Build the solution
WORKDIR "/src/ResearchAgentNetwork.Web"
RUN dotnet build "ResearchAgentNetwork.Web.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "ResearchAgentNetwork.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============================================================================
# Stage 2: Development (with dotnet watch for hot reload)
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /workspace

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy built application from build stage
COPY --from=build /app/build /app

# Create directory for SQLite database
RUN mkdir -p /app/data

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Use dotnet watch for hot reload in development
# Note: Source code should be mounted as volume in docker-compose.dev.yml
ENTRYPOINT ["dotnet", "watch", "run", "--project", "ResearchAgentNetwork.Web", "--no-launch-profile"]

# ============================================================================
# Stage 3: Production (minimal runtime)
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS production
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application from build stage
COPY --from=build /app/publish .

# Create directory for SQLite database with proper permissions
RUN mkdir -p /app/data && chmod 777 /app/data

# Create non-root user for security
RUN useradd -m -u 1000 appuser && \
    chown -R appuser:appuser /app
USER appuser

# Expose HTTP port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ResearchAgentNetwork.Web.dll"]
