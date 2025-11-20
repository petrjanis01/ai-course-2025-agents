# 09 - Document Generator

## Cíl
Implementovat službu pro generování vzorových Markdown dokumentů (faktury, smlouvy, objednávky) s 3 variantami každého typu.

## Prerekvizity
- Dokončený krok 15 (Langfuse monitoring)

## Kroky implementace

### 1. Šablony dokumentů

Vytvoř složku `Templates` v projektu:

**Templates/invoice_template_1.md:**
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
| {ItemDescription} | {ItemQuantity} | {ItemPrice} Kč | {ItemTotal} Kč |

**Celkem bez DPH:** {TotalWithoutVAT} Kč
**DPH 21%:** {VAT} Kč
**Celkem k úhradě:** {TotalAmount} Kč

---

## Platební údaje
**Číslo účtu:** {BankAccount}
**Variabilní symbol:** {VariableSymbol}

Děkujeme za vaši objednávku.
```

**Templates/invoice_template_2.md:**
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
IČO: {SupplierICO}

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

**Templates/invoice_template_3.md:**
```markdown
# F A K T U R A

## Č. {InvoiceNumber}

**Datum vystavení:** {IssueDate}
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

| Položka | Množství | Cena/MJ | Celkem bez DPH |
|---------|----------|---------|----------------|
| {ItemName} | {Quantity} ks | {UnitPrice} Kč | {ItemTotal} Kč |

**Celkem bez daně:** {TotalWithoutVAT} Kč
**DPH 21%:** {VATAmount} Kč
**CELKEM K ÚHRADĚ:** **{TotalAmount} Kč**

---

**Platební podmínky:**
Bankovní spojení: {BankAccount}
Variabilní symbol: {VariableSymbol}
```

**Templates/contract_template_1.md:**
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

**Odběratel:**
{CustomerName}
IČO: {CustomerICO}
Sídlo: {CustomerAddress}

---

## I. Předmět smlouvy

Předmětem této smlouvy je závazek dodavatele dodávat odběrateli {ProductCategory}
a závazek odběratele tyto dodávky odebírat a platit za ně dohodnutou cenu.

## II. Doba trvání smlouvy

Smlouva se uzavírá na dobu určitou od **{ValidFrom}** do **{ValidTo}**.

## III. Cenové podmínky

Odhadovaná roční hodnota dodávek: **{EstimatedAnnualValue} Kč** bez DPH.

## IV. Platební podmínky

Splatnost faktur je {PaymentTerms} dní od data vystavení faktury.

---

V {City} dne {SignDate}

____________________    ____________________
Za dodavatele           Za odběratele
```

**Templates/purchase_order_template_1.md:**
```markdown
# OBJEDNÁVKA

**Číslo objednávky:** {OrderNumber}
**Datum:** {OrderDate}

---

## Objednatel
{CustomerName}
IČO: {CustomerICO}
{CustomerAddress}

## Dodavatel
{SupplierName}
IČO: {SupplierICO}
{SupplierAddress}

---

## Objednáváme

| Položka | Popis | Množství | Jedn. cena | Celkem |
|---------|-------|----------|------------|--------|
| 1 | {ItemDescription} | {ItemQty} ks | {ItemPrice} Kč | {ItemTotal} Kč |

**Celková cena bez DPH:** {TotalWithoutVAT} Kč
**DPH 21%:** {VATAmount} Kč
**Celková cena s DPH:** **{TotalWithVAT} Kč**

---

## Dodací podmínky

**Místo dodání:** {DeliveryAddress}
**Termín dodání:** {DeliveryDate}

## Platební podmínky

Platba: {PaymentMethod}
Splatnost: {PaymentTerms}

---

Za správnost objednávky: ___________________
```

### 2. Document Generator Service

