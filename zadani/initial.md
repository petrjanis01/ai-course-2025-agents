# POC Projekt - Order Management s AI Chat

## 1. Přehled projektu

### 1.1 Cíl projektu
POC projekt pro vyzkoušení a pochopení následujících technologií v rámci komplexní aplikace:
- Microsoft Agent Framework (v .NET)
- Langfuse (monitoring a tracing LLM calls)
- RAG (Retrieval Augmented Generation)
- Vektorová databáze Qdrant + embeddings
- Volitelně MCP (Model Context Protocol)

### 1.2 Popis aplikace
Aplikace pro správu finančních transakcí s možností nahrávání příloh (faktury, smlouvy, objednávky) a inteligentním chatem, který umí odpovídat na dotazy nad daty transakcí i obsahem dokumentů.

### 1.3 Klíčové vlastnosti
- Správa finančních transakcí (CRUD operace)
- Nahrávání a správa příloh ve formátu Markdown
- AI-powered chat s RAG přístupem
- Automatické zpracování dokumentů (kategorizace, indexování, chunking s překryvem)
- Generování vzorových dokumentů (faktury, smlouvy, objednávky)
- Monitoring všech LLM interakcí přes Langfuse
- Embeddings pomocí all-MiniLM-L6-v2 (384 dimenzí)
- Metadata enrichment pro lepší vyhledávání

---

## 2. Tech Stack

### 2.1 Backend
- **.NET 8** (ASP.NET Core Web API)
- **Entity Framework Core** (ORM pro Postgres)
- **Microsoft Agent Framework** (AI agenti)
- **Langfuse SDK** (monitoring LLM)
- **Qdrant Client** (vektorová databáze)
- **Swagger/OpenAPI** (API dokumentace)

### 2.2 Frontend
- **React** (TypeScript doporučeno, ale není povinné)
- **shadcn/ui** (UI komponenty)
- **Tailwind CSS** (styling)
- **REST API client** (fetch/axios)

### 2.3 Databáze a Services
- **PostgreSQL** (relační data)
- **Qdrant** (vektorová databáze pro embeddings)
- **Langfuse** (monitoring a tracing)
- **Ollama/vLLM** (lokální LLM hosting - mimo Docker)

### 2.4 Infrastructure
- **Docker Compose** (API, Frontend, Postgres, Qdrant, Langfuse)
- Filesystem storage pro Markdown přílohy

---

## 3. Datový model

### 3.1 Database Schema (PostgreSQL)

```sql
-- Finanční transakce
CREATE TABLE Transactions (
    Id SERIAL PRIMARY KEY,
    Description VARCHAR(200) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    CompanyId VARCHAR(20) NOT NULL, -- IČO dodavatele/odběratele
    CompanyName VARCHAR(200),
    TransactionType VARCHAR(10) NOT NULL, -- 'income' nebo 'expense'
    TransactionDate TIMESTAMP NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_transactions_date ON Transactions(TransactionDate);
CREATE INDEX idx_transactions_created_at ON Transactions(CreatedAt);
CREATE INDEX idx_transactions_company_id ON Transactions(CompanyId);
CREATE INDEX idx_transactions_type ON Transactions(TransactionType);

-- Přílohy
CREATE TABLE Attachments (
    Id SERIAL PRIMARY KEY,
    TransactionId INT NOT NULL,
    FileName VARCHAR(255) NOT NULL,
    FilePath VARCHAR(500) NOT NULL,
    Category VARCHAR(50), -- 'invoice', 'contract', 'purchase_order', null
    ProcessingStatus VARCHAR(20) NOT NULL DEFAULT 'pending', -- 'pending', 'processing', 'completed', 'failed'
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    ProcessedAt TIMESTAMP,
    
    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE CASCADE
);

CREATE INDEX idx_attachments_transaction_id ON Attachments(TransactionId);
CREATE INDEX idx_attachments_category ON Attachments(Category);
CREATE INDEX idx_attachments_status ON Attachments(ProcessingStatus);

-- Chat konverzace (volitelné - pro history)
CREATE TABLE ChatSessions (
    Id SERIAL PRIMARY KEY,
    SessionId UUID NOT NULL UNIQUE,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE ChatMessages (
    Id SERIAL PRIMARY KEY,
    SessionId UUID NOT NULL,
    Role VARCHAR(20) NOT NULL, -- 'user' nebo 'assistant'
    Content TEXT NOT NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    
    FOREIGN KEY (SessionId) REFERENCES ChatSessions(SessionId) ON DELETE CASCADE
);

CREATE INDEX idx_chat_messages_session ON ChatMessages(SessionId);
```

### 3.2 Filesystem struktura

```
/data/
  /attachments/
    /{transactionId}/
      attachment-{id}.md
      attachment-{id}.md
    /{transactionId}/
      ...
```

### 3.3 Qdrant Collections

**Collection: `transaction_documents`**
```json
{
  "vectors": {
    "size": 384,
    "distance": "Cosine"
  },
  "payload_schema": {
    "attachment_id": "integer",
    "transaction_id": "integer",
    "chunk_index": "integer",
    "total_chunks": "integer",
    "category": "keyword",
    "file_name": "text",
    "content": "text",
    "token_count": "integer",
    "has_amounts": "boolean",
    "has_dates": "boolean",
    "word_count": "integer",
    "created_at": "datetime"
  }
}
```

---

## 4. API Specifikace

### 4.1 Transactions Endpoints

#### GET /api/transactions
Vrátí seznam všech finančních transakcí.

**Response:**
```json
[
  {
    "id": 1,
    "description": "Nákup materiálu",
    "amount": 15000.00,
    "companyId": "12345678",
    "companyName": "ACME Corp s.r.o.",
    "transactionType": "expense",
    "transactionDate": "2025-01-15T00:00:00Z",
    "createdAt": "2025-01-15T10:30:00Z",
    "attachmentCount": 2
  }
]
```

#### GET /api/transactions/{id}
Vrátí detail transakce včetně seznamu příloh.

**Response:**
```json
{
  "id": 1,
  "description": "Nákup materiálu",
  "amount": 15000.00,
  "companyId": "12345678",
  "companyName": "ACME Corp s.r.o.",
  "transactionType": "expense",
  "transactionDate": "2025-01-15T00:00:00Z",
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-01-15T10:30:00Z",
  "attachments": [
    {
      "id": 1,
      "fileName": "faktura-2025-001.md",
      "category": "invoice",
      "processingStatus": "completed",
      "createdAt": "2025-01-15T11:00:00Z"
    }
  ]
}
```

#### POST /api/transactions
Vytvoří novou transakci.

**Request:**
```json
{
  "description": "Nová transakce",
  "amount": 25000.00,
  "companyId": "87654321",
  "companyName": "Partner Ltd.",
  "transactionType": "income",
  "transactionDate": "2025-01-20T00:00:00Z"
}
```

#### PUT /api/transactions/{id}
Aktualizuje existující transakci.

#### DELETE /api/transactions/{id}
Smaže transakci včetně všech příloh.

---

### 4.2 Attachments Endpoints

#### POST /api/transactions/{transactionId}/attachments
Nahraje novou přílohu k transakci.

**Request:** `multipart/form-data`
- `file`: Markdown soubor

