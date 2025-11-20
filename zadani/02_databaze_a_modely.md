# 02 - Databáze a Modely

## Cíl
Vytvořit databázové entity, ApplicationDbContext a provést první migraci PostgreSQL databáze.

## Prerekvizity
- Dokončený krok 01 (projekt setup)
- PostgreSQL běžící lokálně nebo v Dockeru

## Kroky implementace

### 1. Spuštění PostgreSQL (pokud nemáš)

Pro vývoj můžeš použít jednoduchou instanci:

```bash
docker run -d \
  --name postgres-dev \
  -e POSTGRES_DB=transactionsdb \
  -e POSTGRES_USER=appuser \
  -e POSTGRES_PASSWORD=apppass123 \
  -p 5432:5432 \
  postgres:16-alpine
```

**Poznámka:** V produkčním Docker Compose setupu (krok 11) bude jeden PostgreSQL server sdílený pro aplikaci i Langfuse.

### 2. Vytvoření enums

**Models/Enums/TransactionType.cs:**
```csharp
namespace TransactionManagement.Api.Models.Enums;

public enum TransactionType
{
    Income,
    Expense
}
```

**Models/Enums/DocumentCategory.cs:**
```csharp
namespace TransactionManagement.Api.Models.Enums;

public enum DocumentCategory
{
    Unknown,
    Invoice,
    Contract,
    PurchaseOrder
}
```

**Models/Enums/ProcessingStatus.cs:**
```csharp
namespace TransactionManagement.Api.Models.Enums;

public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
```

### 3. Vytvoření entity modelů

**Models/Entities/Transaction.cs:**
```csharp
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string CompanyId { get; set; } = string.Empty; // IČO

    public string? CompanyName { get; set; }

    public TransactionType TransactionType { get; set; }

    public DateTime TransactionDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
```

**Models/Entities/Attachment.cs:**
```csharp
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.Entities;

public class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TransactionId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DocumentCategory Category { get; set; } = DocumentCategory.Unknown;

    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    // Navigation property
    public Transaction Transaction { get; set; } = null!;
}
```

**Models/Entities/ChatSession.cs:**
```csharp
namespace TransactionManagement.Api.Models.Entities;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
```

**Models/Entities/ChatMessage.cs:**
```csharp
namespace TransactionManagement.Api.Models.Entities;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatSessionId { get; set; }

    public string Role { get; set; } = string.Empty; // "user" or "assistant"

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ChatSession ChatSession { get; set; } = null!;
}
```

### 4. Vytvoření ApplicationDbContext

**Data/ApplicationDbContext.cs:**
```csharp
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Models.Entities;

namespace TransactionManagement.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Nastavení cascade delete
        modelBuilder.Entity<Attachment>()
            .HasOne(e => e.Transaction)
            .WithMany(t => t.Attachments)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(e => e.ChatSession)
            .WithMany(s => s.Messages)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Poznámka:** Entity používají EF Core konvence:
- `Id` property je automaticky primární klíč
- Názvy tabulek odvozeny z `DbSet` properties
- `TransactionId` + `Transaction` navigation → automatický FK vztah
- `SessionId` + `ChatSession` navigation → automatický FK vztah
- V `OnModelCreating` jen cascade delete behavior (ostatní podle konvence)

### 5. Vytvoření první migrace

```bash
# Nainstaluj EF Core tools (pokud nemáš)
dotnet tool install --global dotnet-ef

# Vytvoř migraci
dotnet ef migrations add InitialCreate

# Aplikuj migraci na databázi
dotnet ef database update
```

### 6. Ověření databázové struktury

Připoj se k PostgreSQL a ověř, že tabulky byly vytvořeny:

```bash
# Připojení přes psql
docker exec -it postgres-dev psql -U appuser -d transactionsdb

# V psql konzoli:
\dt
# Měl bys vidět tabulky: Transactions, Attachments, ChatSessions, ChatMessages

# Zobraz strukturu tabulky Transactions
\d "Transactions"

# Zobraz indexy
\di

# Exit
\q
```

### 7. Vytvoření DTO modelů

**Models/DTOs/TransactionDto.cs:**
```csharp
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AttachmentCount { get; set; }
}

public class TransactionDetailDto : TransactionDto
{
    public List<AttachmentDto> Attachments { get; set; } = new();
}

public class CreateTransactionDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
}

public class UpdateTransactionDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public TransactionType TransactionType { get; set; }
    public DateTime TransactionDate { get; set; }
}
```

**Models/DTOs/AttachmentDto.cs:**
```csharp
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Models.DTOs;

public class AttachmentDto
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

**Models/DTOs/ChatMessageDto.cs:**
```csharp
namespace TransactionManagement.Api.Models.DTOs;

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}

public class ChatResponseDto
{
    public string Message { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public List<ChatSourceDto> Sources { get; set; } = new();
    public ChatMetadataDto Metadata { get; set; } = new();
}

public class ChatSourceDto
{
    public string Type { get; set; } = string.Empty; // "database" or "document"
    public string? Query { get; set; }
    public int? AttachmentId { get; set; }
    public string? FileName { get; set; }
}

public class ChatMetadataDto
{
    public int TokensUsed { get; set; }
    public double ResponseTime { get; set; }
    public List<string> AgentsUsed { get; set; } = new();
}
```

## Ověření

Po dokončení:

1. **Build projektu:**
```bash
dotnet build
```

2. **Ověření databáze:**
```bash
dotnet ef database update
```

3. **Kontrola tabulek v PostgreSQL:**
```bash
docker exec -it postgres-dev psql -U appuser -d transactionsdb -c "\dt"
```

## Výstup této fáze

✅ Databázové entity (Transaction, Attachment, ChatSession, ChatMessage)
✅ ApplicationDbContext s EF Core konfigurací
✅ Migrace a databázové schéma v PostgreSQL
✅ Indexy pro optimalizaci dotazů
✅ DTO modely pro API komunikaci

## Další krok

→ **03_crud_transakce.md** - Implementace CRUD operací pro transakce a přílohy