**Services/DocumentGeneratorService.cs:**
```csharp
using System.Text;

namespace TransactionManagement.Api.Services;

public interface IDocumentGeneratorService
{
    Task<string> GenerateDocumentAsync(string documentType, int variant, Dictionary<string, string>? customData = null);
}

public class DocumentGeneratorService : IDocumentGeneratorService
{
    private readonly ILogger<DocumentGeneratorService> _logger;
    private readonly Random _random = new Random();

    public DocumentGeneratorService(ILogger<DocumentGeneratorService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateDocumentAsync(
        string documentType,
        int variant,
        Dictionary<string, string>? customData = null)
    {
        // Validate inputs
        if (!new[] { "invoice", "contract", "purchase_order" }.Contains(documentType))
        {
            throw new ArgumentException($"Invalid document type: {documentType}");
        }

        if (variant < 1 || variant > 3)
        {
            throw new ArgumentException("Variant must be between 1 and 3");
        }

        // Load template
        var templatePath = GetTemplatePath(documentType, variant);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}");
        }

        var template = await File.ReadAllTextAsync(templatePath);

        // Generate data
        var data = GenerateDocumentData(documentType, customData);

        // Replace placeholders
        var document = ReplacePlaceholders(template, data);

        _logger.LogInformation("Generated {DocumentType} document (variant {Variant})", documentType, variant);

        return document;
    }

    private string GetTemplatePath(string documentType, int variant)
    {
        var fileName = $"{documentType}_template_{variant}.md";
        return Path.Combine("Templates", fileName);
    }

    private Dictionary<string, string> GenerateDocumentData(
        string documentType,
        Dictionary<string, string>? customData)
    {
        var data = new Dictionary<string, string>();

        // Common data
        data["SupplierName"] = GetOrGenerate(customData, "SupplierName", () => "ACME Corporation s.r.o.");
        data["SupplierICO"] = GetOrGenerate(customData, "SupplierICO", () => "12345678");
        data["SupplierAddress"] = GetOrGenerate(customData, "SupplierAddress", () => "Hlavní 123, 110 00 Praha 1");
        data["CustomerName"] = GetOrGenerate(customData, "CustomerName", () => "Test Client Ltd.");
        data["CustomerICO"] = GetOrGenerate(customData, "CustomerICO", () => "87654321");
        data["CustomerAddress"] = GetOrGenerate(customData, "CustomerAddress", () => "Nová 456, 120 00 Praha 2");

        // Document-specific data
        switch (documentType)
        {
            case "invoice":
                GenerateInvoiceData(data, customData);
                break;
            case "contract":
                GenerateContractData(data, customData);
                break;
            case "purchase_order":
                GeneratePurchaseOrderData(data, customData);
                break;
        }

        return data;
    }

    private void GenerateInvoiceData(Dictionary<string, string> data, Dictionary<string, string>? customData)
    {
        var today = DateTime.Today;
        var amount = _random.Next(10000, 100000);
        var vatRate = 0.21m;
        var totalWithoutVAT = amount;
        var vat = (decimal)Math.Round(totalWithoutVAT * vatRate, 2);
        var total = totalWithoutVAT + (int)vat;

        data["InvoiceNumber"] = GetOrGenerate(customData, "InvoiceNumber", () => $"2025-{_random.Next(100, 999):D3}");
        data["IssueDate"] = GetOrGenerate(customData, "IssueDate", () => today.ToString("dd.MM.yyyy"));
        data["DueDate"] = GetOrGenerate(customData, "DueDate", () => today.AddDays(14).ToString("dd.MM.yyyy"));
        data["VariableSymbol"] = GetOrGenerate(customData, "VariableSymbol", () => _random.Next(10000, 99999).ToString());

        data["ItemDescription"] = GetOrGenerate(customData, "ItemDescription", () => "Software licence");
        data["ItemQuantity"] = GetOrGenerate(customData, "ItemQuantity", () => "1");
        data["ItemPrice"] = GetOrGenerate(customData, "ItemPrice", () => totalWithoutVAT.ToString("N0"));
        data["ItemTotal"] = GetOrGenerate(customData, "ItemTotal", () => totalWithoutVAT.ToString("N0"));
        data["ItemName"] = data["ItemDescription"];
        data["Quantity"] = data["ItemQuantity"];
        data["UnitPrice"] = data["ItemPrice"];

        data["TotalWithoutVAT"] = totalWithoutVAT.ToString("N0");
        data["BaseAmount"] = totalWithoutVAT.ToString("N0");
        data["VAT"] = vat.ToString("N0");
        data["VATAmount"] = vat.ToString("N0");
        data["TotalAmount"] = total.ToString("N0");

        data["BankAccount"] = GetOrGenerate(customData, "BankAccount", () => "123456789/0100");
        data["ServiceDescription"] = GetOrGenerate(customData, "ServiceDescription", () => "Poskytnutí software licence na dobu 12 měsíců");
        data["DetailedDescription"] = data["ServiceDescription"];
    }

    private void GenerateContractData(Dictionary<string, string> data, Dictionary<string, string>? customData)
    {
        var today = DateTime.Today;

        data["ContractNumber"] = GetOrGenerate(customData, "ContractNumber", () => $"SM-{today.Year}-{_random.Next(100, 999)}");
        data["SignDate"] = GetOrGenerate(customData, "SignDate", () => today.ToString("dd.MM.yyyy"));
        data["ValidFrom"] = GetOrGenerate(customData, "ValidFrom", () => today.ToString("dd.MM.yyyy"));
        data["ValidTo"] = GetOrGenerate(customData, "ValidTo", () => today.AddYears(1).ToString("dd.MM.yyyy"));
        data["ProductCategory"] = GetOrGenerate(customData, "ProductCategory", () => "software a IT služby");
        data["EstimatedAnnualValue"] = GetOrGenerate(customData, "EstimatedAnnualValue", () => _random.Next(500000, 2000000).ToString("N0"));
        data["PaymentTerms"] = GetOrGenerate(customData, "PaymentTerms", () => "30");
        data["City"] = GetOrGenerate(customData, "City", () => "Praha");
    }

    private void GeneratePurchaseOrderData(Dictionary<string, string> data, Dictionary<string, string>? customData)
    {
        var today = DateTime.Today;
        var amount = _random.Next(20000, 150000);
        var vatRate = 0.21m;
        var totalWithoutVAT = amount;
        var vat = (decimal)Math.Round(totalWithoutVAT * vatRate, 2);
        var total = totalWithoutVAT + (int)vat;

        data["OrderNumber"] = GetOrGenerate(customData, "OrderNumber", () => $"OBJ-{today.Year}-{_random.Next(100, 999)}");
        data["OrderDate"] = GetOrGenerate(customData, "OrderDate", () => today.ToString("dd.MM.yyyy"));
        data["ItemDescription"] = GetOrGenerate(customData, "ItemDescription", () => "Serverové komponenty");
        data["ItemQty"] = GetOrGenerate(customData, "ItemQty", () => _random.Next(5, 20).ToString());
        data["ItemPrice"] = GetOrGenerate(customData, "ItemPrice", () => (totalWithoutVAT / int.Parse(data["ItemQty"])).ToString("N0"));
        data["ItemTotal"] = GetOrGenerate(customData, "ItemTotal", () => totalWithoutVAT.ToString("N0"));

        data["TotalWithoutVAT"] = totalWithoutVAT.ToString("N0");
        data["VATAmount"] = vat.ToString("N0");
        data["TotalWithVAT"] = total.ToString("N0");

        data["DeliveryAddress"] = GetOrGenerate(customData, "DeliveryAddress", () => data["CustomerAddress"]);
        data["DeliveryDate"] = GetOrGenerate(customData, "DeliveryDate", () => today.AddDays(14).ToString("dd.MM.yyyy"));
        data["PaymentMethod"] = GetOrGenerate(customData, "PaymentMethod", () => "Bankovní převod");
        data["PaymentTerms"] = GetOrGenerate(customData, "PaymentTerms", () => "30 dnů");
    }

    private string GetOrGenerate(
        Dictionary<string, string>? customData,
        string key,
        Func<string> generator)
    {
        if (customData != null && customData.ContainsKey(key))
        {
            return customData[key];
        }

        return generator();
    }

    private string ReplacePlaceholders(string template, Dictionary<string, string> data)
    {
        var result = template;

        foreach (var kvp in data)
        {
            var placeholder = $"{{{kvp.Key}}}";
            result = result.Replace(placeholder, kvp.Value);
        }

        return result;
    }
}
```