**Response:**
```json
{
  "id": 5,
  "transactionId": 1,
  "fileName": "smlouva-2025.md",
  "filePath": "/data/attachments/1/attachment-5.md",
  "processingStatus": "pending",
  "createdAt": "2025-01-16T09:00:00Z"
}
```

#### GET /api/transactions/{transactionId}/attachments/{id}
Vrátí metadata přílohy.

#### GET /api/transactions/{transactionId}/attachments/{id}/download
Stáhne obsah přílohy (Markdown soubor).

#### DELETE /api/transactions/{transactionId}/attachments/{id}
Smaže přílohu.

---

### 4.3 Document Generation Endpoint

#### POST /api/documents/generate
Vygeneruje vzorový dokument podle specifikace.

**Request:**
```json
{
  "documentType": "invoice", // "invoice", "contract", "purchase_order"
  "variant": 1, // 1, 2, nebo 3 (různé šablony)
  "transactionId": 1, // volitelné - pro použití dat z transakce
  "customData": { // volitelné - override dat
    "amount": 50000,
    "companyName": "Custom Company"
  }
}
```

**Response:** Markdown soubor ke stažení

---

### 4.4 Chat Endpoint

#### POST /api/chat/message
Odešle zprávu do chatu a vrátí odpověď.

**Request:**
```json
{
  "message": "Kolik transakcí bylo vytvořeno v roce 2025?",
  "sessionId": "optional-session-uuid", // pro kontinuitu konverzace
  "conversationHistory": [ // volitelné - pro multi-turn
    {
      "role": "user",
      "content": "Předchozí otázka"
    },
    {
      "role": "assistant",
      "content": "Předchozí odpověď"
    }
  ]
}
```

**Response:**
```json
{
  "message": "V roce 2025 bylo vytvořeno 47 transakcí.",
  "sessionId": "uuid-session-id",
  "sources": [ // použité zdroje
    {
      "type": "database",
      "query": "SELECT COUNT(*) FROM Transactions WHERE..."
    },
    {
      "type": "document",
      "attachmentId": 5,
      "fileName": "faktura-xyz.md"
    }
  ],
  "metadata": {
    "tokensUsed": 350,
    "responseTime": 1.2,
    "agentsUsed": ["DatabaseAgent"]
  }
}
```

---

### 4.5 Data Processing Endpoint

#### POST /api/data/sync-existing
Spustí zpracování všech existujících transakcí a příloh pro indexování.

**Response:**
```json
{
  "message": "Synchronizace spuštěna",
  "totalAttachments": 85,
  "status": "processing"
}
```

#### GET /api/data/sync-status
Vrátí aktuální stav synchronizace.

**Response:**
```json
{
  "status": "processing",
  "totalAttachments": 85,
  "processedAttachments": 42,
  "failedAttachments": 3,
  "completedAt": null
}
```

---

## 5. Architektura Agentů

### 5.1 DocumentProcessingAgent (Background)

**Účel:** Zpracování nahraných dokumentů v background jobu

**Tools:**
1. **CategorizeDocument**
   - Input: Markdown obsah dokumentu
   - Output: Kategorie ("invoice", "contract", "purchase_order", "unknown")
   - Implementace: LLM call s promtem pro klasifikaci

2. **ChunkDocument**
   - Input: Plný text dokumentu
   - Output: Seznam chunků (500-1000 tokenů, 10-20% překryv)
   - Implementace: Fixed-size chunks s překryvem na úrovni vět
   - NO Markdown-specific chunking (unified approach)

3. **CreateEmbeddings**
   - Input: Text chunku
   - Output: Vector embeddings (384 dimenzí)
   - Implementace: all-MiniLM-L6-v2 model přes Ollama

4. **EnrichMetadata**
   - Input: Chunk content
   - Output: Metadata (has_amounts, has_dates, word_count)
   - Implementace: Regex + simple parsing

5. **IndexToQdrant**
   - Input: Embeddings + enriched metadata
   - Output: Success/Failure
   - Implementace: Qdrant client upsert

**Workflow:**
```
1. Načti Markdown soubor z disku
2. Zavolej CategorizeDocument → získej kategorii
3. Zavolej ChunkDocument → rozdělení na chunky s překryvem
   - Chunk size: 800 tokenů (~500-600 slov)
   - Overlap: 100 tokenů (~10-15%)
   - Split strategy: Na úrovni vět
4. Pro každý chunk:
   a) Zavolej CreateEmbeddings → získej vector (384 dimenzí)
   b) Zavolaj EnrichMetadata → získej metadata
   c) Zavolaj IndexToQdrant → ulož do Qdrantu s payload:
      - attachment_id
      - transaction_id
      - chunk_index
      - total_chunks
      - category
      - file_name
      - content (text chunku)
      - token_count
      - has_amounts (boolean - obsahuje částky)
      - has_dates (boolean - obsahuje data)
      - word_count
      - created_at
5. Aktualizuj Attachments.ProcessingStatus = 'completed'
```

**Embedding model:** all-MiniLM-L6-v2 (384 dimenzí, rychlý, dostatečně kvalitní)

**Metadata enrichment pravidla:**
- `has_amounts`: true pokud chunk obsahuje částky (regex: `\d+\s*Kč|\d+\s*EUR`)
- `has_dates`: true pokud chunk obsahuje data (regex: `\d{1,2}\.\d{1,2}\.\d{4}`)
- `word_count`: počet slov v chunku
- `token_count`: odhadovaný počet tokenů (délka / 4)

---

### 5.2 ChatOrchestrator (Real-time)

**Účel:** Hlavní agent pro řízení konverzace a delegování úkolů

**Logika:**
```
Přijme user message
  ↓
Analyzuje typ dotazu
  ↓
┌─────────────────────┬────────────────────────┐
│ Strukturovaný dotaz │ Sémantický dotaz       │
│ (počty, sumy, data) │ (obsah dokumentů)      │
└──────────↓──────────┴───────────↓────────────┘
     DatabaseAgent          DocumentSearchAgent
```

**Příklady delegace:**
- "Kolik transakcí v 2025?" → **DatabaseAgent**
- "Najdi smlouvy o pronájmu" → **DocumentSearchAgent**
- "Které faktury nesedí s transakcí?" → **OBA** (database + documents)

---

### 5.3 DatabaseAgent

**Účel:** Provádění SQL dotazů nad strukturovanými daty

**Tools:**

1. **ExecuteReadQuery**
   - Input: SQL SELECT dotaz (read-only)
   - Output: JSON výsledky
   - Bezpečnost: Pouze SELECT, žádné modifikace
   - Implementace: EF Core raw SQL query

2. **GetTransactionsList**
   - Input: Filtry (datum od-do, typ, company)
   - Output: Seznam transakcí
   - Implementace: LINQ query

3. **GetTransactionDetails**
   - Input: Transaction ID
   - Output: Detail transakce včetně příloh
   - Implementace: EF Include

4. **AggregateTransactions**
   - Input: Agregační funkce (COUNT, SUM, AVG), filtry
   - Output: Agregované hodnoty
   - Implementace: LINQ GroupBy/Sum/Count

