# 09 Frontend - Qdrant & Embeddings Test

## Cíl
Implementovat UI pro testování Qdrant embeddings a vektorového vyhledávání.

## Prerekvizity
- Dokončený krok 10 (Qdrant & Embeddings API)
- Frontend setup (krok 04)

## Kroky implementace

### 1. Test API service

**src/services/qdrant-service.ts:**
```typescript
import { apiClient } from './api-client';

export interface EmbeddingTestResult {
  text: string;
  dimensions: number;
  sample: string;
}

export interface SearchResult {
  attachmentId: number;
  fileName: string;
  category: string;
  score: number;
  contentPreview: string;
  hasAmounts: boolean;
  hasDates: boolean;
}

export interface SearchTestResult {
  query: string;
  resultsCount: number;
  results: SearchResult[];
}

export const qdrantService = {
  async testEmbedding(text: string): Promise<EmbeddingTestResult> {
    const response = await apiClient.post<EmbeddingTestResult>(
      '/api/test/embedding',
      JSON.stringify(text),
      {
        headers: {
          'Content-Type': 'application/json'
        }
      }
    );
    return response.data;
  },

  async testSearch(query: string): Promise<SearchTestResult> {
    const response = await apiClient.post<SearchTestResult>(
      '/api/test/search',
      JSON.stringify(query),
      {
        headers: {
          'Content-Type': 'application/json'
        }
      }
    );
    return response.data;
  }
};
```

### 2. Qdrant Test komponenta

**src/components/QdrantTest/QdrantTest.tsx:**
```tsx
import React, { useState } from 'react';
import { qdrantService, EmbeddingTestResult, SearchTestResult } from '../../services/qdrant-service';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { Label } from '../ui/label';
import { Alert, AlertDescription } from '../ui/alert';
import { Badge } from '../ui/badge';
import { Separator } from '../ui/separator';
import { Search, Code, AlertCircle, CheckCircle2, FileText } from 'lucide-react';

export const QdrantTest: React.FC = () => {
  const [embeddingText, setEmbeddingText] = useState('Toto je testovací text pro embedding');
  const [embeddingResult, setEmbeddingResult] = useState<EmbeddingTestResult | null>(null);
  const [embeddingLoading, setEmbeddingLoading] = useState(false);
  const [embeddingError, setEmbeddingError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('smlouva o pronájmu');
  const [searchResult, setSearchResult] = useState<SearchTestResult | null>(null);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const handleTestEmbedding = async () => {
    if (!embeddingText.trim()) return;

    try {
      setEmbeddingLoading(true);
      setEmbeddingError(null);
      const result = await qdrantService.testEmbedding(embeddingText);
      setEmbeddingResult(result);
    } catch (err: any) {
      setEmbeddingError(err.response?.data?.message || 'Chyba při generování embeddingu');
    } finally {
      setEmbeddingLoading(false);
    }
  };

  const handleTestSearch = async () => {
    if (!searchQuery.trim()) return;

    try {
      setSearchLoading(true);
      setSearchError(null);
      const result = await qdrantService.testSearch(searchQuery);
      setSearchResult(result);
    } catch (err: any) {
      setSearchError(err.response?.data?.message || 'Chyba při vyhledávání');
    } finally {
      setSearchLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Testování Qdrant & Embeddings</h1>
        <p className="text-muted-foreground">
          Testování vektorových embeddingů a sémantického vyhledávání
        </p>
      </div>

      {/* Embedding Test */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Code className="h-5 w-5" />
            Test Embeddings
          </CardTitle>
          <CardDescription>
            Vygeneruj embedding vector pomocí all-MiniLM-L6-v2 modelu (384 dimenzí)
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="embeddingText">Testovací text</Label>
            <Input
              id="embeddingText"
              value={embeddingText}
              onChange={(e) => setEmbeddingText(e.target.value)}
              placeholder="Zadej text pro embedding..."
            />
          </div>

          <Button
            onClick={handleTestEmbedding}
            disabled={embeddingLoading || !embeddingText.trim()}
          >
            {embeddingLoading ? 'Generuji...' : 'Vygenerovat Embedding'}
          </Button>

          {embeddingError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{embeddingError}</AlertDescription>
            </Alert>
          )}

          {embeddingResult && (
            <div className="mt-4 space-y-3">
              <Alert>
                <CheckCircle2 className="h-4 w-4" />
                <AlertDescription>
                  Embedding úspěšně vygenerován
                </AlertDescription>
              </Alert>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1">
                  <Label className="text-sm text-muted-foreground">Text</Label>
                  <p className="font-mono text-sm">{embeddingResult.text}</p>
                </div>
                <div className="space-y-1">
                  <Label className="text-sm text-muted-foreground">Dimenze</Label>
                  <Badge variant="secondary" className="font-mono">
                    {embeddingResult.dimensions}
                  </Badge>
                </div>
              </div>

              <div className="space-y-1">
                <Label className="text-sm text-muted-foreground">
                  Vector (prvních 10 hodnot)
                </Label>
                <div className="bg-slate-100 p-3 rounded-md">
                  <code className="text-xs font-mono text-slate-700">
                    {embeddingResult.sample}
                  </code>
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Separator />

      {/* Search Test */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Search className="h-5 w-5" />
            Test Vektorového Vyhledávání
          </CardTitle>
          <CardDescription>
            Sémantické vyhledávání v dokumentech pomocí Qdrant
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Alert variant="default">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              Pro test je potřeba mít nahrané a zpracované dokumenty v systému.
              Nejdříve nahrajte přílohy k transakcím.
            </AlertDescription>
          </Alert>

          <div className="space-y-2">
            <Label htmlFor="searchQuery">Vyhledávací dotaz</Label>
            <Input
              id="searchQuery"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Zadej vyhledávací dotaz..."
            />
          </div>

          <Button
            onClick={handleTestSearch}
            disabled={searchLoading || !searchQuery.trim()}
          >
            {searchLoading ? 'Vyhledávám...' : 'Vyhledat'}
          </Button>

          {searchError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{searchError}</AlertDescription>
            </Alert>
          )}

          {searchResult && (
            <div className="mt-4 space-y-3">
              <Alert>
                <CheckCircle2 className="h-4 w-4" />
                <AlertDescription>
                  Nalezeno {searchResult.resultsCount} výsledků pro dotaz: "{searchResult.query}"
                </AlertDescription>
              </Alert>

              {searchResult.resultsCount === 0 ? (
                <div className="text-center py-8 text-muted-foreground">
                  <FileText className="h-12 w-12 mx-auto mb-2 opacity-50" />
                  <p>Žádné výsledky nenalezeny</p>
                  <p className="text-sm">Zkuste nahrát dokumenty nebo změnit dotaz</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {searchResult.results.map((result, index) => (
                    <Card key={index}>
                      <CardContent className="pt-4">
                        <div className="space-y-2">
                          <div className="flex items-start justify-between">
                            <div className="flex-1">
                              <div className="flex items-center gap-2">
                                <FileText className="h-4 w-4 text-muted-foreground" />
                                <span className="font-medium">{result.fileName}</span>
                              </div>
                              <div className="flex items-center gap-2 mt-1">
                                <Badge variant="outline">{result.category}</Badge>
                                {result.hasAmounts && (
                                  <Badge variant="secondary" className="text-xs">
                                    Obsahuje částky
                                  </Badge>
                                )}
                                {result.hasDates && (
                                  <Badge variant="secondary" className="text-xs">
                                    Obsahuje data
                                  </Badge>
                                )}
                              </div>
                            </div>
                            <Badge className="ml-4">
                              Skóre: {(result.score * 100).toFixed(1)}%
                            </Badge>
                          </div>

                          <div className="mt-3 p-3 bg-slate-50 rounded-md">
                            <p className="text-sm text-slate-700 line-clamp-3">
                              {result.contentPreview}
                            </p>
                          </div>

                          <div className="text-xs text-muted-foreground">
                            Attachment ID: {result.attachmentId}
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Help Section */}
      <Card>
        <CardHeader>
          <CardTitle>Nápověda</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div>
            <h4 className="font-medium mb-2">Testovací dotazy:</h4>
            <ul className="list-disc list-inside space-y-1 text-sm text-muted-foreground">
              <li>smlouva o pronájmu</li>
              <li>faktura software</li>
              <li>objednávka materiál</li>
              <li>dokumenty s částkou 50000 Kč</li>
              <li>smlouvy platné do konce roku</li>
            </ul>
          </div>
          <Separator />
          <div>
            <h4 className="font-medium mb-2">Technické detaily:</h4>
            <ul className="list-disc list-inside space-y-1 text-sm text-muted-foreground">
              <li>Embedding model: all-MiniLM-L6-v2 (384 dimenzí)</li>
              <li>Vektorová databáze: Qdrant</li>
              <li>Distance metric: Cosine similarity</li>
              <li>Minimální skóre: 0.5 (50%)</li>
            </ul>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};
```

