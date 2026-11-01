# The image of the application (research R13): Node builds the Angular app, the .NET SDK publishes the host and builds
# the migration bundles, the runtime stage is the default ASP.NET Core 10 image (not Alpine, not Chiseled, no invariant
# globalization: it keeps tzdata and ICU for the time zones, research R6). The context is the repository root.

# --- The Angular build. `npm run build` runs fetch-fonts.sh first: it needs github.com and unzip, and fails on a font
# whose SHA-256 sum differs (research R24). zip is not needed here: only the font fetch's own test uses it.
FROM node:24 AS frontend
RUN apt-get update \
    && apt-get install --no-install-recommends --yes unzip \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# --- The host and the migration bundles. dotnet-ef is the local tool of backend/.config/dotnet-tools.json (pinned).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY global.json ./
COPY backend/ backend/
# The bundles build the host's services, which read the translation files' folder.
COPY frontend/public/i18n/ frontend/public/i18n/
WORKDIR /src/backend
RUN dotnet tool restore
RUN dotnet publish src/HabitsTracker.Host/HabitsTracker.Host.csproj --configuration Release --output /out/app
ENV DOTNET_ENVIRONMENT=Development Database__MigrateOnStart=false Translations__Path=/src/frontend/public/i18n
RUN dotnet tool run dotnet-ef -- migrations bundle --configuration Release --context IdentityDbContext \
      --project src/HabitsTracker.Identity.Adapters --startup-project src/HabitsTracker.Host \
      --output /out/app/migrate-identity --force
RUN dotnet tool run dotnet-ef -- migrations bundle --configuration Release --context OperationsDbContext \
      --project src/HabitsTracker.Host --startup-project src/HabitsTracker.Host \
      --output /out/app/migrate-operations --force

# --- The runtime.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /out/app ./
COPY --from=frontend /src/frontend/dist/habits-tracker/browser ./wwwroot
# The translation files are served from wwwroot/i18n and read by the host from the same folder (relative to /app).
ENV Translations__Path=wwwroot/i18n
USER $APP_UID
EXPOSE 8080
CMD ["dotnet", "HabitsTracker.Host.dll"]
