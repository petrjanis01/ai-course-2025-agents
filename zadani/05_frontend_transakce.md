# 05 - Frontend - Správa Transakcí

## Cíl
Implementovat kompletní UI pro CRUD operace s transakcemi - seznam, vytvoření, editace a smazání.

## Prerekvizity
- Dokončený krok 04 (Frontend setup)
- Běžící backend (krok 03)

## Kroky implementace

### 1. Instalace potřebných shadcn/ui komponent

```bash
npx shadcn-ui@latest add button
npx shadcn-ui@latest add card
npx shadcn-ui@latest add input
npx shadcn-ui@latest add label
npx shadcn-ui@latest add dialog
npx shadcn-ui@latest add select
npx shadcn-ui@latest add table
npx shadcn-ui@latest add badge
npx shadcn-ui@latest add alert
npx shadcn-ui@latest add calendar
```

### 2. Utility pro formátování

**src/lib/format.ts:**
```typescript
import { format } from 'date-fns';
import { cs } from 'date-fns/locale';

export const formatCurrency = (amount: number): string => {
  return new Intl.NumberFormat('cs-CZ', {
    style: 'currency',
    currency: 'CZK',
  }).format(amount);
};

export const formatDate = (dateString: string): string => {
  return format(new Date(dateString), 'dd.MM.yyyy', { locale: cs });
};

export const formatDateTime = (dateString: string): string => {
  return format(new Date(dateString), 'dd.MM.yyyy HH:mm', { locale: cs });
};
```

### 3. Transaction Card komponenta

**src/components/TransactionCard.tsx:**
```tsx
import React from 'react';
import { TransactionDto, TransactionType } from '../types/api';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from './ui/card';
import { Badge } from './ui/badge';
import { Button } from './ui/button';
import { Pencil, Trash2, Paperclip } from 'lucide-react';
import { formatCurrency, formatDate } from '../lib/format';

interface TransactionCardProps {
  transaction: TransactionDto;
  onEdit: (transaction: TransactionDto) => void;
  onDelete: (id: string) => void;
}

export const TransactionCard: React.FC<TransactionCardProps> = ({
  transaction,
  onEdit,
  onDelete,
}) => {
  const isIncome = transaction.transactionType === TransactionType.Income;

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <CardTitle className="text-lg">{transaction.description}</CardTitle>
            <CardDescription>
              {transaction.companyName} (IČO: {transaction.companyId})
            </CardDescription>
          </div>
          <Badge variant={isIncome ? 'default' : 'destructive'}>
            {isIncome ? 'Příjem' : 'Výdaj'}
          </Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          <div className="flex items-baseline gap-2">
            <span className={`text-2xl font-bold ${isIncome ? 'text-green-600' : 'text-red-600'}`}>
              {isIncome ? '+' : '-'}{formatCurrency(transaction.amount)}
            </span>
          </div>
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <span>Datum: {formatDate(transaction.transactionDate)}</span>
            {transaction.attachmentCount > 0 && (
              <span className="flex items-center gap-1">
                <Paperclip className="h-4 w-4" />
                {transaction.attachmentCount} příloh
              </span>
            )}
          </div>
        </div>
      </CardContent>
      <CardFooter className="flex gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onEdit(transaction)}
        >
          <Pencil className="h-4 w-4 mr-2" />
          Upravit
        </Button>
        <Button
          variant="destructive"
          size="sm"
          onClick={() => onDelete(transaction.id)}
        >
          <Trash2 className="h-4 w-4 mr-2" />
          Smazat
        </Button>
      </CardFooter>
    </Card>
  );
};
```

### 4. Transaction Form Dialog

