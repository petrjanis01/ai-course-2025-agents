# 04 - Seed Data - Generování Testovacích Dat

## Cíl
Vytvořit service pro generování 100 realistických transakcí s 10 opakujícími se společnostmi rozloženými přes 2 roky.

## Prerekvizity
- Dokončený krok 03 (CRUD operace)

## Kroky implementace

### 1. Seed Data Service

**Services/SeedDataService.cs:**
```csharp
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Entities;

namespace TransactionManagement.Api.Services;

public interface ISeedDataService
{
    Task SeedTransactionsAsync(bool clearExisting = false);
    Task<bool> HasDataAsync();
}

public class SeedDataService : ISeedDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeedDataService> _logger;

    private readonly (string Id, string Name, string Type)[] _companies = new[]
    {
        ("12345678", "ACME Corporation s.r.o.", "dodavatel"),
        ("87654321", "TechSupply Ltd.", "dodavatel"),
        ("11223344", "Office Solutions a.s.", "dodavatel"),
        ("99887766", "BuildMat s.r.o.", "dodavatel"),
        ("55667788", "IT Services Group", "dodavatel"),
        ("44332211", "Global Trading Inc.", "odběratel"),
        ("66778899", "SmartRetail s.r.o.", "odběratel"),
        ("22446688", "Corporate Clients a.s.", "odběratel"),
        ("77889900", "Distribution Network", "odběratel"),
        ("33445566", "Premium Partners Ltd.", "odběratel")
    };

    private readonly string[] _expenseDescriptions = new[]
    {
        "Nákup kancelářského materiálu",
        "Platba za serverové služby",
        "Faktura za software licence",
        "Nákup IT hardware",
        "Stavební materiál",
        "Elektrické součástky",
        "Marketingové služby",
        "Konzultační služby",
        "Oprava zařízení",
        "Pronájem kancelářských prostor",
        "Telekomunikační služby",
        "Údržba a servis",
        "Účetní služby",
        "Právní poradenství",
        "Dopravní služby"
    };

    private readonly string[] _incomeDescriptions = new[]
    {
        "Prodej zboží",
        "Zakázka na vývoj software",
        "Dodávka produktů",
        "Projektová práce",
        "Konzultační služby",
        "Licence software",
        "Pravidelná platba",
        "Služby a podpora",
        "Realizace projektu",
        "Prodej řešení"
    };

    public SeedDataService(
        ApplicationDbContext context,
        ILogger<SeedDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasDataAsync()
    {
        return await _context.Transactions.AnyAsync();
    }

    public async Task SeedTransactionsAsync(bool clearExisting = false)
    {
        if (clearExisting)
        {
            _logger.LogWarning("Clearing existing transactions...");
            _context.Transactions.RemoveRange(_context.Transactions);
            await _context.SaveChangesAsync();
        }

        if (await HasDataAsync())
        {
            _logger.LogInformation("Database already contains transactions. Skipping seed.");
            return;
        }

        _logger.LogInformation("Starting seed data generation...");

        var random = new Random(42); // Fixed seed for reproducibility
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2025, 10, 31);
        var totalDays = (endDate - startDate).Days;

        var transactions = new List<Transaction>();

        for (int i = 1; i <= 100; i++)
        {
            var isExpense = random.Next(100) < 60; // 60% expense, 40% income
            var company = GetRandomCompany(random, isExpense);

            var transaction = new Transaction
            {
                Description = GenerateDescription(random, isExpense, i),
                Amount = GenerateAmount(random, isExpense),
                CompanyId = company.Id,
                CompanyName = company.Name,
                TransactionType = isExpense ? "expense" : "income",
                TransactionDate = startDate.AddDays(random.Next(totalDays)),
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 180)), // Random creation time
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 180))
            };

            transactions.Add(transaction);
        }

        // Sort by transaction date
        transactions = transactions.OrderBy(t => t.TransactionDate).ToList();

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seed data generated: {Count} transactions", transactions.Count);

        // Print statistics
        var expenseCount = transactions.Count(t => t.TransactionType == "expense");
        var incomeCount = transactions.Count(t => t.TransactionType == "income");
        var totalExpense = transactions.Where(t => t.TransactionType == "expense").Sum(t => t.Amount);
        var totalIncome = transactions.Where(t => t.TransactionType == "income").Sum(t => t.Amount);

        _logger.LogInformation("Statistics:");
        _logger.LogInformation("  Expenses: {ExpenseCount} transactions, total: {TotalExpense:N2} Kč",
            expenseCount, totalExpense);
        _logger.LogInformation("  Income: {IncomeCount} transactions, total: {TotalIncome:N2} Kč",
            incomeCount, totalIncome);
        _logger.LogInformation("  Net: {Net:N2} Kč", totalIncome - totalExpense);
    }

    private (string Id, string Name, string Type) GetRandomCompany(Random random, bool isExpense)
    {
        var targetType = isExpense ? "dodavatel" : "odběratel";
        var candidates = _companies.Where(c => c.Type == targetType).ToArray();
        return candidates[random.Next(candidates.Length)];
    }

    private string GenerateDescription(Random random, bool isExpense, int number)
    {
        var descriptions = isExpense ? _expenseDescriptions : _incomeDescriptions;
        var baseDescription = descriptions[random.Next(descriptions.Length)];

        // Add some variation
        if (random.Next(100) < 30)
        {
            return $"{baseDescription} #{number:D3}";
        }

        return baseDescription;
    }

    private decimal GenerateAmount(Random random, bool isExpense)
    {
        if (isExpense)
        {
            // Expense: 5,000 - 150,000 Kč
            return random.Next(5000, 150000);
        }
        else
        {
            // Income: 10,000 - 500,000 Kč
            return random.Next(10000, 500000);
        }
    }
}
```

