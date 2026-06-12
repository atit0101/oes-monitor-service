FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MonitorApi.csproj", "."]
RUN dotnet restore

COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app

# สร้าง folder สำหรับ SQLite data
RUN mkdir -p /app/data

COPY --from=publish /app/publish .

# EF Core migration tool
RUN dotnet tool install --global dotnet-ef 2>/dev/null || true

ENV ASPNETCORE_URLS=http://+:8080
ENV DataPath=/app/data

ENTRYPOINT ["dotnet", "MonitorApi.dll"]
