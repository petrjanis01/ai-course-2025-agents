# 12 - Testování a Validace

## Cíl
Provést kompletní end-to-end testování všech funkcionalit projektu a validovat, že POC splňuje všechny požadavky.

## Prerekvizity
- Dokončený krok 11 (Docker Compose)
- Všechny služby běžící

## Test Suite

### 1. Základní Funkčnost - Transactions CRUD

#### Test 1.1: Vytvoření transakce
```bash
curl -X POST http://localhost:5000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "description": "Test transakce - Nákup serveru",
    "amount": 45000,
    "companyId": "12345678",
    "companyName": "ACME Corporation s.r.o.",
    "transactionType": "expense",
    "transactionDate": "2025-01-15T00:00:00Z"
  }'
```

**Očekávaný výsledek:**
- Status: 201 Created
- Response obsahuje ID nové transakce
- TransactionDate je správně uloženo

#### Test 1.2: Seznam transakcí
```bash
curl http://localhost:5000/api/transactions
```

**Očekávaný výsledek:**
- Status: 200 OK
- Array s transakcemi
- Každá transakce má `attachmentCount`

#### Test 1.3: Detail transakce
```bash
curl http://localhost:5000/api/transactions/1
```

**Očekávaný výsledek:**
- Status: 200 OK
- Detail transakce včetně pole `attachments`

#### Test 1.4: Aktualizace transakce
```bash
curl -X PUT http://localhost:5000/api/transactions/1 \
  -H "Content-Type: application/json" \
  -d '{
    "description": "Aktualizovaný popis",
    "amount": 50000,
    "companyId": "12345678",
    "companyName": "ACME Corporation s.r.o.",
    "transactionType": "expense",
    "transactionDate": "2025-01-15T00:00:00Z"
  }'
```

**Očekávaný výsledek:**
- Status: 200 OK
- `updatedAt` je novější než `createdAt`

### 2. Attachments a File Storage

#### Test 2.1: Upload přílohy

Vytvoř testovací soubor `test-invoice.md`:
```markdown
# FAKTURA

**Číslo faktury:** 2025-001
**Datum vystavení:** 15.1.2025

## Dodavatel
ACME Corporation s.r.o.
IČO: 12345678

## Položky
Software licence - 25000 Kč

**Celkem k úhradě:** 25000 Kč
```

Upload:
```bash
curl -X POST http://localhost:5000/api/transactions/1/attachments \
  -F "file=@test-invoice.md"
```

**Očekávaný výsledek:**
- Status: 201 Created
- Response obsahuje attachment ID
- `processingStatus` je "pending" nebo "processing"

#### Test 2.2: Čekání na zpracování
```bash
# Čekej 5-10 sekund, pak zkontroluj status
sleep 10
curl http://localhost:5000/api/transactions/1/attachments/1
```

**Očekávaný výsledek:**
- `processingStatus` je "completed"
- `category` je "invoice"
- `processedAt` je vyplněno

#### Test 2.3: Download přílohy
```bash
curl http://localhost:5000/api/transactions/1/attachments/1/download \
  -o downloaded.md

cat downloaded.md
```

**Očekávaný výsledek:**
- Soubor je identický s původním
- MIME type: text/markdown

### 3. Document Processing

#### Test 3.1: Ověření kategorizace

Vytvoř různé typy dokumentů:

**contract.md:**
```markdown
# RÁMCOVÁ SMLOUVA O DODÁVKÁCH

Číslo smlouvy: SM-2025-001

Předmětem této smlouvy je závazek dodavatele...
Smlouva se uzavírá na dobu určitou od 1.1.2025 do 31.12.2025.
```

**purchase_order.md:**
```markdown
# OBJEDNÁVKA

Číslo objednávky: OBJ-2025-001

Objednáváme:
- Server Dell PowerEdge - 5 ks - 45000 Kč
```