**src/components/TransactionFormDialog.tsx:**
```tsx
import React, { useState, useEffect } from 'react';
import { TransactionDto, TransactionType, CreateTransactionDto, UpdateTransactionDto } from '../types/api';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from './ui/dialog';
import { Button } from './ui/button';
import { Input } from './ui/input';
import { Label } from './ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './ui/select';

interface TransactionFormDialogProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (data: CreateTransactionDto | UpdateTransactionDto) => Promise<void>;
  transaction?: TransactionDto;
}

export const TransactionFormDialog: React.FC<TransactionFormDialogProps> = ({
  open,
  onClose,
  onSubmit,
  transaction,
}) => {
  const [formData, setFormData] = useState({
    description: '',
    amount: '',
    companyId: '',
    companyName: '',
    transactionType: TransactionType.Expense,
    transactionDate: new Date().toISOString().split('T')[0],
  });
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (transaction) {
      setFormData({
        description: transaction.description,
        amount: transaction.amount.toString(),
        companyId: transaction.companyId,
        companyName: transaction.companyName || '',
        transactionType: transaction.transactionType,
        transactionDate: transaction.transactionDate.split('T')[0],
      });
    } else {
      setFormData({
        description: '',
        amount: '',
        companyId: '',
        companyName: '',
        transactionType: TransactionType.Expense,
        transactionDate: new Date().toISOString().split('T')[0],
      });
    }
  }, [transaction, open]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    try {
      const dto = {
        description: formData.description,
        amount: parseFloat(formData.amount),
        companyId: formData.companyId,
        companyName: formData.companyName || undefined,
        transactionType: formData.transactionType,
        transactionDate: new Date(formData.transactionDate).toISOString(),
      };

      await onSubmit(dto);
      onClose();
    } catch (error) {
      console.error('Form submit error:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>
            {transaction ? 'Upravit transakci' : 'Nová transakce'}
          </DialogTitle>
          <DialogDescription>
            {transaction
              ? 'Změňte údaje transakce a uložte.'
              : 'Vyplňte údaje nové transakce.'}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit}>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="description">Popis</Label>
              <Input
                id="description"
                value={formData.description}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                required
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="amount">Částka (Kč)</Label>
                <Input
                  id="amount"
                  type="number"
                  step="0.01"
                  value={formData.amount}
                  onChange={(e) =>
                    setFormData({ ...formData, amount: e.target.value })
                  }
                  required
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="transactionType">Typ</Label>
                <Select
                  value={formData.transactionType.toString()}
                  onValueChange={(value) =>
                    setFormData({
                      ...formData,
                      transactionType: parseInt(value) as TransactionType,
                    })
                  }
                >
                  <SelectTrigger id="transactionType">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="0">Příjem</SelectItem>
                    <SelectItem value="1">Výdaj</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="companyId">IČO</Label>
              <Input
                id="companyId"
                value={formData.companyId}
                onChange={(e) =>
                  setFormData({ ...formData, companyId: e.target.value })
                }
                required
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="companyName">Název firmy (volitelné)</Label>
              <Input
                id="companyName"
                value={formData.companyName}
                onChange={(e) =>
                  setFormData({ ...formData, companyName: e.target.value })
                }
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="transactionDate">Datum transakce</Label>
              <Input
                id="transactionDate"
                type="date"
                value={formData.transactionDate}
                onChange={(e) =>
                  setFormData({ ...formData, transactionDate: e.target.value })
                }
                required
              />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Zrušit
            </Button>
            <Button type="submit" disabled={loading}>
              {loading ? 'Ukládám...' : 'Uložit'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
};
```

### 5. Hlavní Transactions Page

**src/pages/TransactionsPage.tsx:**
```tsx
import React, { useEffect, useState } from 'react';
import { TransactionDto, CreateTransactionDto, UpdateTransactionDto } from '../types/api';
import { transactionsService } from '../services/transactions-service';
import { TransactionCard } from '../components/TransactionCard';
import { TransactionFormDialog } from '../components/TransactionFormDialog';
import { Button } from '../components/ui/button';
import { Alert, AlertDescription } from '../components/ui/alert';
import { Plus, AlertCircle } from 'lucide-react';

export const TransactionsPage: React.FC = () => {
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingTransaction, setEditingTransaction] = useState<TransactionDto | undefined>();

  const loadTransactions = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await transactionsService.getAll();
      setTransactions(data);
    } catch (err: any) {
      setError(err.message || 'Nepodařilo se načíst transakce');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTransactions();
  }, []);

  const handleCreate = async (dto: CreateTransactionDto) => {
    await transactionsService.create(dto);
    await loadTransactions();
  };

  const handleUpdate = async (dto: UpdateTransactionDto) => {
    if (!editingTransaction) return;
    await transactionsService.update(editingTransaction.id, dto);
    await loadTransactions();
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete smazat tuto transakci?')) return;

    try {
      await transactionsService.delete(id);
      await loadTransactions();
    } catch (err: any) {
      setError(err.message || 'Nepodařilo se smazat transakci');
    }
  };

  const openCreateDialog = () => {
    setEditingTransaction(undefined);
    setDialogOpen(true);
  };

  const openEditDialog = (transaction: TransactionDto) => {
    setEditingTransaction(transaction);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingTransaction(undefined);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <div className="text-lg font-medium">Načítám transakce...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Transakce</h1>
          <p className="text-muted-foreground">
            Správa příjmů a výdajů
          </p>
        </div>
        <Button onClick={openCreateDialog}>
          <Plus className="h-4 w-4 mr-2" />
          Nová transakce
        </Button>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {transactions.length === 0 ? (
        <div className="text-center py-12">
          <p className="text-muted-foreground mb-4">
            Zatím nemáte žádné transakce
          </p>
          <Button onClick={openCreateDialog}>
            <Plus className="h-4 w-4 mr-2" />
            Vytvořit první transakci
          </Button>
        </div>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {transactions.map((transaction) => (
            <TransactionCard
              key={transaction.id}
              transaction={transaction}
              onEdit={openEditDialog}
              onDelete={handleDelete}
            />
          ))}
        </div>
      )}

      <TransactionFormDialog
        open={dialogOpen}
        onClose={closeDialog}
        onSubmit={editingTransaction ? handleUpdate : handleCreate}
        transaction={editingTransaction}
      />
    </div>
  );
};
```

