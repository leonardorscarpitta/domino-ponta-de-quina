# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_USE_POLLING_FILE_WATCHER=1 \
    DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1 \
    DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1 \
    ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
CMD ["dotnet", "watch", "--project", "src/DominoPontaDeQuina.Web", "run", "--no-launch-profile"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DominoPontaDeQuina.sln ./
COPY src/DominoPontaDeQuina.Web/DominoPontaDeQuina.Web.csproj src/DominoPontaDeQuina.Web/
COPY tests/DominoPontaDeQuina.Tests/DominoPontaDeQuina.Tests.csproj tests/DominoPontaDeQuina.Tests/
RUN dotnet restore
COPY . .
RUN dotnet test -c Release --no-restore \
 && dotnet publish src/DominoPontaDeQuina.Web -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    Armazenamento__Diretorio=/app/data
COPY --from=build /app .
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
VOLUME /app/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "DominoPontaDeQuina.Web.dll"]
