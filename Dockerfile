FROM mcr.microsoft.com/dotnet/aspnet:10.0

# The runtime image ships without curl, and HEALTHCHECK has to run inside the container.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Built by `npm run publish`, not here — keeps the private NuGet feed credential out of the image build.
COPY publish/ ./

# /data is the only writable path that matters: mount a volume there, or the database and the keys
# protecting every auth cookie die with the container.
RUN mkdir -p /data
VOLUME /data

ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__AppDb="Data Source=/data/fifth-box-server-manager.db" \
    Backup__Directory=/data/backups \
    DataProtection__KeyPath=/data/keys

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Host.dll"]