### 2. Seed Endpoint Controller

**Controllers/DataController.cs:**
```csharp
using Microsoft.AspNetCore.Mvc;
using TransactionManagement.Api.Services;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly ISeedDataService _seedDataService;
    private readonly ILogger<DataController> _logger;

    public DataController(
        ISeedDataService seedDataService,
        ILogger<DataController> logger)
    {
        _seedDataService = seedDataService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/data/seed - Vygeneruje 100 testovacích transakcí
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> SeedData([FromQuery] bool clearExisting = false)
    {
        try
        {
            if (await _seedDataService.HasDataAsync() && !clearExisting)
            {
                return BadRequest(new
                {
                    message = "Database already contains data. Use ?clearExisting=true to clear and reseed."
                });
            }

            await _seedDataService.SeedTransactionsAsync(clearExisting);

            return Ok(new
            {
                message = "Seed data generated successfully",
                transactionsCreated = 100
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during seed data generation");
            return StatusCode(500, new { message = "Error generating seed data", error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/data/stats - Vrátí statistiky dat
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromServices] ApplicationDbContext context)
    {
        var totalTransactions = await context.Transactions.CountAsync();
        var totalAttachments = await context.Attachments.CountAsync();
        var expenseCount = await context.Transactions.CountAsync(t => t.TransactionType == "expense");
        var incomeCount = await context.Transactions.CountAsync(t => t.TransactionType == "income");
        var totalExpense = await context.Transactions
            .Where(t => t.TransactionType == "expense")
            .SumAsync(t => t.Amount);
        var totalIncome = await context.Transactions
            .Where(t => t.TransactionType == "income")
            .SumAsync(t => t.Amount);

        var companiesWithMostTransactions = await context.Transactions
            .GroupBy(t => new { t.CompanyId, t.CompanyName })
            .Select(g => new
            {
                CompanyId = g.Key.CompanyId,
                CompanyName = g.Key.CompanyName,
                TransactionCount = g.Count(),
                TotalAmount = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.TransactionCount)
            .Take(5)
            .ToListAsync();

        return Ok(new
        {
            totalTransactions,
            totalAttachments,
            expenses = new
            {
                count = expenseCount,
                total = totalExpense
            },
            income = new
            {
                count = incomeCount,
                total = totalIncome
            },
            net = totalIncome - totalExpense,
            topCompanies = companiesWithMostTransactions
        });
    }
}
```

### 3. Registrace v Program.cs

