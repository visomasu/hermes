export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'error';
  content: string;
  timestamp: Date;
}

export interface ChatInputMessage {
  text: string;
  userId?: string;
  sessionId?: string;
}

export interface ChatResponse {
  type: 'response' | 'progress' | 'error';
  message: string;
  sessionId?: string;
}
