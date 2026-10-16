# Knowledge gateway. Build from the repository root:  docker build -t knowledge-gateway .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/Knowledge.Gateway/Knowledge.Gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# The policy is part of the image: a policy change is a reviewed pull request and a redeploy.
COPY policy/ policy/

# Non-root. (The base image provides the "app" user.)
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Knowledge.Gateway.dll"]

# Stdio upstreams such as Graphify need their runtime in the image, for example:
#   FROM runtime AS with-python
#   USER root
#   RUN apt-get update && apt-get install -y --no-install-recommends python3 python3-pip && rm -rf /var/lib/apt/lists/*
#   USER app