Upload všechny 3 typy:
```bash
curl -X POST http://localhost:5000/api/transactions/1/attachments -F "file=@test-invoice.md"
curl -X POST http://localhost:5000/api/transactions/1/attachments -F "file=@contract.md"
curl -X POST http://localhost:5000/api/transactions/1/attachments -F "file=@purchase_order.md"

# Čekej na zpracování
sleep 15
```

Ověř kategorie:
```bash
curl http://localhost:5000/api/transactions/1
```

**Očekávaný výsledek:**
- Invoice má category "invoice"
- Contract má category "contract"
- Purchase order má category "purchase_order"

#### Test 3.2: Ověření chunking a embeddings v Qdrantu
```bash
curl http://localhost:6333/collections/transaction_documents
```

**Očekávaný výsledek:**
- `points_count` > 0
- `vectors_count` > 0
- `config.params.vectors.size` = 384

Detailnější info:
```bash
curl http://localhost:6333/collections/transaction_documents/points/scroll \
  -H "Content-Type: application/json" \
  -d '{"limit": 1, "with_payload": true, "with_vector": false}'
```

**Očekávaný výsledek:**
- Payload obsahuje: attachment_id, transaction_id, category, content, has_amounts, has_dates, word_count

### 4. Chat Functionality

#### Test 4.1: Databázové dotazy
```bash
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Kolik transakcí bylo vytvořeno?"
  }'
```

**Očekávaný výsledek:**
- Odpověď obsahuje správný počet transakcí
- `metadata.responseTime` < 5 sekund

#### Test 4.2: Agregační dotazy
```bash
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaká byla celková částka výdajů?"
  }'
```

**Očekávaný výsledek:**
- Odpověď obsahuje sum výdajů
- Číslo odpovídá skutečnosti

#### Test 4.3: Sémantické vyhledávání
```bash
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Kolik faktur máme?"
  }'
```

**Očekávaný výsledek:**
- Odpověď počítá dokumenty s category "invoice"
- Agent použil DocumentSearchAgent

#### Test 4.4: Multi-turn konverzace
```bash
# Zpráva 1
RESPONSE=$(curl -s -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{"message": "Kolik máme transakcí?"}')

SESSION_ID=$(echo $RESPONSE | jq -r '.sessionId')

# Zpráva 2 (s kontextem)
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d "{
    \"message\": \"A kolik z nich má přílohy?\",
    \"sessionId\": \"$SESSION_ID\",
    \"conversationHistory\": [
      {\"role\": \"user\", \"content\": \"Kolik máme transakcí?\"},
      {\"role\": \"assistant\", \"content\": \"$(echo $RESPONSE | jq -r '.message')\"}
    ]
  }"
```

**Očekávaný výsledek:**
- Odpověď reflektuje předchozí kontext
- Asistent chápe, že "z nich" = z transakcí

### 5. Langfuse Monitoring

#### Test 5.1: Ověření tracingu
1. Otevři http://localhost:3030
2. Přejdi do "Traces"
3. Ověř, že vidíš traces z předchozích testů

**Očekávaný výsledek:**
- Trace "categorize_document" pro každý upload
- Trace "create_embedding" pro každý chunk
- Trace "chat_query" pro každou zprávu

#### Test 5.2: Detail trace
Otevři libovolný trace a ověř:
- Input a output jsou viditelné
- Metadata obsahují duration, model
- Timeline ukazuje jednotlivé kroky

### 6. Document Generator

#### Test 6.1: Vygeneruj všechny typy dokumentů
```bash
# Faktury
for i in 1 2 3; do
  curl -X POST http://localhost:5000/api/documents/generate \
    -H "Content-Type: application/json" \
    -d "{\"documentType\": \"invoice\", \"variant\": $i}" \
    -o "generated_invoice_$i.md"
done

# Smlouvy
for i in 1 2 3; do
  curl -X POST http://localhost:5000/api/documents/generate \
    -H "Content-Type: application/json" \
    -d "{\"documentType\": \"contract\", \"variant\": $i}" \
    -o "generated_contract_$i.md"
done

# Objednávky
for i in 1 2 3; do
  curl -X POST http://localhost:5000/api/documents/generate \
    -H "Content-Type: application/json" \
    -d "{\"documentType\": \"purchase_order\", \"variant\": $i}" \
    -o "generated_order_$i.md"
done
```

