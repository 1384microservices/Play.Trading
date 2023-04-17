FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 5006

ENV ASPNETCORE_URLS=http://+:5006

# Creates a non-root user with an explicit UID and adds permission to access the /app folder
# For more info, please refer to https://aka.ms/vscode-docker-dotnet-configure-containers
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

ARG GITHUB_PAT

COPY ["src/Play.Trading.Service/Play.Trading.Service.csproj", "src/Play.Trading.Service/"]
COPY ["src/Play.Trading.Service/nuget.config", "src/Play.Trading.Service/"]

RUN dotnet restore "src/Play.Trading.Service/Play.Trading.Service.csproj"

COPY ["./src", "./src"]

WORKDIR "/src/Play.Trading.Service"
RUN dotnet publish "Play.Trading.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Play.Trading.Service.dll"]
