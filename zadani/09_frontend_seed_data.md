# 08 Frontend - Seed Data Management

## Cíl
Implementovat UI pro generování testovacích dat a zobrazení statistik databáze.

## Prerekvizity
- Dokončený krok 08 (Seed Data API)
- Frontend setup (krok 04)

## Kroky implementace

### 1. Rozšíření API service

**src/services/data-service.ts:**
```typescript
import { apiClient } from './api-client';

export interface DataStats {
  totalTransactions: number;
  totalAttachments: number;
  expenses: {
    count: number;
    total: number;
  };
  income: {
    count: number;
    total: number;
  };
  net: number;
  topCompanies: Array<{
    companyId: string;
    companyName: string;
    transactionCount: number;
    totalAmount: number;
  }>;
}

export interface SeedResponse {
  message: string;
  transactionsCreated: number;
}

export const dataService = {
  async seedData(clearExisting: boolean = false): Promise<SeedResponse> {
    const response = await apiClient.post<SeedResponse>(
      `/api/data/seed?clearExisting=${clearExisting}`
    );
    return response.data;
  },

  async getStats(): Promise<DataStats> {
    const response = await apiClient.get<DataStats>('/api/data/stats');
    return response.data;
  }
};
```

### 2. Data Management komponenta

**src/components/DataManagement/DataManagement.tsx:**
```tsx
import React, { useEffect, useState } from 'react';
import { dataService, DataStats } from '../../services/data-service';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Alert, AlertDescription } from '../ui/alert';
import { Badge } from '../ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../ui/table';
import { Database, AlertCircle, CheckCircle, TrendingUp, TrendingDown, Building2 } from 'lucide-react';
import { formatCurrency } from '../../lib/format';

export const DataManagement: React.FC = () => {
  const [stats, setStats] = useState<DataStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [seeding, setSeeding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const loadStats = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await dataService.getStats();
      setStats(data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Nepodařilo se načíst statistiky');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadStats();
  }, []);

  const handleSeed = async (clearExisting: boolean) => {
    if (clearExisting && !window.confirm('Opravdu chcete smazat všechna existující data a vygenerovat nová?')) {
      return;
    }

    try {
      setSeeding(true);
      setError(null);
      setSuccess(null);
      const result = await dataService.seedData(clearExisting);
      setSuccess(result.message);
      await loadStats(); // Refresh stats
    } catch (err: any) {
      setError(err.response?.data?.message || 'Chyba při generování dat');
    } finally {
      setSeeding(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <div className="text-lg font-medium">Načítám statistiky...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Správa dat</h1>
        <p className="text-muted-foreground">
          Generování testovacích dat a přehled databáze
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {success && (
        <Alert>
          <CheckCircle className="h-4 w-4" />
          <AlertDescription>{success}</AlertDescription>
        </Alert>
      )}

      {/* Seed Data Actions */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Database className="h-5 w-5" />
            Generování testovacích dat
          </CardTitle>
          <CardDescription>
            Vygenerujte 100 realistických transakcí s 10 opakujícími se společnostmi
          </CardDescription>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button
            onClick={() => handleSeed(false)}
            disabled={seeding}
          >
            {seeding ? 'Generuji...' : 'Vygenerovat data'}
          </Button>
          <Button
            variant="destructive"
            onClick={() => handleSeed(true)}
            disabled={seeding}
          >
            Smazat a vygenerovat znovu
          </Button>
        </CardContent>
      </Card>

      {/* Statistics Overview */}
      {stats && (
        <>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Celkem transakcí
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-bold">{stats.totalTransactions}</div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
                  <TrendingUp className="h-4 w-4 text-green-600" />
                  Příjmy
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-green-600">
                  {formatCurrency(stats.income.total)}
                </div>
                <p className="text-sm text-muted-foreground">
                  {stats.income.count} transakcí
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground flex items-center gap-2">
                  <TrendingDown className="h-4 w-4 text-red-600" />
                  Výdaje
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold text-red-600">
                  {formatCurrency(stats.expenses.total)}
                </div>
                <p className="text-sm text-muted-foreground">
                  {stats.expenses.count} transakcí
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">
                  Čistý zisk
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className={`text-2xl font-bold ${stats.net >= 0 ? 'text-green-600' : 'text-red-600'}`}>
                  {formatCurrency(stats.net)}
                </div>
                <p className="text-sm text-muted-foreground">
                  {stats.totalAttachments} příloh
                </p>
              </CardContent>
            </Card>
          </div>

          {/* Top Companies */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Building2 className="h-5 w-5" />
                Top 5 společností podle počtu transakcí
              </CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>IČO</TableHead>
                    <TableHead>Název společnosti</TableHead>
                    <TableHead className="text-right">Počet transakcí</TableHead>
                    <TableHead className="text-right">Celková částka</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {stats.topCompanies.map((company) => (
                    <TableRow key={company.companyId}>
                      <TableCell className="font-mono">{company.companyId}</TableCell>
                      <TableCell className="font-medium">{company.companyName}</TableCell>
                      <TableCell className="text-right">
                        <Badge variant="outline">{company.transactionCount}</Badge>
                      </TableCell>
                      <TableCell className="text-right font-medium">
                        {formatCurrency(company.totalAmount)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
};
```

### 3. Aktualizace routing v App.tsx

**src/App.tsx** - přidej route:
```tsx
import { DataManagement } from './components/DataManagement/DataManagement';

// V Routes:
<Route path="/data" element={<DataManagement />} />

// V navigation:
<Link
  to="/data"
  className="text-sm font-medium hover:text-primary transition-colors"
>
  Správa dat
</Link>
```

## Testování

### 1. Otevři stránku Správa dat

Naviguj na http://localhost:3000/data

### 2. Zobraz statistiky

- Ověř, že se zobrazují správné počty transakcí
- Zkontroluj příjmy, výdaje a čistý zisk
- Ověř top 5 společností

### 3. Vygeneruj data

1. Klikni na "Vygenerovat data"
2. Počkej na dokončení (zobrazí se success zpráva)
3. Ověř, že se statistiky aktualizovaly
4. Zkontroluj, že v seznamu transakcí jsou nová data

### 4. Clear & Reseed

1. Klikni na "Smazat a vygenerovat znovu"
2. Potvrď warning dialog
3. Ověř, že se data smazala a vygenerovala znovu
4. Zkontroluj, že počet transakcí odpovídá (100)

## Ověření

Po dokončení:

1. ✅ Statistiky se správně načítají a zobrazují
2. ✅ Tlačítko "Vygenerovat data" funguje
3. ✅ Tlačítko "Smazat a vygenerovat znovu" funguje
4. ✅ Warning dialog při clear & reseed
5. ✅ Success/Error hlášky se zobrazují
6. ✅ Statistiky se po seed operaci aktualizují
7. ✅ Top 5 společností se zobrazuje v tabulce
8. ✅ Formátování měny je správné (CZK)

## Výstup této fáze

✅ DataManagement komponenta s přehledem statistik
✅ Tlačítka pro seed data operace
✅ Vizualizace příjmů, výdajů a čistého zisku
✅ Top 5 společností tabulka
✅ Success/Error handling
✅ Automatické načítání statistik po seed operaci

## Další krok

→ **09_qdrant_embeddings.md** - Backend integrace Qdrant a embeddings