**Příklady použití:**
```
Query: "Kolik transakcí bylo v roce 2025?"
Tool: AggregateTransactions
Params: { function: "COUNT", filters: { yearFrom: 2025, yearTo: 2025 } }
Result: 47

Query: "Jaký byl celkový výdělek za únor 2025?"
Tool: AggregateTransactions
Params: { 
  function: "SUM", 
  field: "amount",
  filters: { 
    transactionType: "income",
    dateFrom: "2025-02-01",
    dateTo: "2025-02-29"
  }
}
Result: 450000.00
```

---

### 5.4 DocumentSearchAgent

**Účel:** Sémantické vyhledávání v dokumentech pomocí RAG

**Tools:**

1. **SearchInQdrant**
   - Input: Query text, filtry (kategorie, transaction_id), limit
   - Output: Top-K nejrelevantnějších chunků
   - Implementace: 
     - Vytvoř embedding z query (all-MiniLM-L6-v2)
     - Qdrant vector search s cosine similarity
     - Vrať obsah + metadata
     - Využij metadata filtering (has_amounts, has_dates)

2. **GetDocumentContent**
   - Input: Attachment ID
   - Output: Plný Markdown obsah
   - Implementace: Přečti soubor z disku

3. **FilterByMetadata**
   - Input: Metadata filtry (kategorie, datum, order)
   - Output: Seznam attachment IDs
   - Implementace: Qdrant filtered search

**Příklady použití:**

```
Query: "Najdi všechny smlouvy o nájmu"
Tool: SearchInQdrant
Params: { 
  query: "smlouva nájem pronájem",
  filters: { category: "contract" },
  limit: 10
}
Result: [
  {
    attachmentId: 15,
    transactionId: 8,
    fileName: "smlouva-najem-kancelare.md",
    snippet: "...nájem kancelářských prostor...",
    score: 0.89
  },
  ...
]

→ LLM přečte obsah těchto dokumentů a odpovídá
```

```
Query: "Které faktury mají částku vyšší než 50000 Kč?"
Tool: SearchInQdrant
Params: { 
  query: "faktura částka",
  filters: { 
    category: "invoice",
    has_amounts: true  // metadata filter
  },
  limit: 50
}
Result: [všechny faktury s částkami]

→ LLM přečte každou fakturu
→ LLM on-the-fly extrahuje částky
→ LLM vyfiltruje ty > 50000
→ Vrátí odpověď
```

---

### 5.5 Agent Communication Flow

**Příklad komplexního dotazu:**

```
User: "Které transakce mají v přílohách fakturu s částkou 
       odlišnou od částky na transakci?"

ChatOrchestrator:
  ↓
1. Deleguje na DocumentSearchAgent
   Tool: SearchInQdrant
   Params: { category: "invoice", has_amounts: true, limit: 100 }
   Result: Seznam všech faktur s obsahem
   
2. LLM přečte faktury a extrahuje částky z každé
   Result: [
     { attachmentId: 5, transactionId: 2, invoiceAmount: 15000 },
     { attachmentId: 8, transactionId: 4, invoiceAmount: 32000 },
     ...
   ]
   
3. Deleguje na DatabaseAgent
   Tool: GetTransactionDetails (pro každý transactionId)
   Result: [
     { transactionId: 2, transactionAmount: 15000 },
     { transactionId: 4, transactionAmount: 30000 },
     ...
   ]
   
4. Orchestrator porovná částky
   Result: Transakce 4 má rozdíl (faktura 32000 vs transakce 30000)
   
5. Sestaví odpověď:
   "Našel jsem 1 transakci s rozdílem:
   - Transakce #4: Částka na transakci 30 000 Kč, 
     ale faktura ukazuje 32 000 Kč (rozdíl +2 000 Kč)"
```

---

## 6. Frontend Komponenty

### 6.1 Tech stack
- **React** (TypeScript volitelně)
- **shadcn/ui** - moderní UI komponenty (Button, Card, Table, Dialog, Input, ...)
- **Tailwind CSS** - utility-first styling
- **React Router** - routing

### 6.2 Struktura aplikace

```
src/
  components/
    ui/                    # shadcn/ui komponenty
      button.tsx
      card.tsx
      table.tsx
      dialog.tsx
      input.tsx
      ...
    TransactionList/
      TransactionList.tsx
      TransactionListItem.tsx
    TransactionDetail/
      TransactionDetail.tsx
      AttachmentList.tsx
      AttachmentUpload.tsx
    TransactionForm/
      TransactionForm.tsx
    Chat/
      Chat.tsx
      ChatMessage.tsx
      ChatInput.tsx
    DocumentGenerator/
      DocumentGenerator.tsx
  pages/
    TransactionsPage.tsx
    TransactionDetailPage.tsx
    ChatPage.tsx
  services/
    api.ts
  lib/
    utils.ts             # shadcn utils
  App.tsx
```

### 6.3 Hlavní komponenty

#### TransactionList
- Zobrazuje tabulku transakcí (shadcn Table)
- Sloupce: Popis, Částka, IČO, Společnost, Typ (příjem/výdaj), Datum, Počet příloh
- Řazení a filtrování
- Odkaz na detail
- Tlačítko "Nová transakce" (shadcn Button)

#### TransactionDetail
- Detail transakce (shadcn Card)
- Seznam příloh s možností stažení
- Upload nové přílohy (shadcn Dialog)
- Tlačítka "Upravit" / "Smazat"

#### Chat
- Konverzační rozhraní
- Historie zpráv (user/assistant)
- Input pole s tlačítkem Odeslat (shadcn Input + Button)
- Zobrazení použitých zdrojů (volitelné)
- Loading states

#### DocumentGenerator
- Form pro výběr typu dokumentu (faktura/smlouva/objednávka)
- Výběr varianty (1/2/3)
- Tlačítko pro generování a stažení

#### SyncButton
- Tlačítko "Synchronizuj existující data"
- Progress bar / status zpracování

---

### 6.4 Wireframe návrh