Ověř soubory:
```bash
ls -lh generated_*.md
cat generated_invoice_1.md
```

**Očekávaný výsledek:**
- Všech 9 souborů existuje
- Obsahují vyplněná realistická data
- Žádné placeholder {xxx} nejsou ponechány

### 7. Frontend Testing

#### Test 7.1: UI komponenty
1. Otevři http://localhost:3000
2. Ověř, že vidíš seznam transakcí
3. Klikni na transakci → měl by se zobrazit detail
4. Ověř, že shadcn/ui komponenty vypadají správně

#### Test 7.2: Upload přes UI
1. Přejdi na detail transakce
2. Klikni "Nahrát přílohu"
3. Vyber Markdown soubor
4. Ověř, že se objeví v seznamu příloh
5. Čekej na změnu statusu na "completed"

#### Test 7.3: Chat UI
1. Přejdi na http://localhost:3000/chat
2. Napiš: "Kolik transakcí máme?"
3. Ověř odpověď
4. Napiš follow-up: "A kolik z nich má přílohy?"
5. Ověř, že kontext je zachován

### 8. Performance Testing

#### Test 8.1: Embedding rychlost
```bash
time curl -X POST http://localhost:5000/api/test/embedding \
  -H "Content-Type: application/json" \
  -d '"Test text pro embedding"'
```

**Očekávaný výsledek:**
- < 1 sekunda pro krátký text

#### Test 8.2: Chat response time
```bash
time curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{"message": "Kolik transakcí máme?"}'
```

**Očekávaný výsledek:**
- < 5 sekund pro jednoduchý dotaz

#### Test 8.3: Document processing
```bash
# Upload velkého dokumentu
# Měř čas od uploadu do completed statusu
```

**Očekávaný výsledek:**
- < 30 sekund pro dokument ~2000 slov

### 9. Error Handling

#### Test 9.1: Nevalidní data
```bash
# Missing required fields
curl -X POST http://localhost:5000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{"description": "Test"}'
```

**Očekávaný výsledek:**
- Status: 400 Bad Request

#### Test 9.2: Non-Markdown upload
```bash
echo "Plain text file" > test.txt
curl -X POST http://localhost:5000/api/transactions/1/attachments \
  -F "file=@test.txt"
```

**Očekávaný výsledek:**
- Status: 400 Bad Request
- Error message o neplatném formátu

#### Test 9.3: Neexistující transakce
```bash
curl http://localhost:5000/api/transactions/99999
```

**Očekávaný výsledek:**
- Status: 404 Not Found

## Test Checklist

Po provedení všech testů ověř:

### Backend API
- [ ] CRUD operace pro transakce fungují
- [ ] Upload/download příloh funguje
- [ ] File storage ukládá soubory správně
- [ ] Validace inputů funguje

### Document Processing
- [ ] Dokumenty jsou automaticky zpracovány
- [ ] Kategorizace funguje správně (3/3 typy)
- [ ] Chunking rozděluje text na části
- [ ] Metadata enrichment detekuje amounts/dates
- [ ] Embeddings jsou vytvořeny (384D)
- [ ] Indexování do Qdrantu funguje

### Chat & RAG
- [ ] Databázové dotazy fungují
- [ ] Agregace (COUNT, SUM) fungují
- [ ] Sémantické vyhledávání funguje
- [ ] Multi-turn konverzace funguje
- [ ] Function calling je automatický

### Monitoring
- [ ] Langfuse zachycuje všechny LLM cally
- [ ] Traces jsou viditelné v UI
- [ ] Metadata jsou kompletní

