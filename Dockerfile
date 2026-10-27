# Imagem única: o Angular compilado é servido pela própria API (mesma origem, sem CORS),
# o que mantém o cookie SameSite=Strict e a checagem de Origin funcionando em produção.

# ---------- Front-end ----------
FROM node:24-alpine AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---------- API ----------
# global.json não é copiado: a imagem do SDK define a versão 10.0.x usada no build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/RadarPlural.Api/RadarPlural.Api.csproj backend/RadarPlural.Api/
RUN dotnet restore backend/RadarPlural.Api/RadarPlural.Api.csproj
COPY backend/RadarPlural.Api/ backend/RadarPlural.Api/
RUN dotnet publish backend/RadarPlural.Api/RadarPlural.Api.csproj -c Release -o /app --no-restore

# ---------- Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app .
COPY --from=web /web/dist/radarplural/browser ./wwwroot
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RadarPlural.Api.dll"]
