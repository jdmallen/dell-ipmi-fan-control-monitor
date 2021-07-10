FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app
MAINTAINER jdmallen
RUN apt-get update && apt-get dist-upgrade -y
RUN apt-get -y install ipmitool

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj", "JDMallen.IPMITempMonitor/"]
RUN dotnet restore "JDMallen.IPMITempMonitor/JDMallen.IPMITempMonitor.csproj"
COPY . .
WORKDIR "/src/JDMallen.IPMITempMonitor"
RUN dotnet build "JDMallen.IPMITempMonitor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "JDMallen.IPMITempMonitor.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JDMallen.IPMITempMonitor.dll"]
