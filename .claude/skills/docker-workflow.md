# Docker Workflow Skill

## Local Dev Stack

```bash
# Start toàn bộ stack
docker compose up

# Start chỉ backend (Umbraco)
docker compose up backend

# Start chỉ frontend (Next.js)
docker compose up frontend

# Start với rebuild
docker compose up --build

# Start ở background
docker compose up -d

# Stop tất cả
docker compose down

# Stop và xóa volumes (reset DB)
docker compose down -v
```

## Build Images

```bash
# Build backend image
docker build -f infra/docker/Dockerfile.backend -t dcms-backend .

# Build frontend image
docker build -f infra/docker/Dockerfile.frontend -t dcms-frontend .

# Build tất cả qua compose
docker compose build
```

## Debug Containers

```bash
# Xem logs của service
docker compose logs backend
docker compose logs frontend
docker compose logs -f backend   # follow mode

# Vào container để debug
docker compose exec backend bash
docker compose exec frontend sh

# Xem trạng thái containers
docker compose ps

# Xem resource usage
docker stats
```

## Services trong docker-compose.yml

| Service | Port | Mô tả |
|---|---|---|
| `backend` | 8080 | Umbraco CMS (ASP.NET Core) |
| `frontend` | 3000 | Next.js storefront |
| `db` | 1433 | SQL Server |
| `elasticsearch` | 9200 | Elasticsearch |
| `kibana` | 5601 | Kibana (dev only, debug ES) |

## Dockerfile Patterns

**Backend (ASP.NET Core):**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/backend/dCMS.Web/dCMS.Web.csproj", "dCMS.Web/"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "dCMS.Web.dll"]
```

**Frontend (Next.js):**
```dockerfile
FROM node:20-alpine AS base
FROM base AS deps
WORKDIR /app
COPY src/frontend/package*.json ./
RUN npm ci
FROM base AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY src/frontend/ .
RUN npm run build
FROM base AS runner
WORKDIR /app
ENV NODE_ENV production
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
EXPOSE 3000
CMD ["node", "server.js"]
```

## CI/CD với GitHub Actions

Workflow tự động:
1. Push to any branch → run tests
2. Push to `main` → build Docker images + push to registry + deploy to staging
3. Tag release → deploy to production