### 6. Aktualizace App.tsx s routing

**src/App.tsx:**
```tsx
import React from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { TransactionsPage } from './pages/TransactionsPage';

function App() {
  return (
    <Router>
      <div className="min-h-screen bg-background">
        <header className="border-b">
          <div className="container mx-auto px-4 py-4">
            <div className="flex items-center justify-between">
              <Link to="/" className="text-2xl font-bold">
                Transaction Management
              </Link>
              <nav className="flex gap-4">
                <Link
                  to="/"
                  className="text-sm font-medium hover:text-primary transition-colors"
                >
                  Transakce
                </Link>
              </nav>
            </div>
          </div>
        </header>
        <main className="container mx-auto px-4 py-8">
          <Routes>
            <Route path="/" element={<TransactionsPage />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;
```

### 7. Instalace date-fns locale

```bash
npm install date-fns
```

## Testování

### 1. Spuštění aplikace

```bash
# Backend (pokud neběží)
cd TransactionManagement.Api
dotnet run

# Frontend
cd transaction-management-ui
npm start
```

### 2. Test scénáře

1. **Vytvoření transakce:**
   - Klikni na "Nová transakce"
   - Vyplň formulář:
     - Popis: "Nákup kancelářských potřeb"
     - Částka: 5000
     - Typ: Výdaj
     - IČO: 12345678
     - Název firmy: "Office Supplies s.r.o."
     - Datum: Dnešní datum
   - Klikni "Uložit"
   - Ověř, že se transakce zobrazila v seznamu

2. **Editace transakce:**
   - Klikni "Upravit" na kartě transakce
   - Změň částku na 5500
   - Klikni "Uložit"
   - Ověř, že se částka aktualizovala

3. **Smazání transakce:**
   - Klikni "Smazat" na kartě transakce
   - Potvrď smazání
   - Ověř, že transakce zmizela ze seznamu

4. **Vytvoř více transakcí různých typů:**
   - Vytvoř příjem: "Faktura #2025-001", 50000 Kč
   - Vytvoř výdaj: "Pronájem kanceláře", 15000 Kč
   - Ověř, že příjmy jsou zelené, výdaje červené

## Ověření

Po dokončení:

1. ✅ Seznam transakcí se načítá z API
2. ✅ Lze vytvořit novou transakci
3. ✅ Lze upravit existující transakci
4. ✅ Lze smazat transakci
5. ✅ Příjmy a výdaje mají odlišné barvy
6. ✅ Částky jsou správně formátované (CZK)
7. ✅ Datum je ve formátu dd.MM.yyyy
8. ✅ Formulář validuje povinná pole
9. ✅ Error stavy jsou správně zobrazeny

## Výstup této fáze

✅ Kompletní UI pro správu transakcí
✅ CRUD operace plně funkční
✅ Responzivní layout (mobile-friendly)
✅ shadcn/ui komponenty (Card, Dialog, Button, Input, Select)
✅ Správné formátování dat a měny
✅ Error handling a loading states

## Další krok

→ **06_attachments_api.md** - Backend API pro nahrávání a správu příloh k transakcím
