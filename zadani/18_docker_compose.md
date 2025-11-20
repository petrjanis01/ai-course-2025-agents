# 11 - Docker Compose Setup

## Cíl
Dockerizovat celou aplikaci a vytvořit docker-compose.yml pro jednoduché spuštění všech služeb.

## Prerekvizity
- Dokončený krok 15 (frontend)
- Docker a Docker Compose nainstalováno

## Kroky implementace

### 1. Backend Dockerfile

**backend/Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["TransactionManagement.Api/TransactionManagement.Api.csproj", "TransactionManagement.Api/"]
RUN dotnet restore "TransactionManagement.Api/TransactionManagement.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/TransactionManagement.Api"
RUN dotnet build "TransactionManagement.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransactionManagement.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransactionManagement.Api.dll"]
```

### 2. Frontend Dockerfile

**frontend/Dockerfile:**
```dockerfile
# Build stage
FROM node:18-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source code
COPY . .

# Build app
RUN npm run build

# Production stage
FROM nginx:alpine
COPY --from=build /app/build /usr/share/nginx/html

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

**frontend/nginx.conf:**
```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    # Enable gzip
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;

    # SPA routing - return index.html for all routes
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

### 3. docker-compose.yml

**docker-compose.yml:**
```yaml
version: '3.8'

