import React from 'react';
import { useNavigate } from 'react-router-dom';
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
  const navigate = useNavigate();
  const isIncome = transaction.transactionType === TransactionType.Income;

  const handleCardClick = () => {
    navigate(`/transactions/${transaction.id}`);
  };

  const handleEditClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onEdit(transaction);
  };

  const handleDeleteClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onDelete(transaction.id);
  };

  return (
    <Card className="cursor-pointer hover:shadow-lg transition-shadow" onClick={handleCardClick}>
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
          onClick={handleEditClick}
        >
          <Pencil className="h-4 w-4 mr-2" />
          Upravit
        </Button>
        <Button
          variant="destructive"
          size="sm"
          onClick={handleDeleteClick}
        >
          <Trash2 className="h-4 w-4 mr-2" />
          Smazat
        </Button>
      </CardFooter>
    </Card>
  );
};
