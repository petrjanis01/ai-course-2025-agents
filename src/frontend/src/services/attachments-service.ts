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