```
┌─────────────────────────────────────────────────────────────┐
│  Logo    Transakce  |  Chat  |  Generovat dokument           │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Seznam transakcí                                             │
│  ┌──────────┬────────────────────────────────┐               │
│  │ [+ Nová] │ [Synchronizuj existující data] │               │
│  └──────────┴────────────────────────────────┘               │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ Popis       │ Částka │ Společnost │ Typ │ Datum │ 📎 │  │
│  ├────────────────────────────────────────────────────────┤  │
│  │ Nákup mat.  │ 15000  │ ACME       │ ▼   │ 15.1  │ 2  │  │
│  │ Prodej zboží│ 32000  │ Partner    │ ▲   │  3.2  │ 1  │  │
│  └────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Detail transakce #1                                          │
├─────────────────────────────────────────────────────────────┤
│  Popis: Nákup materiálu                                       │
│  Částka: 15 000 Kč                                           │
│  IČO: 12345678                                               │
│  Společnost: ACME Corp s.r.o.                                │
│  Typ: Výdaj                                                  │
│  Datum transakce: 15.1.2025                                  │
│  Vytvořeno: 15.1.2025 10:30                                  │
│                                                               │
│  Přílohy (2):                                                │
│  ┌────────────────────────────────────────┐                  │
│  │ 📄 faktura-2025-001.md  [Stáhnout]    │                  │
│  │ 📄 smlouva-ramcova.md   [Stáhnout]    │                  │
│  └────────────────────────────────────────┘                  │
│                                                               │
│  [Nahrát přílohu]  [Upravit]  [Smazat]                      │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Chat                                                         │
├─────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────┐  │
│  │ 👤 Kolik transakcí bylo v roce 2025?                  │  │
│  ├───────────────────────────────────────────────────────┤  │
│  │ 🤖 V roce 2025 bylo vytvořeno 47 transakcí.          │  │
│  │    Zdroje: [Databázový dotaz]                         │  │
│  ├───────────────────────────────────────────────────────┤  │
│  │ 👤 Které z nich mají fakturu?                         │  │
│  ├───────────────────────────────────────────────────────┤  │
│  │ 🤖 32 transakcí má připojenou fakturu.               │  │
│  │    Zdroje: [Dokumenty: 32 faktur nalezeno]           │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                               │
│  [____________________________________________] [Odeslat]     │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  Generovat dokument                                           │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Typ dokumentu:                                              │
│  ( ) Faktura  ( ) Smlouva  ( ) Objednávka                    │
│                                                               │
│  Varianta šablony:                                           │
│  ( ) Varianta 1  ( ) Varianta 2  ( ) Varianta 3              │
│                                                               │
│  [Vygenerovat a stáhnout]                                    │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

---

## 7. Docker Compose Setup

### 7.1 docker-compose.yml

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:16-alpine
    container_name: transactions-postgres
    environment:
      POSTGRES_DB: transactionsdb
      POSTGRES_USER: transactionuser
      POSTGRES_PASSWORD: transactionpass123
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U transactionuser -d transactionsdb"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Qdrant Vector Database
  qdrant:
    image: qdrant/qdrant:latest
    container_name: transactions-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Langfuse (LLM Monitoring)
  langfuse-db:
    image: postgres:16-alpine
    container_name: langfuse-postgres
    environment:
      POSTGRES_DB: langfuse
      POSTGRES_USER: langfuse
      POSTGRES_PASSWORD: langfuse123
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U langfuse -d langfuse"]
      interval: 10s
      timeout: 5s
      retries: 5

  langfuse:
    image: langfuse/langfuse:latest
    container_name: transactions-langfuse
    depends_on:
      langfuse-db:
        condition: service_healthy
    environment:
      DATABASE_URL: postgresql://langfuse:langfuse123@langfuse-db:5432/langfuse
      NEXTAUTH_URL: http://localhost:3030
      NEXTAUTH_SECRET: mysecretkey
      SALT: mysaltkey
    ports:
      - "3030:3000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/api/health"]
      interval: 30s
      timeout: 10s
      retries: 5

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
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=transactionsdb;Username=transactionuser;Password=transactionpass123
      Qdrant__Url: http://qdrant:6333
      Langfuse__PublicKey: ${LANGFUSE_PUBLIC_KEY}
      Langfuse__SecretKey: ${LANGFUSE_SECRET_KEY}
      Langfuse__BaseUrl: http://langfuse:3000
      LLM__BaseUrl: ${OLLAMA_BASE_URL:-http://host.docker.internal:11434}
      LLM__Model: ${LLM_MODEL:-llama3.1:8b}
      Embedding__Model: ${EMBEDDING_MODEL:-all-minilm:l6-v2}
    ports:
      - "5000:8080"
    volumes:
      - ./data/attachments:/app/data/attachments

  # React Frontend
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: transactions-frontend
    depends_on:
      - api
    environment:
      REACT_APP_API_URL: http://localhost:5000
    ports:
      - "3000:80"

networks:
  default:
    name: transactions-network
      retries: 5

  # .NET API
  api:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: orders-api
    depends_on:
      postgres:
        condition: service_healthy
      qdrant:
        condition: service_healthy
      langfuse:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=ordersdb;Username=orderuser;Password=orderpass123
      Qdrant__Url: http://qdrant:6333
      Langfuse__PublicKey: ${LANGFUSE_PUBLIC_KEY}
      Langfuse__SecretKey: ${LANGFUSE_SECRET_KEY}
      Langfuse__BaseUrl: http://langfuse:3000
      LLM__BaseUrl: ${OLLAMA_BASE_URL:-http://host.docker.internal:11434}
      LLM__Model: ${LLM_MODEL:-llama3.1:8b}
      Embedding__Model: ${EMBEDDING_MODEL:-all-minilm}
    ports:
      - "5000:8080"
    volumes:
      - ./data/attachments:/app/data/attachments

  # React Frontend
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: transactions-frontend
    depends_on:
      - api
    environment:
      REACT_APP_API_URL: http://localhost:5000
    ports:
      - "3000:80"

networks:
  default:
    name: transactions-network
```

### 7.2 .env.example

```env
# Langfuse API Keys (získat z Langfuse UI po prvním spuštění)
LANGFUSE_PUBLIC_KEY=pk-lf-xxx
LANGFUSE_SECRET_KEY=sk-lf-xxx

# LLM Configuration
OLLAMA_BASE_URL=http://host.docker.internal:11434
LLM_MODEL=llama3.1:8b
EMBEDDING_MODEL=all-minilm:l6-v2
```

### 7.3 Spuštění

```bash
# 1. Ujisti se, že Ollama běží na hostu
ollama serve

# 2. Stáhni potřebné modely
ollama pull llama3.1:8b
ollama pull all-minilm:l6-v2

# 3. Spusť Docker Compose
docker-compose up -d

# 4. Počkej na inicializaci (health checks)
docker-compose ps

# 5. Přístup k aplikacím:
# - Frontend: http://localhost:3000
# - API Swagger: http://localhost:5000/swagger
# - Langfuse UI: http://localhost:3030
# - Qdrant UI: http://localhost:6333/dashboard
```

---

## 8. Implementační kroky

### 8.1 Fáze 1: Backend základ (Den 1-2)

1. **Projekt setup**
   - Vytvoř .NET 8 Web API projekt
   - Nastav EF Core + PostgreSQL
   - Přidej Swagger/OpenAPI

2. **Database & Models**
   - Vytvoř entity: `Transaction`, `Attachment`
   - Konfigurace EF Core
   - Migrace databáze

3. **Basic CRUD**
   - `TransactionsController` - CRUD operace
   - `AttachmentsController` - Upload/Download
   - Filesystem service pro ukládání MD souborů

4. **Data Seed**
   - Vytvoř seed service
   - Generuj 100 transakcí
   - 10 opakujících se dodavatelů/odběratelů
   - Rozptyl přes 2 roky (2024-2025)
   - Realistické částky a popisy

### 8.2 Fáze 2: Document Processing (Den 3-4)

5. **Background Job Queue**
   - Implementuj `BackgroundTaskQueue` (IHostedService)
   - Queue pro zpracování příloh

6. **Qdrant Integration**
   - Přidej Qdrant client
   - Vytvoř service pro embeddings (all-MiniLM-L6-v2)
   - Init collection při startu s 384 dimenzemi

7. **Chunking Service**
   - Implementuj `ChunkingService`
   - Fixed-size chunks (800 tokenů, 100 overlap)
   - Split na úrovni vět (NO Markdown-specific)

8. **Metadata Enrichment Service**
   - Implementuj `MetadataEnrichmentService`
   - Regex pro has_amounts (částky v Kč/EUR)
   - Regex pro has_dates (DD.MM.YYYY)
   - Word count a token count

