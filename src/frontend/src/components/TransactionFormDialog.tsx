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
                  value={formData.transactionType}
                  onValueChange={(value) =>
                    setFormData({
                      ...formData,
                      transactionType: value as TransactionType,
                    })
                  }
                >
                  <SelectTrigger id="transactionType">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={TransactionType.Income}>Příjem</SelectItem>
                    <SelectItem value={TransactionType.Expense}>Výdaj</SelectItem>
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