### 3. Aktualizace routing

**src/App.tsx** - přidej route:
```tsx
import { QdrantTest } from './components/QdrantTest/QdrantTest';

// V Routes:
<Route path="/qdrant-test" element={<QdrantTest />} />

// V navigation:
<Link
  to="/qdrant-test"
  className="text-sm font-medium hover:text-primary transition-colors"
>
  Test Qdrant
</Link>
```

## Testování

### 1. Test embeddings

1. Otevři http://localhost:3000/qdrant-test
2. Zadej testovací text (např. "Toto je faktura za software")
3. Klikni "Vygenerovat Embedding"
4. Ověř:
   - Dimension count = 384
   - Vector values jsou zobrazeny
   - Success zpráva se zobrazuje

### 2. Test vyhledávání

**Příprava:**
1. Nejdříve nahraj nějaké dokumenty přes UI transakcí
2. Počkej na zpracování (processing status = completed)

**Test:**
1. Zadej dotaz: "faktura"
2. Klikni "Vyhledat"
3. Ověř:
   - Zobrazí se relevantní dokumenty
   - Skóre je mezi 50-100%
   - Content preview se zobrazuje
   - Badges (kategorie, has_amounts, has_dates) jsou správné

### 3. Různé testovací dotazy

Zkus další dotazy:
- "smlouva pronájem"
- "objednávka hardware"
- "dokumenty s částkou"
- "smlouvy platné 2025"

## Ověření

Po dokončení:

1. ✅ Embedding test generuje 384-dimenzní vektory
2. ✅ Vector values se zobrazují (prvních 10)
3. ✅ Search funguje a vrací relevantní výsledky
4. ✅ Skóre similarity je zobrazeno
5. ✅ Content preview je viditelný
6. ✅ Metadata badges (category, has_amounts, has_dates) fungují
7. ✅ Error handling funguje (když Qdrant není dostupný)
8. ✅ Loading states jsou zobrazeny

## Výstup této fáze

✅ QdrantTest komponenta pro testování embeddings
✅ Test embedding generation s vizualizací
✅ Test sémantického vyhledávání
✅ Zobrazení výsledků s relevancy scores
✅ Content preview pro nalezené chunky
✅ Metadata visualization (badges)
✅ Help section s příklady dotazů

## Další krok

→ **10_document_processing.md** - Backend document chunking a processing agent
