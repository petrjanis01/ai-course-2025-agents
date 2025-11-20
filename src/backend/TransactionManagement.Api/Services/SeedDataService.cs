using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Entities;
using TransactionManagement.Api.Models.Enums;

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
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 10, 31, 0, 0, 0, DateTimeKind.Utc);
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
                TransactionType = isExpense ? TransactionType.Expense : TransactionType.Income,
                TransactionDate = DateTime.SpecifyKind(startDate.AddDays(random.Next(totalDays)), DateTimeKind.Utc),
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
        var expenseCount = transactions.Count(t => t.TransactionType == TransactionType.Expense);
        var incomeCount = transactions.Count(t => t.TransactionType == TransactionType.Income);
        var totalExpense = transactions.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount);
        var totalIncome = transactions.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount);

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
