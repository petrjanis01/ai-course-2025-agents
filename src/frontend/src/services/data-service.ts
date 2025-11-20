import { apiClient } from './api-client';

export interface DataStats {
  totalTransactions: number;
  totalAttachments: number;
  expenses: {
    count: number;
    total: number;
  };
  income: {
    count: number;
    total: number;
  };
  net: number;
  topCompanies: Array<{
    companyId: string;
    companyName: string;
    transactionCount: number;
    totalAmount: number;
  }>;
}

export interface SeedResponse {
  message: string;
  transactionsCreated: number;
}

export const dataService = {
  async seedData(clearExisting: boolean = false): Promise<SeedResponse> {
    const response = await apiClient.post<SeedResponse>(
      `/api/data/seed?clearExisting=${clearExisting}`
    );
    return response.data;
  },

  async getStats(): Promise<DataStats> {
    const response = await apiClient.get<DataStats>('/api/data/stats');
    return response.data;
  }
};
