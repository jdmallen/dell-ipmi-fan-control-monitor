# =============================================================================
# Stage 1: Build & publish
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy only the csproj first so NuGet restore is cached unless dependencies change.
COPY src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj src/JDMallen.IPMITempMonitor/
RUN dotnet restore src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj

# Copy the rest of the source and publish a framework-dependent build (the final
# stage already carries the matching .NET runtime, so no need to self-contain).
COPY . .
RUN dotnet publish src/JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj \
    -c Release -o /app/publish --no-restore

# =============================================================================
# Stage 2: Final runtime image
# =============================================================================
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
LABEL maintainer="jdmallen" \
      description="Dell IPMI Fan Control Monitor" \
      org.opencontainers.image.source="https://github.com/jdmallen/dell-ipmi-fan-control-monitor"

WORKDIR /app

# ipmitool is the only OS dependency; procps supplies pgrep for the health check.
# No CAP_NET_RAW is required: the app talks to the iDRAC over the network with
# `ipmitool -I lanplus`, which uses an ordinary UDP socket (port 623), not raw
# sockets. Clean up the apt cache to keep the layer small.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ipmitool \
        procps && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy the published application from the build stage.
COPY --from=build /app/publish .

# Run as the non-root user that the .NET runtime image already provides (UID
# 1654). The app only reads its files and writes logs to stdout, so it needs no
# ownership changes under /app.
USER $APP_UID

# Verify the worker process is alive (matches `dotnet JDMallen.IPMITempMonitor.dll`).
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD pgrep -f "JDMallen.IPMITempMonitor" || exit 1

ENTRYPOINT ["dotnet", "JDMallen.IPMITempMonitor.dll"]