### 3. Documents Controller

**Controllers/DocumentsController.cs:**
```csharp
using Microsoft.AspNetCore.Mvc;
using TransactionManagement.Api.Services;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentGeneratorService _generatorService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentGeneratorService generatorService,
        ILogger<DocumentsController> logger)
    {
        _generatorService = generatorService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/documents/generate - Vygeneruje vzorový dokument
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateDocument([FromBody] GenerateDocumentRequest request)
    {
        try
        {
            var document = await _generatorService.GenerateDocumentAsync(
                request.DocumentType,
                request.Variant,
                request.CustomData);

            var fileName = $"{request.DocumentType}_{request.Variant}_{DateTime.Now:yyyyMMdd_HHmmss}.md";

            return File(
                System.Text.Encoding.UTF8.GetBytes(document),
                "text/markdown",
                fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating document");
            return StatusCode(500, new { message = "Error generating document", error = ex.Message });
        }
    }
}

public class GenerateDocumentRequest
{
    public string DocumentType { get; set; } = string.Empty; // "invoice", "contract", "purchase_order"
    public int Variant { get; set; } = 1; // 1, 2, or 3
    public Dictionary<string, string>? CustomData { get; set; }
}
```

### 4. Registrace v Program.cs

```csharp
builder.Services.AddScoped<IDocumentGeneratorService, DocumentGeneratorService>();
```

