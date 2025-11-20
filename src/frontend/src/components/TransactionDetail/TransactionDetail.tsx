import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ProcessingStatus, TransactionDto } from '../../types/api';
import { transactionsService } from '../../services/transactions-service';
import { attachmentsService } from '../../services/attachments-service';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../ui/card';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import { Alert, AlertDescription } from '../ui/alert';
import { AttachmentList } from '../AttachmentList';
import { AttachmentUploadDialog } from '../AttachmentUploadDialog';
import { ArrowLeft, Pencil, Trash2, Upload, AlertCircle } from 'lucide-react';
import { formatCurrency, formatDate, formatDateTime } from '../../lib/format';
import { TransactionType } from '../../types/api';

export const TransactionDetail: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [transaction, setTransaction] = useState<TransactionDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);

  const loadTransaction = async () => {
    if (!id) return;

    try {
      setLoading(true);
      setError(null);
      const data = await transactionsService.getById(id);
      setTransaction(data);
    } catch (err: any) {
      setError(err.message || 'Nepodařilo se načíst transakci');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTransaction();
  }, [id]);

  // Auto-refresh when there are pending/processing attachments
  useEffect(() => {
    if (!transaction?.attachments) return;

    const hasProcessingAttachments = transaction.attachments.some(
      (a) => a.processingStatus === ProcessingStatus.Pending || a.processingStatus === ProcessingStatus.Processing
    );

    if (!hasProcessingAttachments) return;

    const interval = setInterval(() => {
      loadTransaction();
    }, 3000); // Refresh every 3 seconds

    return () => clearInterval(interval);
  }, [transaction?.attachments]);

  const handleUpload = async (file: File) => {
    if (!id) return;
    await attachmentsService.upload(id, file);
    await loadTransaction(); // Refresh to show new attachment
  };

  const handleDownload = (attachmentId: string) => {
    if (!id) return;
    attachmentsService.download(id, attachmentId);
  };

  const handleDeleteAttachment = async (attachmentId: string) => {
    if (!id || !window.confirm('Opravdu chcete smazat tuto přílohu?')) return;

    try {
      await attachmentsService.delete(id, attachmentId);
      await loadTransaction();
    } catch (err: any) {
      setError(err.message || 'Nepodařilo se smazat přílohu');
    }
  };

  const handleDelete = async () => {
    if (!id || !window.confirm('Opravdu chcete smazat tuto transakci?')) return;

    try {
      await transactionsService.delete(id);
      navigate('/');
    } catch (err: any) {
      setError(err.message || 'Nepodařilo se smazat transakci');
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <div className="text-lg font-medium">Načítám transakci...</div>
        </div>
      </div>
    );
  }

  if (!transaction) {
    return (
      <div className="space-y-4">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>Transakce nenalezena</AlertDescription>
        </Alert>
        <Button onClick={() => navigate('/')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Zpět na seznam
        </Button>
      </div>
    );
  }

  const isIncome = transaction.transactionType === TransactionType.Income;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <Button variant="ghost" onClick={() => navigate('/')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Zpět na seznam
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => navigate(`/transactions/${id}/edit`)}>
            <Pencil className="h-4 w-4 mr-2" />
            Upravit
          </Button>
          <Button variant="destructive" onClick={handleDelete}>
            <Trash2 className="h-4 w-4 mr-2" />
            Smazat
          </Button>
        </div>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <div className="flex items-start justify-between">
            <div className="flex-1">
              <CardTitle className="text-2xl">{transaction.description}</CardTitle>
              <CardDescription>
                {transaction.companyName} (IČO: {transaction.companyId})
              </CardDescription>
            </div>
            <Badge variant={isIncome ? 'default' : 'destructive'}>
              {isIncome ? 'Příjem' : 'Výdaj'}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <div className={`text-3xl font-bold ${isIncome ? 'text-green-600' : 'text-red-600'}`}>
              {isIncome ? '+' : '-'}{formatCurrency(transaction.amount)}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4 pt-4 border-t">
            <div>
              <p className="text-sm text-muted-foreground">Datum transakce</p>
              <p className="font-medium">{formatDate(transaction.transactionDate)}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Vytvořeno</p>
              <p className="font-medium">{formatDateTime(transaction.createdAt)}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">IČO</p>
              <p className="font-medium">{transaction.companyId}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Společnost</p>
              <p className="font-medium">{transaction.companyName || '-'}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Přílohy ({transaction.attachmentCount})</CardTitle>
              <CardDescription>Dokumenty připojené k této transakci</CardDescription>
            </div>
            <Button onClick={() => setUploadDialogOpen(true)}>
              <Upload className="h-4 w-4 mr-2" />
              Nahrát přílohu
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <AttachmentList
            attachments={transaction.attachments || []}
            transactionId={transaction.id}
            onDownload={handleDownload}
            onDelete={handleDeleteAttachment}
          />
        </CardContent>
      </Card>

      <AttachmentUploadDialog
        open={uploadDialogOpen}
        onClose={() => setUploadDialogOpen(false)}
        onUpload={handleUpload}
        transactionId={transaction.id}
      />
    </div>
  );
};
