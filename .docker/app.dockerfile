FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS base

RUN apt-get update \
    && apt-get install -y --no-install-recommends libc6-dev

FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build

WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o publish

FROM base AS final

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "App.dll"]