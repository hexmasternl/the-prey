FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY src/ src/
RUN dotnet publish src/Games/HexMaster.ThePrey.Games.Api/HexMaster.ThePrey.Games.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
RUN addgroup --system --gid 1001 appgroup \
    && adduser --system --uid 1001 --ingroup appgroup --no-create-home appuser
COPY --from=build /app/publish .
USER appuser
ENTRYPOINT ["dotnet", "HexMaster.ThePrey.Games.Api.dll"]
