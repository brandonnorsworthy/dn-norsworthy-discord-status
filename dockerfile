# =========================
# Build stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the csproj first to maximize restore caching
COPY StatusImageCard.csproj ./
RUN dotnet restore ./StatusImageCard.csproj

# Copy the rest of the source
COPY . ./

# Publish the project explicitly (no ambiguity)
RUN dotnet publish ./StatusImageCard.csproj -c Release -o /out /p:UseAppHost=false

# =========================
# Runtime stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Fonts for SkiaSharp text rendering
RUN apt-get update \
  && apt-get install -y --no-install-recommends fontconfig fonts-dejavu-core \
  && rm -rf /var/lib/apt/lists/*

COPY --from=build /out ./

ENV ASPNETCORE_URLS=http://+:3500
EXPOSE 3500

ENTRYPOINT ["dotnet", "StatusImageCard.dll"]
