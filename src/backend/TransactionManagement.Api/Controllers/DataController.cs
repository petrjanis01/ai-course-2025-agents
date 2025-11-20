using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Enums;
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
        var expenseCount = await context.Transactions.CountAsync(t => t.TransactionType == TransactionType.Expense);
        var incomeCount = await context.Transactions.CountAsync(t => t.TransactionType == TransactionType.Income);
        var totalExpense = await context.Transactions
            .Where(t => t.TransactionType == TransactionType.Expense)
            .SumAsync(t => t.Amount);
        var totalIncome = await context.Transactions
            .Where(t => t.TransactionType == TransactionType.Income)
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