services:
  # PostgreSQL Database (shared for both app and Langfuse)
  postgres:
    image: postgres:16-alpine
    container_name: transactions-postgres
    environment:
      POSTGRES_USER: appuser
      POSTGRES_PASSWORD: apppass123
      # Create both databases on init
      POSTGRES_MULTIPLE_DATABASES: transactionsdb,langfusedb
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./docker/postgres-init.sh:/docker-entrypoint-initdb.d/init-databases.sh
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U appuser"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - transactions-network

  # Qdrant Vector Database
  qdrant:
    image: qdrant/qdrant:latest
    container_name: transactions-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - qdrant_data:/qdrant/storage
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - transactions-network

  # Langfuse (LLM Monitoring)
  langfuse:
    image: langfuse/langfuse:latest
    container_name: transactions-langfuse
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      DATABASE_URL: postgresql://appuser:apppass123@postgres:5432/langfusedb
      NEXTAUTH_URL: http://localhost:3030
      NEXTAUTH_SECRET: mysecretkey123456789012345678901234567890
      SALT: mysaltkey123456789012345678901234567890
    ports:
      - "3030:3000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/api/health"]
      interval: 30s
      timeout: 10s
      retries: 5
    networks:
      - transactions-network

  # .NET API
  api:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: transactions-api
    depends_on:
      postgres:
        condition: service_healthy
      qdrant:
        condition: service_healthy
      langfuse:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_HTTP_PORTS: 8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=transactionsdb;Username=appuser;Password=apppass123"
      Qdrant__Url: http://qdrant:6333
      Qdrant__CollectionName: transaction_documents
      Qdrant__VectorSize: 384
      Langfuse__PublicKey: ${LANGFUSE_PUBLIC_KEY:-}
      Langfuse__SecretKey: ${LANGFUSE_SECRET_KEY:-}
      Langfuse__BaseUrl: http://langfuse:3000
      LLM__BaseUrl: ${OLLAMA_BASE_URL:-http://host.docker.internal:11434}
      LLM__Model: ${LLM_MODEL:-llama3.1:8b}
      Embedding__Model: ${EMBEDDING_MODEL:-all-minilm:l6-v2}
      FileStorage__BasePath: /app/data/attachments
    ports:
      - "5000:8080"
    volumes:
      - attachment_data:/app/data/attachments
    networks:
      - transactions-network

  # React Frontend
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: transactions-frontend
    depends_on:
      - api
    environment:
      REACT_APP_API_URL: http://localhost:5000/api
    ports:
      - "3000:80"
    networks:
      - transactions-network

volumes:
  postgres_data:
  qdrant_data:
  attachment_data:

networks:
  transactions-network:
    driver: bridge
```

### 4. PostgreSQL Init Script

Vytvoř složku `docker` a v ní soubor pro inicializaci databází:

**docker/postgres-init.sh:**
```bash
#!/bin/bash
set -e

# Create databases
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE transactionsdb;
    CREATE DATABASE langfusedb;
EOSQL

echo "Databases 'transactionsdb' and 'langfusedb' created successfully"
```

Udělej soubor spustitelný:
```bash
chmod +x docker/postgres-init.sh
```

**Poznámka:** Tento script se automaticky spustí při prvním startu PostgreSQL containeru a vytvoří obě databáze pod stejným uživatelem `appuser`.

### 5. .env file

**.env:**
```env
# Langfuse API Keys (získat z Langfuse UI po prvním spuštění)
LANGFUSE_PUBLIC_KEY=pk-lf-xxx
LANGFUSE_SECRET_KEY=sk-lf-xxx

# LLM Configuration
# IMPORTANT: Ollama musí běžet na hostu (ne v Dockeru)
OLLAMA_BASE_URL=http://host.docker.internal:11434
LLM_MODEL=llama3.1:8b
EMBEDDING_MODEL=all-minilm:l6-v2
```

### 6. .dockerignore files

**backend/.dockerignore:**
```
**/bin
**/obj
**/out
**/.vs
**/.vscode
**/node_modules
**/.git
**/.gitignore
**/README.md
**/docker-compose.yml
**/Dockerfile
```

**frontend/.dockerignore:**
```
node_modules
build
.git
.gitignore
README.md
docker-compose.yml
Dockerfile
.env.local
.env.development.local
.env.test.local
.env.production.local
npm-debug.log*
yarn-debug.log*
yarn-error.log*
```

### 7. README pro spuštění

**README.md:**
```markdown
# Transaction Management POC

## Prerekvizity

1. **Docker Desktop** nainstalován a běžící
2. **Ollama** nainstalován a běžící na hostu:
   ```bash
   ollama serve
   ```
3. **LLM modely** stažené:
   ```bash
   ollama pull llama3.1:8b
   ollama pull all-minilm:l6-v2
   ```

## Spuštění aplikace

### 1. Úvodní setup

```bash
# Clone repository
git clone <repository-url>
cd transaction-management

# Vytvoř .env file
cp .env.example .env
```

### 2. První spuštění

```bash
# Build a spusť všechny služby
docker-compose up -d --build

# Sleduj logy
docker-compose logs -f
```

### 3. Získání Langfuse API klíčů

Po prvním spuštění:

1. Otevři http://localhost:3030
2. Zaregistruj se (první uživatel je admin)
3. Vytvoř nový projekt
4. Přejdi do Settings → API Keys
5. Zkopíruj Public Key a Secret Key

Aktualizuj `.env`:
```env
LANGFUSE_PUBLIC_KEY=pk-lf-your-key
LANGFUSE_SECRET_KEY=sk-lf-your-key
```

Restartuj API:
```bash
docker-compose restart api
```

### 4. Seed dat (volitelné)

```bash
curl -X POST http://localhost:5000/api/data/seed
```

## Přístup k službám

- **Frontend**: http://localhost:3000
- **API**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger
- **Langfuse**: http://localhost:3030
- **Qdrant Dashboard**: http://localhost:6333/dashboard

## Zastavení

```bash
# Zastavit služby
docker-compose down

# Zastavit a smazat volumes (POZOR: smaže všechna data!)
docker-compose down -v
```

## Troubleshooting

### API nemůže kontaktovat Ollama

Ujisti se, že:
1. Ollama běží na hostu: `ollama serve`
2. Modely jsou stažené: `ollama list`
3. URL v `.env` je `http://host.docker.internal:11434`

### Langfuse nefunguje

1. Ověř, že PostgreSQL je healthy: `docker-compose ps`
2. Zkontroluj, že databáze `langfusedb` existuje:
   ```bash
   docker exec -it transactions-postgres psql -U appuser -l
   ```
3. Zkontroluj logy: `docker-compose logs langfuse`
4. Ověř API klíče v `.env`

### PostgreSQL databáze neinicializovaly správně

Pokud je potřeba reinicializovat:
```bash
# Zastav a smaž volumes
docker-compose down -v

# Ujisti se, že init script má správná práva
chmod +x docker/postgres-init.sh

# Spusť znovu
docker-compose up -d
```

### Frontend nemůže kontaktovat API

1. Ověř, že API běží: `curl http://localhost:5000/api/transactions`
2. Zkontroluj CORS nastavení v API
```

## Testování Docker setupu

### 1. Build images

```bash
docker-compose build
```

### 2. Spuštění

```bash
docker-compose up -d
```

### 3. Sledování logů

```bash
# Všechny služby
docker-compose logs -f

# Pouze API
docker-compose logs -f api

# Pouze frontend
docker-compose logs -f frontend
```

### 4. Ověření health checks

```bash
docker-compose ps
```

Všechny služby by měly mít status `healthy`.

### 5. Testování endpointů

```bash
# Frontend
curl http://localhost:3000

# API
curl http://localhost:5000/api/transactions

# Langfuse
curl http://localhost:3030/api/health

# Qdrant
curl http://localhost:6333/health
```

### 6. Restart služeb

```bash
# Restartovat vše
docker-compose restart

# Restartovat pouze API
docker-compose restart api
```

## Docker Compose Commands Reference

```bash
# Build bez cache
docker-compose build --no-cache

# Spustit konkrétní službu
docker-compose up -d postgres

# Zobraz logy od určitého času
docker-compose logs --since 10m

# Exec do containeru
docker-compose exec api bash

# Zobraz volumes
docker volume ls

# Vyčistění
docker-compose down -v --remove-orphans
docker system prune -a
```

## Ověření

Po dokončení:

1. ✅ Všechny služby běží v Dockeru
2. ✅ Frontend je dostupný na portu 3000
3. ✅ API je dostupné na portu 5000
4. ✅ Health checks všech služeb fungují
5. ✅ Volumes pro perzistentní data
6. ✅ Network komunikace mezi services
7. ✅ API může kontaktovat Ollama na hostu

## Výstup této fáze

✅ Dockerfile pro .NET API
✅ Dockerfile pro React frontend
✅ docker-compose.yml s 5 services (optimalizováno - 1 PostgreSQL)
✅ Health checks pro všechny služby
✅ Volumes pro perzistenci
✅ Network izolace
✅ README s instrukcemi

## Další krok

→ **12_testovani.md** - Kompletní testovací scénáře pro validaci POC projektu
