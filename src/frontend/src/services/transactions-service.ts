import { apiClient } from './api-client';
import { TransactionDto, CreateTransactionDto, UpdateTransactionDto } from '../types/api';

export const transactionsService = {
  async getAll(): Promise<TransactionDto[]> {
    const response = await apiClient.get<TransactionDto[]>('/api/transactions');
    return response.data;
  },

  async getById(id: string): Promise<TransactionDto> {
    const response = await apiClient.get<TransactionDto>(`/api/transactions/${id}`);
    return response.data;
  },

  async create(dto: CreateTransactionDto): Promise<TransactionDto> {
    const response = await apiClient.post<TransactionDto>('/api/transactions', dto);
    return response.data;
  },

  async update(id: string, dto: UpdateTransactionDto): Promise<TransactionDto> {
    const response = await apiClient.put<TransactionDto>(`/api/transactions/${id}`, dto);
    return response.data;
  },

  async delete(id: string): Promise<void> {
    await apiClient.delete(`/api/transactions/${id}`);
  }
};