9. **DocumentProcessingAgent**
   - Implementuj MS Agent Framework agenta
   - Tool: `CategorizeDocument` (LLM call)
   - Tool: `ChunkDocument` (chunking service)
   - Tool: `CreateEmbeddings` (embedding model)
   - Tool: `EnrichMetadata` (metadata service)
   - Tool: `IndexToQdrant`

10. **Processing Workflow**
    - Při uploadu → queue job
    - Background worker zpracuje
    - Chunking → Embeddings → Metadata → Qdrant
    - Aktualizuj status v DB

### 8.3 Fáze 3: Chat & RAG (Den 5-6)

11. **Chat Agents**
    - `ChatOrchestrator` (MS Agent Framework)
    - `DatabaseAgent` + tools (GetTransactionsList, AggregateTransactions)
    - `DocumentSearchAgent` + tools (SearchInQdrant s metadata filtering)

12. **Chat API**
    - `ChatController` endpoint
    - Handling conversation history
    - Response formatting

13. **Langfuse Integration**
    - Přidaj Langfuse SDK
    - Trace všechny LLM cally včetně embeddings
    - Log conversations s metadata

### 8.4 Fáze 4: Document Generation (Den 7)

14. **MD Templates**
    - Vytvoř šablony pro faktury (3 varianty)
    - Vytvoř šablony pro smlouvy (3 varianty)
    - Vytvoř šablony pro objednávky (3 varianty)

15. **Generator Service**
    - Service pro generování MD z template
    - Randomizace dat
    - `DocumentGeneratorController`

### 8.5 Fáze 5: Frontend (Den 8-10)

16. **React Setup**
    - Create React App / Vite
    - Nainstaluj shadcn/ui (npx shadcn@latest init)
    - Nainstaluj potřebné komponenty (Button, Card, Table, Dialog, Input)
    - API service layer
    - Routing

17. **Transactions UI**
    - `TransactionList` komponenta (s shadcn Table)
    - `TransactionDetail` komponenta (s shadcn Card)
    - `TransactionForm` (create/edit s shadcn Dialog)

18. **Attachments UI**
    - Upload komponenta (s shadcn Dialog)
    - Download/view funkce

19. **Chat UI**
    - `Chat` komponenta
    - Message history
    - Input handling (s shadcn Input + Button)

20. **Additional Features**
    - Document generator UI
    - Sync existující data tlačítko (s progress)

### 8.6 Fáze 6: Docker & Testing (Den 11-12)

21. **Dockerizace**
    - Dockerfile pro .NET API
    - Dockerfile pro React
    - docker-compose.yml
    - Health checks

22. **Testing & Debug**
    - End-to-end testy
    - Chat dotazy (různé scénáře)
    - Test chunking a embeddings
    - Performance check (rychlost vyhledávání)
    - Langfuse monitoring review

23. **Dokumentace**
    - README.md
    - API documentation
    - Setup guide

---

## 9. Seed Data Specifikace

### 9.1 Dodavatelé/Odběratelé (10 společností)

```csharp
var companies = new[]
{
    new { Id = "12345678", Name = "ACME Corporation s.r.o.", Type = "dodavatel" },
    new { Id = "87654321", Name = "TechSupply Ltd.", Type = "dodavatel" },
    new { Id = "11223344", Name = "Office Solutions a.s.", Type = "dodavatel" },
    new { Id = "99887766", Name = "BuildMat s.r.o.", Type = "dodavatel" },
    new { Id = "55667788", Name = "IT Services Group", Type = "dodavatel" },
    new { Id = "44332211", Name = "Global Trading Inc.", Type = "odběratel" },
    new { Id = "66778899", Name = "SmartRetail s.r.o.", Type = "odběratel" },
    new { Id = "22446688", Name = "Corporate Clients a.s.", Type = "odběratel" },
    new { Id = "77889900", Name = "Distribution Network", Type = "odběratel" },
    new { Id = "33445566", Name = "Premium Partners Ltd.", Type = "odběratel" }
};
```

### 9.2 Generování transakcí

**Parametry:**
- **Počet:** 100 transakcí
- **Časové období:** 1.1.2024 - 31.10.2025 (rovnoměrně rozloženo)
- **Částky:** 
  - Expense: 5 000 - 150 000 Kč
  - Income: 10 000 - 500 000 Kč
- **Popisy:**
  - Expense: "Nákup {produkt}", "Platba za {kategorie}", "Faktura za {služba}"
  - Income: "Prodej {produkt}", "Zakázka {číslo}", "Dodávka pro {zákazník}"

**Distribuce:**
- 60% Expense, 40% Income
- Každá společnost se opakuje 8-12×
- Některé transakce mají přílohy (30% seed dat)

**Příklad seed kódu:**
```csharp
var random = new Random(42); // fixed seed pro reprodukovatelnost
var startDate = new DateTime(2024, 1, 1);
var endDate = new DateTime(2025, 10, 31);
var totalDays = (endDate - startDate).Days;

var transactions = new List<Transaction>();

for (int i = 1; i <= 100; i++)
{
    var isExpense = random.Next(100) < 60;
    var company = companies[random.Next(companies.Length)];
    
    while ((isExpense && company.Type == "odběratel") || 
           (!isExpense && company.Type == "dodavatel"))
    {
        company = companies[random.Next(companies.Length)];
    }
    
    var transaction = new Transaction
    {
        Description = GenerateTransactionDescription(isExpense, i),
        Amount = isExpense 
            ? random.Next(5000, 150000) 
            : random.Next(10000, 500000),
        CompanyId = company.Id,
        CompanyName = company.Name,
        TransactionType = isExpense ? "expense" : "income",
        TransactionDate = startDate.AddDays(random.Next(totalDays)),
        CreatedAt = startDate.AddDays(random.Next(totalDays))
    };
    
    transactions.Add(transaction);
}

// Seřaď podle data transakce
transactions = transactions.OrderBy(t => t.TransactionDate).ToList();
```

---

## 10. Markdown Šablony pro Dokumenty

### 10.1 Faktura - Varianta 1

```markdown
# FAKTURA

**Číslo faktury:** {InvoiceNumber}  
**Datum vystavení:** {IssueDate}  
**Datum splatnosti:** {DueDate}  

---

## Dodavatel
**{SupplierName}**  
IČO: {SupplierICO}  
{SupplierAddress}  

## Odběratel
**{CustomerName}**  
IČO: {CustomerICO}  
{CustomerAddress}  

---

## Položky

| Popis | Množství | Jedn. cena | Celkem |
|-------|----------|------------|--------|
| {Item1Description} | {Item1Quantity} | {Item1Price} Kč | {Item1Total} Kč |
| {Item2Description} | {Item2Quantity} | {Item2Price} Kč | {Item2Total} Kč |

**Celkem bez DPH:** {TotalWithoutVAT} Kč  
**DPH 21%:** {VAT} Kč  
**Celkem k úhradě:** {TotalAmount} Kč

---

## Platební údaje
**Číslo účtu:** {BankAccount}  
**Variabilní symbol:** {VariableSymbol}  
**Konstantní symbol:** 0308

Děkujeme za vaši objednávku.
```

### 10.2 Faktura - Varianta 2

