# Game engine worker image.
#
# Built from the repository root as the build context:
#   docker build -f src/GameEngine/HexMaster.ThePrey.GameEngine/Dockerfile .
#
# We use an explicit Dockerfile (instead of `dotnet publish /t:PublishContainer`)
# so we can install libgssapi-krb5-2. The .NET HTTP stack lazily loads
# libgssapi_krb5.so.2 on the first outbound HttpClient request, and the slim
# runtime base image omits it — without this package the engine crashes with
# "libgssapi_krb5.so.2: cannot open shared object file".

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0
WORKDIR /src

# Copy NuGet config and source, then restore + publish the worker.
COPY nuget.config ./
COPY src/ ./src/

RUN dotnet publish src/GameEngine/HexMaster.ThePrey.GameEngine/HexMaster.ThePrey.GameEngine.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:Version=$VERSION \
    /p:AssemblyVersion=$VERSION \
    /p:InformationalVersion=$VERSION

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

# Kerberos runtime — required by System.Net.Http for outbound calls on Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

USER app
ENTRYPOINT ["dotnet", "HexMaster.ThePrey.GameEngine.dll"]
