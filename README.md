```
User Query
    ↓
ChatController.Chat()
    ↓
ChatOrchestrator.ProcessMessageAsync()
    ↓
TransactionAssistant Agent (Microsoft.Extensions.AI)
    │
    ├─ Database Functions (Text-to-SQL approach)
    │   ├─ GetDatabaseSchema()
    │   │   └─ Vrací schema databáze (Tables: Transactions, Attachments)
    │   │
    │   ├─ ExecuteSqlQuery(sqlQuery)
    │   │   └─ Vykonává libovolné SELECT dotazy nad PostgreSQL
    │   │   └─ Validace bezpečnosti (jen SELECT, žádné DROP/DELETE/UPDATE)
    │   │
    │   └─ GetTransactionStatistics(dateFrom?, dateTo?, companyId?)
    │       └─ Agregované statistiky (Total, Income, Expense, NetBalance)
    │
    └─ Document Functions (RAG approach)
        ├─ SearchDocuments(query, category?, hasAmounts?, hasDates?, limit)
        │   └─ Sémantické vyhledávání přes Qdrant vector DB
        │   └─ Vrací chunks s preview (300 znaků) + AttachmentId
        │
        ├─ GetDocumentContent(attachmentId)
        │   └─ Načte plný obsah dokumentu z File Storage
        │
        └─ CountDocumentsByCategory()
            └─ Statistiky dokumentů (počty per kategorie)
    ↓
ChatResponse (Message, ResponseTime, TokensUsed, AgentsUsed)

═══════════════════════════════════════════════════════════════
DOCUMENT UPLOAD & PROCESSING PIPELINE
═══════════════════════════════════════════════════════════════

User Upload Document
    ↓
AttachmentsController.UploadAttachment()
    ├─ Validace souboru (max 10 MB)
    ├─ Uložení do File Storage
    ├─ Vytvoření Attachment entity (status: Pending, category: Unknown)
    └─ Zařazení do Background Queue
    ↓
Background Worker (DocumentProcessingService)
    ↓
ProcessAttachmentAsync(attachmentId)
    ├─ 1. Načtení souboru z File Storage
    │
    ├─ 2. KATEGORIZACE pomocí LLM
    │   └─ LLMService.CategorizeDocumentAsync(content)
    │   └─ Kategorie: invoice | contract | purchase_order | unknown
    │
    ├─ 3. CHUNKING
    │   └─ ChunkingService.SplitIntoChunks(content)
    │   └─ Rozdělí dokument na menší části (chunks)
    │
    ├─ 4. METADATA ENRICHMENT (pro každý chunk)
    │   └─ MetadataEnrichmentService.EnrichMetadata(chunk)
    │   └─ Extrahuje: hasAmounts, hasDates, wordCount
    │
    ├─ 5. INDEXOVÁNÍ DO QDRANT (pro každý chunk)
    │   └─ QdrantService.IndexDocumentChunkAsync(chunk)
    │   └─ Vytvoří embedding + uloží do vector DB
    │
    └─ 6. Označení jako Completed (ProcessingStatus, ProcessedAt)

Výsledek: Dokument je kategorizovaný, rozchunkovaný a prohledávatelný přes RAG

═══════════════════════════════════════════════════════════════

Technologie:
- Microsoft.Extensions.AI framework (agent + function calling)
- OpenTelemetry monitoring (automatic tracing)
- Qdrant vector database (embeddings + semantic search)
- PostgreSQL (transaction data)
- File Storage (document content)
- Background Task Queue (asynchronní zpracování)

Query Flow:
1. User zadá dotaz (např. "Kolik máme faktur od ACME v roce 2025?")
2. ChatOrchestrator vytvoří TransactionAssistant agenta s tools
3. Agent automaticky volá potřebné funkce:
   - GetDatabaseSchema() → zjistí strukturu DB
   - ExecuteSqlQuery("SELECT...") → spustí SQL dotaz
   - SearchDocuments("ACME faktury") → najde relevantní dokumenty
4. Agent zpracuje výsledky a vygeneruje odpověď v češtině
5. OpenTelemetry tracuje všechny operace do Langfuse
```