# 04 - Frontend Setup (React + TypeScript)

## Cíl
Vytvořit React projekt s TypeScript, nakonfigurovat shadcn/ui a připravit API service layer pro komunikaci s backendem.

## Prerekvizity
- Dokončený krok 03 (Transactions API)
- Node.js 18+ nainstalován
- npm nebo yarn

## Kroky implementace

### 1. Vytvoření React projektu

```bash
# V root složce projektu (vedle TransactionManagement.Api)
npx create-react-app transaction-management-ui --template typescript
cd transaction-management-ui
```

### 2. Instalace dependencies

```bash
# Tailwind CSS
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p

# shadcn/ui dependencies
npm install class-variance-authority clsx tailwind-merge
npm install lucide-react
npm install @radix-ui/react-slot

# React Router
npm install react-router-dom

# Date handling
npm install date-fns

# API client
npm install axios
```

### 3. Konfigurace Tailwind CSS

**tailwind.config.js:**
```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: ["class"],
  content: [
    './pages/**/*.{ts,tsx}',
    './components/**/*.{ts,tsx}',
    './app/**/*.{ts,tsx}',
    './src/**/*.{ts,tsx}',
  ],
  theme: {
    container: {
      center: true,
      padding: "2rem",
      screens: {
        "2xl": "1400px",
      },
    },
    extend: {
      colors: {
        border: "hsl(var(--border))",
        input: "hsl(var(--input))",
        ring: "hsl(var(--ring))",
        background: "hsl(var(--background))",
        foreground: "hsl(var(--foreground))",
        primary: {
          DEFAULT: "hsl(var(--primary))",
          foreground: "hsl(var(--primary-foreground))",
        },
        secondary: {
          DEFAULT: "hsl(var(--secondary))",
          foreground: "hsl(var(--secondary-foreground))",
        },
        destructive: {
          DEFAULT: "hsl(var(--destructive))",
          foreground: "hsl(var(--destructive-foreground))",
        },
        muted: {
          DEFAULT: "hsl(var(--muted))",
          foreground: "hsl(var(--muted-foreground))",
        },
        accent: {
          DEFAULT: "hsl(var(--accent))",
          foreground: "hsl(var(--accent-foreground))",
        },
        popover: {
          DEFAULT: "hsl(var(--popover))",
          foreground: "hsl(var(--popover-foreground))",
        },
        card: {
          DEFAULT: "hsl(var(--card))",
          foreground: "hsl(var(--card-foreground))",
        },
      },
      borderRadius: {
        lg: "var(--radius)",
        md: "calc(var(--radius) - 2px)",
        sm: "calc(var(--radius) - 4px)",
      },
      keyframes: {
        "accordion-down": {
          from: { height: 0 },
          to: { height: "var(--radix-accordion-content-height)" },
        },
        "accordion-up": {
          from: { height: "var(--radix-accordion-content-height)" },
          to: { height: 0 },
        },
      },
      animation: {
        "accordion-down": "accordion-down 0.2s ease-out",
        "accordion-up": "accordion-up 0.2s ease-out",
      },
    },
  },
  plugins: [require("tailwindcss-animate")],
}
```

**src/index.css:**
```css
@tailwind base;
@tailwind components;
@tailwind utilities;

@layer base {
  :root {
    --background: 0 0% 100%;
    --foreground: 222.2 84% 4.9%;
    --card: 0 0% 100%;
    --card-foreground: 222.2 84% 4.9%;
    --popover: 0 0% 100%;
    --popover-foreground: 222.2 84% 4.9%;
    --primary: 222.2 47.4% 11.2%;
    --primary-foreground: 210 40% 98%;
    --secondary: 210 40% 96.1%;
    --secondary-foreground: 222.2 47.4% 11.2%;
    --muted: 210 40% 96.1%;
    --muted-foreground: 215.4 16.3% 46.9%;
    --accent: 210 40% 96.1%;
    --accent-foreground: 222.2 47.4% 11.2%;
    --destructive: 0 84.2% 60.2%;
    --destructive-foreground: 210 40% 98%;
    --border: 214.3 31.8% 91.4%;
    --input: 214.3 31.8% 91.4%;
    --ring: 222.2 84% 4.9%;
    --radius: 0.5rem;
  }

  .dark {
    --background: 222.2 84% 4.9%;
    --foreground: 210 40% 98%;
    --card: 222.2 84% 4.9%;
    --card-foreground: 210 40% 98%;
    --popover: 222.2 84% 4.9%;
    --popover-foreground: 210 40% 98%;
    --primary: 210 40% 98%;
    --primary-foreground: 222.2 47.4% 11.2%;
    --secondary: 217.2 32.6% 17.5%;
    --secondary-foreground: 210 40% 98%;
    --muted: 217.2 32.6% 17.5%;
    --muted-foreground: 215 20.2% 65.1%;
    --accent: 217.2 32.6% 17.5%;
    --accent-foreground: 210 40% 98%;
    --destructive: 0 62.8% 30.6%;
    --destructive-foreground: 210 40% 98%;
    --border: 217.2 32.6% 17.5%;
    --input: 217.2 32.6% 17.5%;
    --ring: 212.7 26.8% 83.9%;
  }
}

@layer base {
  * {
    @apply border-border;
  }
  body {
    @apply bg-background text-foreground;
  }
}
```

