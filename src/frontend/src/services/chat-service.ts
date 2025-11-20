import { apiClient } from './api-client';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface ChatRequest {
  message: string;
  sessionId?: string;
  conversationHistory?: ChatMessage[];
}

export interface ChatMetadata {
  tokensUsed: number;
  responseTime: number;
  agentsUsed: string[];
}

export interface ChatResponse {
  message: string;
  sessionId: string;
  metadata: ChatMetadata;
}

export const chatService = {
  async sendMessage(request: ChatRequest): Promise<ChatResponse> {
    const response = await apiClient.post<ChatResponse>('/api/chat/message', request);
    return response.data;
  }
};
