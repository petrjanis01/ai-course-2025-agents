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
