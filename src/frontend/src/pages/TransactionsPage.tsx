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