Přidej do `Program.cs`:

```csharp
// Register services
builder.Services.AddScoped<ISeedDataService, SeedDataService>();
```

### 4. Automatické seedování při startu (volitelné)

Pokud chceš automaticky seedovat data při prvním startu, přidej do `Program.cs` před `app.Run()`:

```csharp
// Auto-seed on first run
using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<ISeedDataService>();

    if (!await seedService.HasDataAsync())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("No data found. Running seed...");
        await seedService.SeedTransactionsAsync();
    }
}
```

## Testování

### 1. Pomocí Swagger UI

```
POST /api/data/seed
```

Response:
```json
{
  "message": "Seed data generated successfully",
  "transactionsCreated": 100
}
```

### 2. Zobrazení statistik

```
GET /api/data/stats
```

Response:
```json
{
  "totalTransactions": 100,
  "totalAttachments": 0,
  "expenses": {
    "count": 60,
    "total": 5250000.00
  },
  "income": {
    "count": 40,
    "total": 12500000.00
  },
  "net": 7250000.00,
  "topCompanies": [
    {
      "companyId": "12345678",
      "companyName": "ACME Corporation s.r.o.",
      "transactionCount": 12,
      "totalAmount": 850000.00
    }
  ]
}
```

### 3. Pomocí curl

```bash
# Vygeneruj seed data
curl -X POST http://localhost:5000/api/data/seed

# Zobraz statistiky
curl http://localhost:5000/api/data/stats

# Seznam transakcí
curl http://localhost:5000/api/transactions

# Clear a reseed
curl -X POST "http://localhost:5000/api/data/seed?clearExisting=true"
```

### 4. Ověření v databázi

```bash
docker exec -it postgres-dev psql -U appuser -d transactionsdb

# V psql:
SELECT COUNT(*) FROM "Transactions";
SELECT "TransactionType", COUNT(*), SUM("Amount")
FROM "Transactions"
GROUP BY "TransactionType";

SELECT "CompanyName", COUNT(*) as transaction_count
FROM "Transactions"
GROUP BY "CompanyName"
ORDER BY transaction_count DESC;
```

## Očekávané výsledky

Po úspěšném seedování:

- ✅ **100 transakcí** v databázi
- ✅ **60% výdajů** (expense), **40% příjmů** (income)
- ✅ **10 společností** s 8-12 transakcemi každá
- ✅ **Transakce rozložené** od 1.1.2024 do 31.10.2025
- ✅ **Realistické částky**:
  - Výdaje: 5,000 - 150,000 Kč
  - Příjmy: 10,000 - 500,000 Kč
- ✅ **Různorodé popisy** transakcí

## Vzorová data

Příklad vygenerovaných transakcí:

| ID | Popis | Částka | IČO | Společnost | Typ | Datum |
|----|-------|--------|-----|------------|-----|-------|
| 1 | Nákup IT hardware | 45,000 | 12345678 | ACME Corporation | expense | 2024-01-15 |
| 2 | Prodej zboží | 125,000 | 44332211 | Global Trading Inc. | income | 2024-01-22 |
| 3 | Faktura za software | 32,000 | 87654321 | TechSupply Ltd. | expense | 2024-02-03 |
| ... | ... | ... | ... | ... | ... | ... |

## Docker Compose - Poznámka

V tomto kroku **nepřidáváme žádné nové služby** do Docker Compose. Seed data funkce je součást backend API, které už běží.

Po spuštění stacku můžete zavolat seed endpoint:
```bash
# Spuštění celého stacku (pokud ještě neběží)
docker-compose up -d

# Seed dat přes API
curl -X POST http://localhost:5000/api/data/seed

# Nebo přes frontend na http://localhost:3000/data
```

## Výstup této fáze

✅ SeedDataService s realistickými daty
✅ DataController s seed a stats endpointy
✅ 100 testovacích transakcí
✅ 10 opakujících se společností
✅ Statistiky a analýza dat
✅ Možnost clear & reseed
✅ **Funguje v Docker Compose bez změn**

## Další krok

→ **09_qdrant_embeddings.md** - Integrace Qdrant vektorové databáze a embedding modelu
