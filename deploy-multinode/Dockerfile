# syntax=docker/dockerfile:1
# ═══════════════════════════════════════════════════════════════════
# Multi-Stage-Image für einen CQRS-Cluster-Node.
#
# Parametrisiert über Build-Args, damit DASSELBE Dockerfile sowohl den
# gRPC-Host als auch den LoadHarness-Verifikationsnode baut:
#   PROJECT    — welches Projekt publiziert wird (Verzeichnis == .csproj-Name)
#   ENTRY_DLL  — welche DLL beim Start ausgeführt wird
#
# Build-Context = Repo-Root (die Projekte referenzieren einander).
# `dotnet publish <PROJECT>` zieht NUR den Abhängigkeitsgraphen des Ziels —
# Domain.Client (Blazor, baut aktuell nicht) ist NICHT im Graph von Host.Grpc
# oder LoadHarness und wird daher nicht gebaut.
# ═══════════════════════════════════════════════════════════════════

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT=Host.Grpc
WORKDIR /src
COPY . .
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
ARG ENTRY_DLL=Host.Grpc.dll
WORKDIR /app

# netcat für den TCP-Healthcheck (gRPC-Port offen == Node hochgefahren + Marten-Schema migriert).
RUN apt-get update \
    && apt-get install -y --no-install-recommends netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# WatchPath/PreprocessedPath müssen nicht vorab existieren (die Pipeline legt sie an),
# aber wir legen sie an, um die Warn-Logs zu vermeiden.
RUN mkdir -p /data/input /data/preprocessed

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_ENVIRONMENT=Production

# Entry-Skript verdrahtet ENTRY_DLL fest und reicht compose-`command:`-Args ("$@") durch
# (z. B. die LoadHarness-CLI-Flags). Host.Grpc braucht keine Args.
RUN printf '#!/bin/sh\nexec dotnet %s "$@"\n' "${ENTRY_DLL}" > /entrypoint.sh \
    && chmod +x /entrypoint.sh

EXPOSE 5001
ENTRYPOINT ["/entrypoint.sh"]
