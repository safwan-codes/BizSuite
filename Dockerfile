# ─── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (layer cache optimization)
COPY ["BizSuite.csproj", "./"]
RUN dotnet restore "BizSuite.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "BizSuite.csproj" -c Release -o /app/build

# ─── Publish stage ───────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "BizSuite.csproj" -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Runtime stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN addgroup --system --gid 1001 bizsuite \
 && adduser  --system --uid 1001 --ingroup bizsuite bizsuite

# Copy published output
COPY --from=publish /app/publish .

# Ensure log directory exists (used by web.config stdout logging)
RUN mkdir -p /app/logs && chown -R bizsuite:bizsuite /app

USER bizsuite

# ASP.NET Core listens on port 8080 in container
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "BizSuite.dll"]
