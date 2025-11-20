# 07 - Frontend - Správa Příloh

## Cíl
Implementovat UI pro nahrávání, zobrazování a správu příloh k transakcím (upload, download, smazání).

## Prerekvizity
- Dokončený krok 06 (Attachments API)
- Běžící backend s podporou příloh

## Kroky implementace

### 1. Rozšíření typů pro přílohy

**src/types/api.ts** - doplň:
```typescript
export enum DocumentCategory {
  Invoice = 'invoice',
  Contract = 'contract',
  PurchaseOrder = 'purchase_order',
  Unknown = 'unknown'
}

export enum ProcessingStatus {
  Pending = 'pending',
  Processing = 'processing',
  Completed = 'completed',
  Failed = 'failed'
}

export interface AttachmentDto {
  id: string;
  transactionId: string;
  fileName: string;
  category: DocumentCategory;
  processingStatus: ProcessingStatus;
  createdAt: string;
  processedAt?: string;
}
```

### 2. API Service pro přílohy

**src/services/attachments-service.ts:**
```typescript
import { apiClient } from './api-client';
import { AttachmentDto } from '../types/api';

export const attachmentsService = {
  async getAll(transactionId: string): Promise<AttachmentDto[]> {
    const response = await apiClient.get<AttachmentDto[]>(
      `/api/transactions/${transactionId}/attachments`
    );
    return response.data;
  },

  async getById(transactionId: string, id: string): Promise<AttachmentDto> {
    const response = await apiClient.get<AttachmentDto>(
      `/api/transactions/${transactionId}/attachments/${id}`
    );
    return response.data;
  },

  async upload(transactionId: string, file: File): Promise<AttachmentDto> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await apiClient.post<AttachmentDto>(
      `/api/transactions/${transactionId}/attachments`,
      formData,
      {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      }
    );
    return response.data;
  },

  getDownloadUrl(transactionId: string, id: string): string {
    return `${apiClient.defaults.baseURL}/api/transactions/${transactionId}/attachments/${id}/download`;
  },

  async download(transactionId: string, id: string): Promise<void> {
    const url = this.getDownloadUrl(transactionId, id);
    window.open(url, '_blank');
  },

  async delete(transactionId: string, id: string): Promise<void> {
    await apiClient.delete(`/api/transactions/${transactionId}/attachments/${id}`);
  }
};
```

### 3. Attachment List komponenta

**src/components/AttachmentList.tsx:**
```tsx
import React from 'react';
import { AttachmentDto } from '../types/api';
import { Card, CardContent, CardHeader, CardTitle } from './ui/card';
import { Button } from './ui/button';
import { Badge } from './ui/badge';
import { Download, Trash2, FileText, Loader2 } from 'lucide-react';
import { formatDateTime } from '../lib/format';

interface AttachmentListProps {
  attachments: AttachmentDto[];
  transactionId: string;
  onDownload: (id: string) => void;
  onDelete: (id: string) => void;
}

const getCategoryLabel = (category: string): string => {
  switch (category) {
    case 'invoice':
      return 'Faktura';
    case 'contract':
      return 'Smlouva';
    case 'purchase_order':
      return 'Objednávka';
    default:
      return 'Neznámý';
  }
};

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'completed':
      return <Badge variant="default">Zpracováno</Badge>;
    case 'processing':
      return (
        <Badge variant="secondary" className="flex items-center gap-1">
          <Loader2 className="h-3 w-3 animate-spin" />
          Zpracovává se
        </Badge>
      );
    case 'failed':
      return <Badge variant="destructive">Chyba</Badge>;
    default:
      return <Badge variant="outline">Čeká</Badge>;
  }
};

export const AttachmentList: React.FC<AttachmentListProps> = ({
  attachments,
  transactionId,
  onDownload,
  onDelete,
}) => {
  if (attachments.length === 0) {
    return (
      <div className="text-center py-8 text-muted-foreground">
        <FileText className="h-12 w-12 mx-auto mb-2 opacity-50" />
        <p>Žádné přílohy</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {attachments.map((attachment) => (
        <Card key={attachment.id}>
          <CardContent className="p-4">
            <div className="flex items-start justify-between">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <FileText className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                  <span className="font-medium truncate">{attachment.fileName}</span>
                </div>
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <span>{getCategoryLabel(attachment.category)}</span>
                  <span>•</span>
                  <span>{formatDateTime(attachment.createdAt)}</span>
                </div>
                <div className="mt-2">
                  {getStatusBadge(attachment.processingStatus)}
                </div>
              </div>
              <div className="flex gap-2 ml-4">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => onDownload(attachment.id)}
                >
                  <Download className="h-4 w-4" />
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => onDelete(attachment.id)}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
};
```

### 4. Attachment Upload Dialog