```markdown
# DAŇOVÝ DOKLAD - FAKTURA

| | |
|---|---|
| **Faktura č.** | {InvoiceNumber} |
| **Vystaveno** | {IssueDate} |
| **Splatnost** | {DueDate} |
| **VS** | {VariableSymbol} |

---

### DODAVATEL
{SupplierName}  
{SupplierAddress}  
IČO: {SupplierICO}, DIČ: {SupplierDIC}

### ODBĚRATEL
{CustomerName}  
{CustomerAddress}  
IČO: {CustomerICO}

---

### SPECIFIKACE PLNĚNÍ

**{ServiceDescription}**

Jednotková cena: {UnitPrice} Kč  
Počet jednotek: {Quantity}  

| Základ daně | DPH 21% | Celkem |
|-------------|---------|--------|
| {BaseAmount} Kč | {VATAmount} Kč | **{TotalAmount} Kč** |

Částka k úhradě: **{TotalAmount} Kč**  
Úhrada na účet: {BankAccount}

---

*Faktura byla vystavena elektronicky a je platná bez podpisu.*
```

### 10.3 Faktura - Varianta 3

```markdown
# F A K T U R A

## Č. {InvoiceNumber}

**Datum vystavení:** {IssueDate}  
**Datum zdanitelného plnění:** {TaxableDate}  
**Datum splatnosti:** {DueDate}

---

**DODAVATEL:**  
{SupplierName}, IČO: {SupplierICO}  
Sídlo: {SupplierAddress}

**ODBĚRATEL:**  
{CustomerName}, IČO: {CustomerICO}  
Sídlo: {CustomerAddress}

---

## FAKTURUJEME

{DetailedDescription}

| Položka | MJ | Množství | Cena/MJ | Celkem bez DPH |
|---------|----|-----------|---------|-----------------| 
| {ItemName} | ks | {Quantity} | {UnitPrice} Kč | {ItemTotal} Kč |

**Celkem bez daně:** {TotalWithoutVAT} Kč  
**DPH 21%:** {VATAmount} Kč  
**CELKEM K ÚHRADĚ:** **{TotalAmount} Kč**

---

**Platební podmínky:**  
Bankovní spojení: {BankAccount}  
Variabilní symbol: {VariableSymbol}

Případné reklamace uplatněte do 14 dnů od obdržení faktury.
```

---

### 10.4 Smlouva - Varianta 1

```markdown
# RÁMCOVÁ SMLOUVA O DODÁVKÁCH

**Číslo smlouvy:** {ContractNumber}  
**Datum uzavření:** {SignDate}

---

## Smluvní strany

**Dodavatel:**  
{SupplierName}  
IČO: {SupplierICO}  
Sídlo: {SupplierAddress}  
Zastoupený: {SupplierRepresentative}

**Odběratel:**  
{CustomerName}  
IČO: {CustomerICO}  
Sídlo: {CustomerAddress}  
Zastoupený: {CustomerRepresentative}

---

## I. Předmět smlouvy

Předmětem této smlouvy je závazek dodavatele dodávat odběrateli {ProductCategory} 
a závazek odběratele tyto dodávky odebírat a platit za ně dohodnutou cenu.

## II. Doba trvání smlouvy

Smlouva se uzavírá na dobu určitou od **{ValidFrom}** do **{ValidTo}**.

## III. Cenové podmínky

Odhadovaná roční hodnota dodávek: **{EstimatedAnnualValue} Kč** bez DPH.

Konkrétní ceny budou sjednány v jednotlivých dílčích objednávkách.

## IV. Platební podmínky

Splatnost faktur je {PaymentTerms} dní od data vystavení faktury.

## V. Závěrečná ustanovení

Smlouva je vyhotovena ve dvou stejnopisech, z nichž každá smluvní strana obdrží 
po jednom.

---

V {City} dne {SignDate}

____________________    ____________________  
Za dodavatele           Za odběratele
```

### 10.5 Smlouva - Varianta 2

```markdown
# SMLOUVA O DÍLO

uzavřená dle § 2586 a násl. zákona č. 89/2012 Sb., občanský zákoník

**Číslo smlouvy:** {ContractNumber}

---

### SMLUVNÍ STRANY

**Objednatel:**  
Název: {CustomerName}  
IČO: {CustomerICO}  
Adresa: {CustomerAddress}

**Zhotovitel:**  
Název: {SupplierName}  
IČO: {SupplierICO}  
Adresa: {SupplierAddress}

---

### Článek I - Předmět smlouvy

Zhotovitel se zavazuje provést pro objednatele dílo spočívající v:

{WorkDescription}

Objednatel se zavazuje dílo převzít a zaplatit zhotoviteli sjednanou cenu.

### Článek II - Termíny

**Zahájení prací:** {StartDate}  
**Dokončení díla:** {CompletionDate}

### Článek III - Cena a platební podmínky

Celková cena díla: **{TotalPrice} Kč** bez DPH  
DPH 21%: {VATAmount} Kč  
**Cena celkem: {TotalWithVAT} Kč**

Platba bude provedena na základě faktury splatné do {PaymentDays} dnů.

### Článek IV - Platnost smlouvy

Tato smlouva nabývá platnosti dnem podpisu oběma smluvními stranami 
a účinnosti dnem {EffectiveDate}.

---

Podpisy smluvních stran:

Za objednatele: ________________  
Za zhotovitele: ________________

Datum: {SignDate}
```

### 10.6 Smlouva - Varianta 3

```markdown
# NÁJEMNÍ SMLOUVA

**Evidenční číslo:** {ContractNumber}

---

## PRONAJÍMATEL

{LandlordName}  
IČO: {LandlordICO}  
Adresa: {LandlordAddress}

## NÁJEMCE

{TenantName}  
IČO: {TenantICO}  
Adresa: {TenantAddress}

---

## ČLÁNEK I - PŘEDMĚT NÁJMU

Pronajímatel přenechává nájemci do dočasného užívání nebytové prostory:

**Adresa:** {PropertyAddress}  
**Účel užití:** {UsagePurpose}  
**Plocha:** {Area} m²

## ČLÁNEK II - DOBA NÁJMU

Nájem se sjednává na dobu určitou:

**Od:** {LeaseStartDate}  
**Do:** {LeaseEndDate}

Výpovědní lhůta činí {NoticePeriod} měsíce.

## ČLÁNEK III - NÁJEMNÉ A SLUŽBY

Měsíční nájemné: **{MonthlyRent} Kč** + DPH  
Záloha na služby: **{UtilitiesDeposit} Kč**

Nájemné je splatné do {RentDueDay}. dne každého měsíce předem.

## ČLÁNEK IV - KAUCE

Nájemce složil pronajímateli kauci ve výši {DepositAmount} Kč, 
která bude vrácena po řádném ukončení nájmu.

## ČLÁNEK V - PRÁVA A POVINNOSTI

Nájemce je povinen užívat předmět nájmu řádně a v souladu s jeho účelem.

---

Smlouva byla sepsána a podepsána dne {SignDate}.

_____________________    _____________________  
Pronajímatel             Nájemce
```

---

### 10.7 Objednávka - Varianta 1