### 4. Instalace shadcn/ui komponenty

```bash
# Nainstaluj tailwindcss-animate
npm install -D tailwindcss-animate

# Přidej shadcn/ui utility
npx shadcn-ui@latest init
```

Při inicializaci zvol:
- TypeScript: Yes
- Style: Default
- Base color: Slate
- CSS variables: Yes

### 5. Utility pro className handling

**src/lib/utils.ts:**
```typescript
import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
```

### 6. API Types a DTOs

**src/types/api.ts:**
```typescript
export enum TransactionType {
  Income = 0,
  Expense = 1
}

export interface TransactionDto {
  id: string;
  description: string;
  amount: number;
  companyId: string;
  companyName?: string;
  transactionType: TransactionType;
  transactionDate: string;
  createdAt: string;
  updatedAt: string;
  attachmentCount: number;
}

export interface CreateTransactionDto {
  description: string;
  amount: number;
  companyId: string;
  companyName?: string;
  transactionType: TransactionType;
  transactionDate: string;
}

export interface UpdateTransactionDto {
  description: string;
  amount: number;
  companyId: string;
  companyName?: string;
  transactionType: TransactionType;
  transactionDate: string;
}
```

### 7. API Service Layer

**src/services/api-client.ts:**
```typescript
import axios from 'axios';

const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor pro logování
apiClient.interceptors.request.use(
  (config) => {
    console.log(`API Request: ${config.method?.toUpperCase()} ${config.url}`);
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor pro error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      console.error('API Error:', error.response.status, error.response.data);
    } else if (error.request) {
      console.error('Network Error:', error.message);
    }
    return Promise.reject(error);
  }
);
```

**src/services/transactions-service.ts:**
```typescript
import { apiClient } from './api-client';
import { TransactionDto, CreateTransactionDto, UpdateTransactionDto } from '../types/api';

export const transactionsService = {
  async getAll(): Promise<TransactionDto[]> {
    const response = await apiClient.get<TransactionDto[]>('/api/transactions');
    return response.data;
  },

  async getById(id: string): Promise<TransactionDto> {
    const response = await apiClient.get<TransactionDto>(`/api/transactions/${id}`);
    return response.data;
  },

  async create(dto: CreateTransactionDto): Promise<TransactionDto> {
    const response = await apiClient.post<TransactionDto>('/api/transactions', dto);
    return response.data;
  },

  async update(id: string, dto: UpdateTransactionDto): Promise<TransactionDto> {
    const response = await apiClient.put<TransactionDto>(`/api/transactions/${id}`, dto);
    return response.data;
  },

  async delete(id: string): Promise<void> {
    await apiClient.delete(`/api/transactions/${id}`);
  }
};
```

### 8. Environment konfigurace

**.env:**
```
REACT_APP_API_URL=http://localhost:5000
```

**.env.development:**
```
REACT_APP_API_URL=http://localhost:5000
```

### 9. Základní App struktura

**src/App.tsx:**
```tsx
import React from 'react';
import { BrowserRouter as Router } from 'react-router-dom';

function App() {
  return (
    <Router>
      <div className="min-h-screen bg-background">
        <header className="border-b">
          <div className="container mx-auto px-4 py-4">
            <h1 className="text-2xl font-bold">Transaction Management</h1>
          </div>
        </header>
        <main className="container mx-auto px-4 py-8">
          <p className="text-muted-foreground">Frontend připraven. Pokračuj krokem 05.</p>
        </main>
      </div>
    </Router>
  );
}

export default App;
```

### 10. Package.json scripts

Ověř, že máš v **package.json**:
```json
{
  "scripts": {
    "start": "react-scripts start",
    "build": "react-scripts build",
    "test": "react-scripts test",
    "eject": "react-scripts eject"
  }
}
```

## Testování setupu

### 1. Spuštění frontendu

```bash
npm start
```

Aplikace by měla běžet na http://localhost:3000

### 2. Ověření API komunikace

V prohlížeči otevři console a spusť:

```javascript
// Test API connection
fetch('http://localhost:5000/api/transactions')
  .then(r => r.json())
  .then(console.log)
  .catch(console.error)
```

Pokud vidíš CORS error, ověř že backend má správně nakonfigurovaný CORS (viz krok 01).

### 3. Test API service

Vytvoř testovací komponentu pro ověření:

**src/components/ApiTest.tsx:**
```tsx
import React, { useEffect, useState } from 'react';
import { transactionsService } from '../services/transactions-service';
import { TransactionDto } from '../types/api';

export const ApiTest: React.FC = () => {
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    transactionsService.getAll()
      .then(setTransactions)
      .catch(err => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div>Načítám...</div>;
  if (error) return <div className="text-destructive">Error: {error}</div>;

  return (
    <div>
      <h2 className="text-xl font-semibold mb-4">API Test - Transakce</h2>
      <div className="space-y-2">
        {transactions.length === 0 ? (
          <p className="text-muted-foreground">Žádné transakce</p>
        ) : (
          transactions.map(t => (
            <div key={t.id} className="p-4 border rounded">
              <p className="font-medium">{t.description}</p>
              <p className="text-sm text-muted-foreground">
                {t.amount} Kč - {t.companyName}
              </p>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
```

Použij v **src/App.tsx**:
```tsx
import { ApiTest } from './components/ApiTest';

// V main:
<main className="container mx-auto px-4 py-8">
  <ApiTest />
</main>
```

## Ověření

Po dokončení:

1. ✅ React aplikace běží na http://localhost:3000
2. ✅ Tailwind CSS styly fungují
3. ✅ API client se úspěšně připojuje k backendu
4. ✅ Lze načíst seznam transakcí přes API
5. ✅ TypeScript kompiluje bez chyb
6. ✅ shadcn/ui je správně nakonfigurováno

## Docker Compose Setup

### Dockerfile pro Frontend

**transaction-management-ui/Dockerfile:**
```dockerfile
FROM node:18-alpine as build

WORKDIR /app

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source
COPY . .

# Build
RUN npm run build

# Production image
FROM nginx:alpine
COPY --from=build /app/build /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

**transaction-management-ui/nginx.conf:**
```nginx
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://backend:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

**transaction-management-ui/.dockerignore:**
```
node_modules
build
.git
.env.local
*.log
```

### Docker Compose v1 (Backend + Frontend + PostgreSQL)

**docker-compose.yml** (v root složce projektu):
```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    container_name: postgres-transactions
    environment:
      POSTGRES_DB: transactionsdb
      POSTGRES_USER: appuser
      POSTGRES_PASSWORD: AppPassword123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U appuser -d transactionsdb"]
      interval: 10s
      timeout: 5s
      retries: 5

  backend:
    build:
      context: ./TransactionManagement.Api
      dockerfile: Dockerfile
    container_name: backend-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=transactionsdb;Username=appuser;Password=AppPassword123
    ports:
      - "5000:5000"
    depends_on:
      postgres:
        condition: service_healthy
    volumes:
      - ./TransactionManagement.Api/uploads:/app/uploads

  frontend:
    build:
      context: ./transaction-management-ui
      dockerfile: Dockerfile
    container_name: frontend-ui
    ports:
      - "3000:80"
    depends_on:
      - backend
    environment:
      - REACT_APP_API_URL=http://localhost:5000/api

volumes:
  postgres_data:
```

### Dockerfile pro Backend

**TransactionManagement.Api/Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TransactionManagement.Api.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransactionManagement.Api.dll"]
```

**TransactionManagement.Api/.dockerignore:**
```
bin/
obj/
*.user
*.suo
.vs/
.vscode/
```

### Spuštění celého stacku

```bash
# Build a spuštění všech služeb
docker-compose up --build

# Spuštění na pozadí
docker-compose up -d --build

# Sledování logů
docker-compose logs -f

# Zastavení
docker-compose down

# Zastavení včetně volumes (smaže databázi)
docker-compose down -v
```

### Přístup k aplikaci

Po spuštění `docker-compose up`:

- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger
- **PostgreSQL**: localhost:5432

### Výhody Docker Compose setupu

✅ **Jedno příkazové spuštění** - `docker-compose up`
✅ **Izolované prostředí** - žádné konflikty s lokálními službami
✅ **Konzistentní prostředí** - stejné pro všechny vývojáře
✅ **Automatické dependency management** - služby startují ve správném pořadí
✅ **Hot-reload** - změny kódu se automaticky projeví (dev mode)

### Development mode (s hot-reload)

Pro development můžete použít override:

**docker-compose.dev.yml:**
```yaml
version: '3.8'

services:
  backend:
    build:
      context: ./TransactionManagement.Api
      target: build
    volumes:
      - ./TransactionManagement.Api:/src
    command: dotnet watch run --project /src/TransactionManagement.Api.csproj

  frontend:
    build:
      context: ./transaction-management-ui
      target: build
    volumes:
      - ./transaction-management-ui/src:/app/src
      - ./transaction-management-ui/public:/app/public
    command: npm start
    environment:
      - REACT_APP_API_URL=http://localhost:5000/api
      - CHOKIDAR_USEPOLLING=true
```

Spuštění dev módu:
```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up
```

## Výstup této fáze

✅ React projekt s TypeScript
✅ Tailwind CSS + shadcn/ui konfigurace
✅ API service layer (axios)
✅ TypeScript typy pro API komunikaci
✅ Základní routing structure
✅ Environment konfigurace
✅ Funkční komunikace s backendem
✅ **Docker Compose setup pro Backend + Frontend + PostgreSQL**
✅ **Jedno-příkazové spuštění celého stacku**

## Další krok

→ **05_frontend_transakce.md** - Implementace UI pro správu transakcí (seznam, vytvoření, editace, smazání)
