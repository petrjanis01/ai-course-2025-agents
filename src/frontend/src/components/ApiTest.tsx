import React, { useEffect, useState } from 'react';
import { transactionsService } from '../services/transactions-service';
import { TransactionDto } from '../types/api';

export const ApiTest: React.FC = () => {
  const [transactions, setTransactions] = useState<TransactionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    transactionsService.getAll()
      .then(setTransactions)
      .catch(err => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div>Načítám...</div>;
  if (error) return <div className="text-destructive">Error: {error}</div>;

  return (
    <div>
      <h2 className="text-xl font-semibold mb-4">API Test - Transakce</h2>
      <div className="space-y-2">
        {transactions.length === 0 ? (
          <p className="text-muted-foreground">Žádné transakce</p>
        ) : (
          transactions.map(t => (
            <div key={t.id} className="p-4 border rounded">
              <p className="font-medium">{t.description}</p>
              <p className="text-sm text-muted-foreground">
                {t.amount} Kč - {t.companyName}
              </p>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
