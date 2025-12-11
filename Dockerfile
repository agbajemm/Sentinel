# SENTINEL AI API - Dockerfile
# Multi-stage build for optimized production image

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ["SentinelAI.sln", "./"]
COPY ["src/SentinelAI.Core/SentinelAI.Core.csproj", "src/SentinelAI.Core/"]
COPY ["src/SentinelAI.Application/SentinelAI.Application.csproj", "src/SentinelAI.Application/"]
COPY ["src/SentinelAI.Infrastructure/SentinelAI.Infrastructure.csproj", "src/SentinelAI.Infrastructure/"]
COPY ["src/SentinelAI.Agents/SentinelAI.Agents.csproj", "src/SentinelAI.Agents/"]
COPY ["src/SentinelAI.Api/SentinelAI.Api.csproj", "src/SentinelAI.Api/"]

# Restore dependencies
RUN dotnet restore "src/SentinelAI.Api/SentinelAI.Api.csproj"

# Copy remaining source files
COPY . .

# Build the application
WORKDIR "/src/src/SentinelAI.Api"
RUN dotnet build "SentinelAI.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SentinelAI.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published files
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

# Environment variables (will be overridden by docker-compose or k8s)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=Africa/Lagos

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "SentinelAI.Api.dll"]
