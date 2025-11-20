export enum TransactionType {
  Income = 'Income',
  Expense = 'Expense'
}

export enum DocumentCategory {
  Unknown = 'Unknown',
  Invoice = 'Invoice',
  Contract = 'Contract',
  PurchaseOrder = 'PurchaseOrder'
}

export enum ProcessingStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Completed = 'Completed',
  Failed = 'Failed'
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
  attachments?: AttachmentDto[];
}

export interface CreateTransactionDto {
  description: string;
  amount: number;
  companyId: string;
  companyName?: string;
  transactionType: TransactionType;
  transactionDate: string;
}

export interface UpdateTransactionDto {
  description: string;
  amount: number;
  companyId: string;
  companyName?: string;
  transactionType: TransactionType;
  transactionDate: string;
}
