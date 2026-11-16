# Build stage: restore from the committed lock file, then publish.
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Project file and lock file first, so the restore layer is cached until dependencies change.
COPY StatusImageCard.csproj packages.lock.json ./
RUN dotnet restore StatusImageCard.csproj --locked-mode

COPY . ./
RUN dotnet publish StatusImageCard.csproj -c Release -o /out --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# fontconfig and a font for SkiaSharp/OxyPlot text rendering.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /out ./

# /app/data holds message-id.txt (the Discord message to keep editing). It is the only
# place the app writes; mount a volume here. Owned by the image's non-root user so the
# default user works without a mount; a stack running as another UID bind-mounts a
# directory that UID owns.
RUN mkdir -p /app/data && chown "$APP_UID" /app/data

# .NET's default container port (ASPNETCORE_HTTP_PORTS=8080 in the base image).
EXPOSE 8080

# Non-root user that ships with the .NET images (UID 1654).
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 \
    CMD ["dotnet", "/app/StatusImageCard.dll", "healthcheck"]

ENTRYPOINT ["dotnet", "StatusImageCard.dll"]
