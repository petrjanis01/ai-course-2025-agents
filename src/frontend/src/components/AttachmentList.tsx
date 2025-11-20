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
    case 'Invoice':
      return 'Faktura';
    case 'Contract':
      return 'Smlouva';
    case 'PurchaseOrder':
      return 'Objednávka';
    default:
      return 'Neznámý';
  }
};

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'Completed':
      return <Badge variant="default">Zpracováno</Badge>;
    case 'Processing':
      return (
        <Badge variant="secondary" className="flex items-center gap-1">
          <Loader2 className="h-3 w-3 animate-spin" />
          Zpracovává se
        </Badge>
      );
    case 'Failed':
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
