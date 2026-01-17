# =============================================================================
# Stage 1: Base runtime with ipmitool
# =============================================================================
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
LABEL maintainer="jdmallen" \
      description="Dell IPMI Fan Control Monitor" \
      org.opencontainers.image.source="https://github.com/jdmallen/dell-ipmi-fan-control-monitor"

WORKDIR /app

# Install ipmitool in a single layer, clean up apt cache to reduce image size
# Using --no-install-recommends to minimize installed packages
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ipmitool \
        libcap2-bin \
        procps && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for running the application
RUN groupadd --gid 1000 appuser && \
    useradd --uid 1000 --gid 1000 -m appuser

# Grant ipmitool the CAP_NET_RAW capability so it can work without root
# This allows IPMI over LAN to function with raw socket access
RUN setcap cap_net_raw+ep /usr/sbin/ipmitool

# =============================================================================
# Stage 2: Build
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy only the csproj for better Docker layer caching
# NuGet restore is cached unless csproj changes
COPY src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj src/JDMallen.IPMITempMonitor/
RUN dotnet restore src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj

# Copy all remaining source files
COPY . .

# Build the project
RUN dotnet build src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj \
    -c Release -o /app/build --no-restore

# =============================================================================
# Stage 3: Publish
# =============================================================================
FROM build AS publish
RUN dotnet publish src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj \
    -c Release -o /app/publish --no-restore

# =============================================================================
# Stage 4: Final runtime image
# =============================================================================
FROM base AS final
WORKDIR /app

# Copy published application from publish stage
COPY --from=publish /app/publish .

# Set ownership to non-root user
RUN chown -R appuser:appuser /app

# Switch to non-root user for security
# ipmitool will still work due to CAP_NET_RAW capability set earlier
USER appuser

# Health check - verify the process is running
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD pgrep -f "JDMallen.IPMITempMonitor" || exit 1

ENTRYPOINT ["dotnet", "JDMallen.IPMITempMonitor.dll"]