```markdown
# OBJEDNÁVKA

**Číslo objednávky:** {OrderNumber}  
**Datum:** {OrderDate}

---

## Objednatel
{CustomerName}  
IČO: {CustomerICO}  
{CustomerAddress}  
Kontakt: {CustomerContact}

## Dodavatel
{SupplierName}  
IČO: {SupplierICO}  
{SupplierAddress}

---

## Objednáváme

| Položka | Popis | Množství | Jedn. cena | Celkem |
|---------|-------|----------|------------|--------|
| 1 | {Item1Description} | {Item1Qty} ks | {Item1Price} Kč | {Item1Total} Kč |
| 2 | {Item2Description} | {Item2Qty} ks | {Item2Price} Kč | {Item2Total} Kč |

**Celková cena bez DPH:** {TotalWithoutVAT} Kč  
**DPH 21%:** {VATAmount} Kč  
**Celková cena s DPH:** **{TotalWithVAT} Kč**

---

## Dodací podmínky

**Místo dodání:** {DeliveryAddress}  
**Termín dodání:** {DeliveryDate}  
**Způsob dopravy:** {DeliveryMethod}

## Platební podmínky

Platba: {PaymentMethod}  
Splatnost: {PaymentTerms}

---

Za správnost objednávky: ___________________
```

### 10.8 Objednávka - Varianta 2

```markdown
# KUPNÍ OBJEDNÁVKA

| | |
|---|---|
| **Objednávka č.** | {OrderNumber} |
| **Datum vystavení** | {OrderDate} |
| **Požadované dodání** | {RequestedDeliveryDate} |

---

### ODBĚRATEL
{CustomerName}, IČO: {CustomerICO}  
{CustomerAddress}  
Tel: {CustomerPhone}, Email: {CustomerEmail}

### DODAVATEL
{SupplierName}, IČO: {SupplierICO}  
{SupplierAddress}

---

### SPECIFIKACE OBJEDNÁVKY

**{MainProductDescription}**

Katalogové číslo: {CatalogNumber}  
Množství: {Quantity} {Unit}  
Jednotková cena: {UnitPrice} Kč

| Cena bez DPH | DPH 21% | Cena s DPH |
|--------------|---------|------------|
| {PriceWithoutVAT} Kč | {VATAmount} Kč | **{TotalPrice} Kč** |

---

**DODACÍ ADRESA:**  
{DeliveryAddress}

**FAKTURAČNÍ ADRESA:**  
{BillingAddress}

**PLATBA:** {PaymentTerms} dnů od dodání  
**DOPRAVA:** {ShippingMethod}

---

Tato objednávka je závazná po potvrzení dodavatelem.

Potvrzení zašlete na: {CustomerEmail}
```

### 10.9 Objednávka - Varianta 3

```markdown
# O B J E D N Á V K A

## Č. {OrderNumber} / {Year}

Datum objednávky: {OrderDate}

---

**OBJEDNATEL:**  
{CustomerName}  
{CustomerAddress}  
IČO: {CustomerICO}, DIČ: {CustomerDIC}  
Kontaktní osoba: {ContactPerson}  
Tel: {Phone}, Email: {Email}

**DODAVATEL:**  
{SupplierName}  
{SupplierAddress}  
IČO: {SupplierICO}

---

## OBJEDNANÉ ZBOŽÍ / SLUŽBY

### {ProductCategoryTitle}

{DetailedProductDescription}

| Pol. | Kód zboží | Popis | MJ | Množství | Cena/MJ | Sleva | Celkem |
|------|-----------|-------|----|----------|---------|-------|--------|
| 1 | {Code1} | {Desc1} | {Unit1} | {Qty1} | {Price1} | {Disc1}% | {Total1} Kč |
| 2 | {Code2} | {Desc2} | {Unit2} | {Qty2} | {Price2} | {Disc2}% | {Total2} Kč |

**Celkem bez DPH:** {SubTotal} Kč  
**DPH 21%:** {VATAmount} Kč  
**CELKEM:** **{GrandTotal} Kč**

---

**POŽADOVANÝ TERMÍN DODÁNÍ:** {DeliveryDeadline}  
**MÍSTO DODÁNÍ:** {DeliveryLocation}

**PLATEBNÍ PODMÍNKY:** {PaymentConditions}

---

Potvrzení objednávky zašlete na email: {ConfirmationEmail}

_____________________  
Podpis objednatele
```

---

## 11. Testovací scénáře pro Chat

### 11.1 SQL dotazy (DatabaseAgent)

1. "Kolik transakcí bylo vytvořeno v roce 2025?"
2. "Kolik se na transakcích vydělalo za tento kalendářní měsíc?"
3. "Jaké byly výdaje a výnosy v únoru roku 2025?"
4. "Kolik máme celkem transakcí typu příjem?"
5. "Která společnost má nejvíce transakcí?"
6. "Jaká je průměrná hodnota transakce?"

### 11.2 Sémantické dotazy (DocumentSearchAgent)

7. "Kolik transakcí má v přílohách fakturu?"
8. "Najdi všechny smlouvy o nájmu"
9. "Které dokumenty zmiňují výpovědní lhůtu?"
10. "Najdi faktury od společnosti ACME"

### 11.3 Kombinované dotazy (Multi-agent)

11. "Které transakce mají v přílohách fakturu, která neodpovídá částce na transakci?"
12. "Kolik faktur máme za říjen 2025 a jaká je jejich celková hodnota?"
13. "Najdi smlouvy, které končí v příštích 3 měsících"
14. "Které transakce typu výdaj mají připojený dokument o dílo?"

### 11.4 Multi-turn konverzace

```
User: Kolik transakcí máme?
Assistant: Celkem máte 100 transakcí.

User: A kolik z nich má přílohy?
Assistant: 30 transakcí má připojené přílohy.

User: Jaké typy dokumentů to jsou?
Assistant: Mezi přílohami je 15 faktur, 8 smluv a 7 objednávek.
```

---

## 12. Implementační poznámky

### 12.1 MS Agent Framework - Základní struktura

```csharp
public class DatabaseAgent : Agent
{
    private readonly ApplicationDbContext _db;
    
    public DatabaseAgent(ApplicationDbContext db)
    {
        _db = db;
        
        // Definice tools
        RegisterTool("execute_query", ExecuteQueryTool);
        RegisterTool("aggregate_transactions", AggregateTransactionsTool);
    }
    
    private async Task<object> ExecuteQueryTool(Dictionary<string, object> args)
    {
        var sql = args["sql"].ToString();
        // Execute safe read-only SQL
        return await _db.Database.SqlQueryRaw<dynamic>(sql).ToListAsync();
    }
    
    private async Task<object> AggregateTransactionsTool(Dictionary<string, object> args)
    {
        var function = args["function"].ToString(); // COUNT, SUM, AVG
        var filters = args["filters"] as Dictionary<string, object>;
        
        var query = _db.Transactions.AsQueryable();
        
        // Apply filters...
        
        return function switch
        {
            "COUNT" => await query.CountAsync(),
            "SUM" => await query.SumAsync(t => t.Amount),
            "AVG" => await query.AverageAsync(t => t.Amount),
            _ => null
        };
    }
}
```

### 12.2 Langfuse Tracing