**src/components/AttachmentUploadDialog.tsx:**
```tsx
import React, { useState, useRef } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from './ui/dialog';
import { Button } from './ui/button';
import { Alert, AlertDescription } from './ui/alert';
import { Upload, File, X, AlertCircle } from 'lucide-react';

interface AttachmentUploadDialogProps {
  open: boolean;
  onClose: () => void;
  onUpload: (file: File) => Promise<void>;
  transactionId: string;
}

export const AttachmentUploadDialog: React.FC<AttachmentUploadDialogProps> = ({
  open,
  onClose,
  onUpload,
  transactionId,
}) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Validate file size (max 10 MB)
    if (file.size > 10 * 1024 * 1024) {
      setError('Soubor je příliš velký. Maximum je 10 MB.');
      return;
    }

    setSelectedFile(file);
    setError(null);
  };

  const handleUpload = async () => {
    if (!selectedFile) return;

    try {
      setUploading(true);
      setError(null);
      await onUpload(selectedFile);
      setSelectedFile(null);
      onClose();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Nepodařilo se nahrát přílohu');
    } finally {
      setUploading(false);
    }
  };

  const handleClose = () => {
    if (!uploading) {
      setSelectedFile(null);
      setError(null);
      onClose();
    }
  };

  const handleRemoveFile = () => {
    setSelectedFile(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Nahrát přílohu</DialogTitle>
          <DialogDescription>
            Vyberte soubor k nahrání k transakci. Maximální velikost je 10 MB.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {!selectedFile ? (
            <div className="border-2 border-dashed border-muted rounded-lg p-8">
              <div className="text-center">
                <Upload className="h-12 w-12 mx-auto mb-4 text-muted-foreground" />
                <p className="text-sm text-muted-foreground mb-4">
                  Klikněte pro výběr souboru
                </p>
                <input
                  ref={fileInputRef}
                  type="file"
                  onChange={handleFileSelect}
                  className="hidden"
                  id="file-upload"
                />
                <label htmlFor="file-upload">
                  <Button variant="outline" asChild>
                    <span>Vybrat soubor</span>
                  </Button>
                </label>
              </div>
            </div>
          ) : (
            <div className="border rounded-lg p-4">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <File className="h-8 w-8 text-muted-foreground" />
                  <div>
                    <p className="font-medium">{selectedFile.name}</p>
                    <p className="text-sm text-muted-foreground">
                      {(selectedFile.size / 1024 / 1024).toFixed(2)} MB
                    </p>
                  </div>
                </div>
                {!uploading && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleRemoveFile}
                  >
                    <X className="h-4 w-4" />
                  </Button>
                )}
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={uploading}>
            Zrušit
          </Button>
          <Button onClick={handleUpload} disabled={!selectedFile || uploading}>
            {uploading ? 'Nahrávám...' : 'Nahrát'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};
```

### 5. Aktualizace TransactionDetail komponenty

**src/components/TransactionDetail/TransactionDetail.tsx:**
```tsx
import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { TransactionDto } from '../../types/api';
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
```

### 6. Aktualizace TransactionDto pro přílohy

Ověř, že **src/types/api.ts** obsahuje rozšířený TransactionDto:

```typescript
export interface TransactionDto {
  id: string;
  description: string;
  amount: number;
  companyId: string;
  companyName?: string;
  transactionType: TransactionType;
  transactionDate: string;
  createdAt: string;
  updatedAt: string;
  attachmentCount: number;
  attachments?: AttachmentDto[];  // Pro detail transakce
}
```

### 7. Přidání routing pro detail transakce

**src/App.tsx** - aktualizuj routes:
```tsx
import React from 'react';
import { BrowserRouter as Router, Routes, Route, Link } from 'react-router-dom';
import { TransactionsPage } from './pages/TransactionsPage';
import { TransactionDetail } from './components/TransactionDetail/TransactionDetail';

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
            <Route path="/transactions/:id" element={<TransactionDetail />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;
```

### 8. Aktualizace TransactionCard pro link na detail

**src/components/TransactionCard.tsx** - přidej link:
```tsx
import { Link } from 'react-router-dom';

// V CardFooter přidej:
<CardFooter className="flex gap-2">
  <Button variant="outline" size="sm" asChild>
    <Link to={`/transactions/${transaction.id}`}>Detail</Link>
  </Button>
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

1. **Zobrazení detailu transakce:**
   - Na hlavní stránce klikni na "Detail" u nějaké transakce
   - Ověř, že se zobrazí kompletní informace
   - Zkontroluj, že attachmentCount odpovídá

2. **Nahrání přílohy:**
   - V detailu transakce klikni "Nahrát přílohu"
   - Vyber testovací soubor (např. .txt nebo .md)
   - Klikni "Nahrát"
   - Ověř, že se příloha objevila v seznamu s statusem "Čeká" nebo "Zpracováno"

3. **Stažení přílohy:**
   - U nahrané přílohy klikni na ikonu downloadu
   - Ověř, že se soubor stáhne

4. **Smazání přílohy:**
   - Klikni na ikonu koše u přílohy
   - Potvrď smazání
   - Ověř, že příloha zmizela ze seznamu
   - Zkontroluj, že attachmentCount se snížil

5. **Validace velikosti souboru:**
   - Zkus nahrát soubor větší než 10 MB
   - Ověř, že se zobrazí chybová hláška

6. **Processing status:**
   - Po nahrání přílohy sleduj změnu statusu z "Čeká" na "Zpracováno" (pokud je background processing aktivní)

## Ověření

Po dokončení:

1. ✅ Detail transakce zobrazuje kompletní informace
2. ✅ Seznam příloh se načítá a zobrazuje
3. ✅ Lze nahrát novou přílohu (drag & drop nebo file picker)
4. ✅ Lze stáhnout přílohu
5. ✅ Lze smazat přílohu
6. ✅ Validace velikosti souboru funguje (max 10 MB)
7. ✅ Processing status se zobrazuje správně
8. ✅ AttachmentCount se aktualizuje po upload/delete
9. ✅ Kategorie dokumentu se zobrazuje (po zpracování)
10. ✅ Navigace zpět na seznam funguje

## Výstup této fáze

✅ Kompletní UI pro správu příloh k transakcím
✅ Upload s drag & drop podporou
✅ Download funkcionalita
✅ Status indikátory (pending, processing, completed, failed)
✅ Kategorizace dokumentů (faktura, smlouva, objednávka)
✅ Responzivní layout
✅ Error handling a validace

## Další krok

→ **08_seed_data.md** - Vytvoření seed dat pro vývoj a testování