### 5. Ujisti se, že Templates jsou zkopírovány

Přidej do `.csproj` souboru:

```xml
<ItemGroup>
  <None Update="Templates\*.md">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## Testování

### 1. Vygeneruj fakturu (varianta 1)

```bash
curl -X POST http://localhost:5000/api/documents/generate \
  -H "Content-Type: application/json" \
  -d '{
    "documentType": "invoice",
    "variant": 1
  }' \
  -o generated_invoice_1.md
```

### 2. Vygeneruj smlouvu s custom daty

```bash
curl -X POST http://localhost:5000/api/documents/generate \
  -H "Content-Type: application/json" \
  -d '{
    "documentType": "contract",
    "variant": 1,
    "customData": {
      "SupplierName": "My Company s.r.o.",
      "EstimatedAnnualValue": "1500000"
    }
  }' \
  -o generated_contract.md
```

### 3. Vygeneruj všechny varianty faktur

```bash
for i in 1 2 3; do
  curl -X POST http://localhost:5000/api/documents/generate \
    -H "Content-Type: application/json" \
    -d "{\"documentType\": \"invoice\", \"variant\": $i}" \
    -o "invoice_variant_$i.md"
done
```

### 4. Otevři vygenerovaný dokument

```bash
cat generated_invoice_1.md
```

Měl bys vidět kompletně vyplněnou fakturu s náhodnými, ale realistickými daty.

## Ověření

Po dokončení:

1. ✅ Všechny 3 šablony pro faktury existují
2. ✅ Šablony pro smlouvy a objednávky existují
3. ✅ DocumentGeneratorService generuje realistická data
4. ✅ Placeholder replacement funguje správně
5. ✅ Custom data jsou správně použita
6. ✅ Vygenerované dokumenty jsou validní Markdown
7. ✅ Soubory se stahují s correct MIME typem

## Rozšíření (volitelné)

### Přidání více variant

Vytvoř další šablony:
- `invoice_template_2.md`
- `invoice_template_3.md`
- `contract_template_2.md`
- `contract_template_3.md`
- `purchase_order_template_2.md`
- `purchase_order_template_3.md`

### Integrace s transakcemi

Můžeš rozšířit `GenerateDocument` o možnost použít data z existující transakce:

```csharp
[HttpPost("generate-for-transaction/{transactionId}")]
public async Task<IActionResult> GenerateForTransaction(
    int transactionId,
    [FromBody] GenerateDocumentRequest request)
{
    var transaction = await _context.Transactions.FindAsync(transactionId);
    if (transaction == null)
        return NotFound();

    var customData = new Dictionary<string, string>
    {
        ["Amount"] = transaction.Amount.ToString("N0"),
        ["CompanyName"] = transaction.CompanyName ?? "",
        ["CompanyICO"] = transaction.CompanyId,
        ["Description"] = transaction.Description
    };

    var document = await _generatorService.GenerateDocumentAsync(
        request.DocumentType,
        request.Variant,
        customData);

    // ... return file
}
```

## Výstup této fáze

✅ 9 Markdown šablon (3 typy × 3 varianty)
✅ DocumentGeneratorService s placeholder replacementem
✅ Generování realistických náhodných dat
✅ Support pro custom data override
✅ REST API endpoint pro generování
✅ Download dokumentů jako .md soubory

## Další krok

→ **10_frontend_react.md** - React aplikace se shadcn/ui komponentami
