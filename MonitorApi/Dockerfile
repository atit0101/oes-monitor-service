FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MonitorApi.csproj", "."]
RUN dotnet restore

# Copy source directories explicitly — หลีกเลี่ยง cache ที่ค้างจาก bin/obj
COPY Controllers/  Controllers/
COPY Data/         Data/
COPY Models/       Models/
COPY Services/     Services/
COPY Workers/      Workers/
COPY Program.cs    .
COPY appsettings.json .

RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app

# สร้าง folder สำหรับ SQLite data
RUN mkdir -p /app/data

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DataPath=/app/data

ENTRYPOINT ["dotnet", "MonitorApi.dll"]
