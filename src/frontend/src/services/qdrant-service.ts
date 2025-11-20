import { apiClient } from './api-client';

export interface EmbeddingTestResult {
  text: string;
  dimensions: number;
  sample: string;
}

export interface SearchResult {
  attachmentId: string;
  fileName: string;
  category: string;
  score: number;
  contentPreview: string;
  hasAmounts: boolean;
  hasDates: boolean;
}

export interface SearchTestResult {
  query: string;
  resultsCount: number;
  results: SearchResult[];
}

export const qdrantService = {
  async testEmbedding(text: string): Promise<EmbeddingTestResult> {
    const response = await apiClient.post<EmbeddingTestResult>(
      '/api/test/embedding',
      JSON.stringify(text),
      {
        headers: {
          'Content-Type': 'application/json'
        }
      }
    );
    return response.data;
  },

  async testSearch(query: string): Promise<SearchTestResult> {
    const response = await apiClient.post<SearchTestResult>(
      '/api/test/search',
      JSON.stringify(query),
      {
        headers: {
          'Content-Type': 'application/json'
        }
      }
    );
    return response.data;
  }
};
