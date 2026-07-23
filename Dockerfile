FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GithubScout.sln .
COPY src/GithubScout.Core/GithubScout.Core.csproj src/GithubScout.Core/
COPY src/GithubScout.Web/GithubScout.Web.csproj src/GithubScout.Web/
COPY tests/GithubScout.Tests/GithubScout.Tests.csproj tests/GithubScout.Tests/
RUN dotnet restore src/GithubScout.Web/GithubScout.Web.csproj

COPY src/ src/
RUN dotnet publish src/GithubScout.Web/GithubScout.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Signals to GithubScout that keep-awake should no-op: a container has no access
# to the host machine's power/sleep state.
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "GithubScout.Web.dll"]
