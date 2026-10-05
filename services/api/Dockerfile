# Dashboard image: the Angular app and the API in one container, one origin.
# Build from the repo root:  docker compose up -d --build web

# 1. Build the Angular app.
FROM node:22-alpine AS web
WORKDIR /web
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci
COPY apps/web/ ./
RUN npx ng build

# 2. Build the API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY services/core/ services/core/
COPY services/decisions/ services/decisions/
COPY services/api/ services/api/
RUN dotnet publish services/api -c Release -o /app

# 3. Runtime: the API, with the Angular build as its static files.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app .
COPY --from=web /web/dist/web/browser ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
# The base image ships a non-root "app" user; nothing here needs root.
USER app
ENTRYPOINT ["dotnet", "SignalGarden.Api.dll"]