### Document Generator
- [ ] Všech 9 variant generuje správně
- [ ] Data jsou realistická
- [ ] Custom data override funguje

### Frontend
- [ ] Seznam transakcí se zobrazuje
- [ ] Detail funguje
- [ ] Upload funguje přes UI
- [ ] Download funguje
- [ ] Chat rozhraní funguje
- [ ] shadcn/ui komponenty vypadají správně

### Infrastructure
- [ ] Docker Compose spouští všechny služby
- [ ] Health checks fungují
- [ ] Volumes perzistují data
- [ ] Network komunikace funguje
- [ ] API dosáhne na Ollama na hostu

## Regression Test Script

**test_all.sh:**
```bash
#!/bin/bash

echo "=== Transaction Management POC - Test Suite ==="

# Test 1: Health checks
echo -n "Testing health checks... "
if curl -sf http://localhost:5000/api/transactions > /dev/null; then
    echo "✓ API OK"
else
    echo "✗ API FAIL"
    exit 1
fi

# Test 2: Create transaction
echo -n "Creating transaction... "
TRANSACTION_ID=$(curl -s -X POST http://localhost:5000/api/transactions \
  -H "Content-Type: application/json" \
  -d '{"description":"Test","amount":1000,"companyId":"12345678","companyName":"Test","transactionType":"expense","transactionDate":"2025-01-01T00:00:00Z"}' \
  | jq -r '.id')

if [ ! -z "$TRANSACTION_ID" ]; then
    echo "✓ Created #$TRANSACTION_ID"
else
    echo "✗ FAIL"
    exit 1
fi

# Test 3: Upload attachment
echo -n "Uploading attachment... "
echo "# Test Invoice" > /tmp/test.md
ATTACHMENT_ID=$(curl -s -X POST http://localhost:5000/api/transactions/$TRANSACTION_ID/attachments \
  -F "file=@/tmp/test.md" \
  | jq -r '.id')

if [ ! -z "$ATTACHMENT_ID" ]; then
    echo "✓ Uploaded #$ATTACHMENT_ID"
else
    echo "✗ FAIL"
    exit 1
fi

# Test 4: Wait for processing
echo -n "Waiting for processing... "
sleep 10
STATUS=$(curl -s http://localhost:5000/api/transactions/$TRANSACTION_ID/attachments/$ATTACHMENT_ID \
  | jq -r '.processingStatus')

if [ "$STATUS" == "completed" ]; then
    echo "✓ Processed"
else
    echo "✗ Status: $STATUS"
fi

# Test 5: Chat query
echo -n "Testing chat... "
CHAT_RESPONSE=$(curl -s -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{"message":"Kolik transakcí máme?"}' \
  | jq -r '.message')

if [ ! -z "$CHAT_RESPONSE" ]; then
    echo "✓ Response received"
else
    echo "✗ FAIL"
    exit 1
fi

echo ""
echo "=== All tests passed! ==="
```

Spuštění:
```bash
chmod +x test_all.sh
./test_all.sh
```

## Výstup této fáze

✅ Kompletní test suite pro všechny funkcionality
✅ Performance benchmarky
✅ Error handling ověření
✅ Regression test script
✅ Checklist pro validaci
✅ End-to-end testovací scénáře

## Závěr POC

Po úspěšném dokončení všech testů máš funkční POC projekt s:

1. **Microsoft Agent Framework** - ChatOrchestrator, DatabaseAgent, DocumentSearchAgent
2. **RAG implementace** - Embeddings, Qdrant, sémantické vyhledávání
3. **Langfuse monitoring** - Kompletní tracing všech LLM calls
4. **Document processing** - Chunking, metadata enrichment, kategorizace
5. **Full-stack aplikace** - .NET API + React frontend
6. **Docker orchestrace** - Kompletní infrastruktura v Dockeru

Gratulujeme! 🎉