```csharp
public class LangfuseService
{
    private readonly HttpClient _httpClient;
    
    public async Task<string> TraceGeneration(
        string name,
        string input,
        Func<Task<string>> action)
    {
        var traceId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;
        
        // Start trace
        await StartTrace(traceId, name, input);
        
        try
        {
            var output = await action();
            
            // End trace with success
            await EndTrace(traceId, output, startTime);
            
            return output;
        }
        catch (Exception ex)
        {
            // End trace with error
            await EndTraceWithError(traceId, ex, startTime);
            throw;
        }
    }
}
```

### 12.3 Qdrant Indexování s Chunking a Metadata Enrichment

```csharp
public class QdrantService
{
    private readonly QdrantClient _client;
    private readonly EmbeddingService _embedding;
    private readonly ChunkingService _chunking;
    private readonly MetadataEnrichmentService _metadataService;
    
    public async Task IndexDocumentWithChunks(
        int attachmentId,
        string content,
        string category,
        int transactionId)
    {
        // 1. Rozděl na chunky
        var chunks = _chunking.SplitIntoChunks(content);
        
        // 2. Pro každý chunk: embedding + metadata + index
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            
            // 3. Vytvoř embedding (384 dimenzí)
            var vector = await _embedding.CreateEmbedding(chunk.Content);
            
            // 4. Enriched metadata
            var metadata = _metadataService.EnrichMetadata(chunk.Content);
            
            // 5. Upsert do Qdrantu
            await _client.UpsertAsync(
                collectionName: "transaction_documents",
                points: new[]
                {
                    new PointStruct
                    {
                        Id = new PointId { Uuid = $"{attachmentId}-chunk{i}" },
                        Vectors = vector,
                        Payload = new Dictionary<string, object>
                        {
                            ["attachment_id"] = attachmentId,
                            ["transaction_id"] = transactionId,
                            ["chunk_index"] = i,
                            ["total_chunks"] = chunks.Count,
                            ["category"] = category,
                            ["content"] = chunk.Content,
                            ["token_count"] = chunk.TokenCount,
                            ["has_amounts"] = metadata.HasAmounts,
                            ["has_dates"] = metadata.HasDates,
                            ["word_count"] = metadata.WordCount,
                            ["created_at"] = DateTime.UtcNow
                        }
                    }
                }
            );
        }
    }
    
    public async Task<List<SearchResult>> SearchDocuments(
        string query,
        string category = null,
        bool? hasAmounts = null,
        int limit = 10)
    {
        // Vytvoř query embedding
        var queryVector = await _embedding.CreateEmbedding(query);
        
        // Build filters
        var filters = new List<Condition>();
        if (category != null)
            filters.Add(FieldCondition.Match("category", category));
        if (hasAmounts.HasValue)
            filters.Add(FieldCondition.Match("has_amounts", hasAmounts.Value));
        
        var filter = filters.Count > 0 
            ? Filter.Must(filters.ToArray())
            : null;
        
        var results = await _client.SearchAsync(
            collectionName: "transaction_documents",
            vector: queryVector,
            filter: filter,
            limit: (ulong)limit,
            scoreThreshold: 0.5f  // Minimum similarity
        );
        
        return results.Select(r => new SearchResult
        {
            AttachmentId = r.Payload["attachment_id"].IntegerValue,
            TransactionId = r.Payload["transaction_id"].IntegerValue,
            ChunkIndex = r.Payload["chunk_index"].IntegerValue,
            Score = r.Score,
            Content = r.Payload["content"].StringValue,
            Category = r.Payload["category"].StringValue,
            HasAmounts = r.Payload["has_amounts"].BoolValue,
            HasDates = r.Payload["has_dates"].BoolValue
        }).ToList();
    }
}
```

---

## 13. Očekávané výstupy projektu

Po dokončení POC projektu budeš mít:

### 13.1 Funkční aplikace
- ✅ Webové rozhraní pro správu finančních transakcí
- ✅ AI chat s možností dotazování na data
- ✅ Automatické zpracování dokumentů s chunking (800 tokenů, 10% překryv)
- ✅ Embeddings pomocí all-MiniLM-L6-v2 (384 dimenzí)
- ✅ Metadata enrichment pro lepší vyhledávání
- ✅ Generování vzorových dokumentů

### 13.2 Naučené technologie
- ✅ Microsoft Agent Framework - praktické použití agentů
- ✅ RAG implementace - vektorové vyhledávání + LLM
- ✅ Qdrant - práce s embeddings a vektorovou DB
- ✅ Langfuse - monitoring a debugging LLM aplikací

### 13.3 Zkušenosti pro produkci
- 📊 Kdy použít RAG vs strukturovanou extrakci
- 🤖 Jak orchestrovat více agentů
- 💾 Jak efektivně indexovat dokumenty
- 🔍 Jaké typy dotazů RAG zvládá/nezvládá
- ⚡ Performance charakteristiky (rychlost, přesnost)

### 13.4 Rozšiřitelnost
Projekt je navržen tak, aby mohl být snadno rozšířen:
- Přidání strukturované extrakce dat
- Implementace MCP protokolu
- Rozšíření o více typů dokumentů
- Přidání více agentů (např. ReportingAgent)
- Pokročilejší RAG techniky (reranking, hybrid search)

---

## 14. Troubleshooting & Tips

### 14.1 Časté problémy

**Problem:** Pomalé RAG dotazy
- Zkus snížit limit výsledků z Qdrantu
- Používej filtrování podle kategorie
- Zvětši batch size pro embeddings

**Problem:** Nepřesné odpovědi z LLM
- Zkontroluj prompt engineering
- Přidej více příkladů do system promptu
- Použij větší/lepší model
- Zkontroluj relevanci výsledků z Qdrantu (score)

**Problem:** Qdrant vrací irelevantní dokumenty
- Zkontroluj kvalitu embeddingů
- Vyzkoušej jiný embedding model
- Použij hybrid search (keyword + vector)

### 14.2 Optimalizace

**Rychlejší indexování:**
- Batch processing příloh
- Paralelní embeddings
- Cache embeddingů pro často používané texty

**Lepší chat odpovědi:**
- Fine-tuned prompt pro každého agenta
- Few-shot examples v promptu
- Structured outputs z LLM

**Úspora nákladů:**
- Cache LLM responses
- Používej menší model pro klasifikaci
- Větší model jen pro složité dotazy

---

## 15. Další kroky po POC

Po úspěšném dokončení POC můžeš zvážit:

1. **Strukturovaná extrakce** - přidat extractAgent pro přesné hodnoty
2. **Autentizace/Autorizace** - multi-tenant setup
3. **Advanced RAG** - reranking, hypothetical document embeddings
4. **MCP Integration** - propojení s dalšími systémy
5. **Production deployment** - Azure/AWS hosting
6. **A/B Testing** - různé RAG strategie
7. **Fine-tuning** - vlastní model pro lepší výsledky

---

## Kontakt a podpora

Pro dotazy ohledně implementace:
- MS Agent Framework docs: https://microsoft.github.io/semantic-kernel/
- Qdrant docs: https://qdrant.tech/documentation/
- Langfuse docs: https://langfuse.com/docs

---

**Verze:** 1.0  
**Datum:** 12. listopadu 2025  
**Autor:** Petr

---

*Toto zadání je živý dokument. Během implementace můžeš provádět úpravy podle zjištěných potřeb a zkušeností.*
