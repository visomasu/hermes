import { useState, useRef, useEffect, useCallback } from 'react';
import { useWebSocket } from '../../hooks/useWebSocket';
import type { ChatMessage, ChatResponse } from '../../types/chat';
import clsx from 'clsx';
import MarkdownRenderer from '../shared/MarkdownRenderer';

interface ChatPaneProps {
  onClose: () => void;
  onFocusMessage?: (content: string) => void;
}

const TEST_USER_ID = 'testuser@microsoft.com'; // Hardcoded for MVP

export default function ChatPane({ onClose, onFocusMessage }: ChatPaneProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Wrap onMessage in useCallback to prevent WebSocket reconnections
  const handleMessage = useCallback((data: ChatResponse) => {
    if (data.type === 'response') {
      setIsTyping(false);
      setMessages((prev) => [
        ...prev,
        {
          id: Date.now().toString(),
          role: 'assistant',
          content: data.message,
          timestamp: new Date(),
        },
      ]);
    } else if (data.type === 'progress') {
      // Show progress as a temporary typing indicator
      setIsTyping(true);
    } else if (data.type === 'error') {
      setIsTyping(false);
      setMessages((prev) => [
        ...prev,
        {
          id: Date.now().toString(),
          role: 'error',
          content: data.message,
          timestamp: new Date(),
        },
      ]);
    }
  }, []);

  const { sendMessage, connectionStatus } = useWebSocket({
    url: 'ws://localhost:3978/api/hermes/ws',
    onMessage: handleMessage,
  });

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isTyping]);

  // Auto-resize textarea
  const autoResize = useCallback(() => {
    const textarea = textareaRef.current;
    if (textarea) {
      textarea.style.height = '48px';
      const scrollHeight = textarea.scrollHeight;
      textarea.style.height = Math.min(scrollHeight, 128) + 'px';
    }
  }, []);

  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setInput(e.target.value);
    autoResize();
  };

  const handleSend = () => {
    if (!input.trim()) return;

    const userMessage: ChatMessage = {
      id: Date.now().toString(),
      role: 'user',
      content: input,
      timestamp: new Date(),
    };

    setMessages((prev) => [...prev, userMessage]);
    setIsTyping(true);

    sendMessage({
      text: input,
      userId: TEST_USER_ID,
      sessionId: `web-${Date.now()}`,
    });

    setInput('');
    // Reset textarea height
    if (textareaRef.current) {
      textareaRef.current.style.height = '48px';
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <aside className="w-96 bg-gradient-to-b from-white to-gray-50 border-l border-gray-200 flex flex-col shadow-xl">
      {/* Header */}
      <div className="px-6 py-4 bg-gradient-to-r from-blue-500 to-blue-600 text-white flex items-center justify-between shadow-md">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-white/20 rounded-lg flex items-center justify-center backdrop-blur-sm">
            <span className="text-lg">💬</span>
          </div>
          <div>
            <h2 className="text-base font-bold">Chat with Hermes</h2>
            <div className="flex items-center gap-1.5 mt-0.5">
              <div
                className={clsx(
                  'w-2 h-2 rounded-full',
                  connectionStatus === 'connected' && 'bg-green-400 animate-pulse',
                  connectionStatus === 'connecting' && 'bg-yellow-400',
                  connectionStatus === 'disconnected' && 'bg-gray-300',
                  connectionStatus === 'error' && 'bg-red-400'
                )}
              />
              <span className="text-xs opacity-90 capitalize">{connectionStatus}</span>
            </div>
          </div>
        </div>
        <button
          onClick={onClose}
          className="text-white/80 hover:text-white hover:bg-white/10 rounded-lg p-2 transition-all"
        >
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-6 space-y-4">
        {messages.length === 0 && (
          <div className="text-center text-gray-500 mt-16">
            <div className="w-16 h-16 bg-gradient-to-br from-blue-100 to-blue-200 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <span className="text-3xl">💬</span>
            </div>
            <p className="font-semibold text-gray-700">Start a conversation</p>
            <p className="text-sm mt-2 text-gray-500">Ask about configurations, SLA violations, or anything else!</p>
          </div>
        )}

        {messages.map((message) => (
          <div
            key={message.id}
            className={clsx(
              'flex',
              message.role === 'user' ? 'justify-end' : 'justify-start'
            )}
          >
            <div
              className={clsx(
                'max-w-[85%] rounded-2xl px-4 py-3 shadow-md relative group',
                message.role === 'user' && 'bg-gradient-to-br from-blue-500 to-blue-600 text-white',
                message.role === 'assistant' && 'bg-white text-gray-900 border border-gray-200',
                message.role === 'error' && 'bg-red-50 text-red-900 border border-red-200'
              )}
            >
              {/* Render markdown for assistant messages */}
              {message.role === 'assistant' ? (
                <MarkdownRenderer
                  content={message.content}
                  mode="compact"
                />
              ) : (
                <p className="text-sm whitespace-pre-wrap leading-relaxed">{message.content}</p>
              )}

              {/* Focus button for assistant messages */}
              {message.role === 'assistant' && onFocusMessage && (
                <button
                  onClick={() => onFocusMessage(message.content)}
                  className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity bg-gray-100 hover:bg-gray-200 p-1.5 rounded-lg shadow"
                  title="View in focus mode"
                >
                  📖
                </button>
              )}

              {/* Timestamp */}
              <p className={clsx(
                'text-xs mt-2',
                message.role === 'user' ? 'text-white/70' : 'text-gray-500'
              )}>
                {message.timestamp.toLocaleTimeString()}
              </p>
            </div>
          </div>
        ))}

        {isTyping && (
          <div className="flex justify-start">
            <div className="bg-white rounded-2xl px-5 py-3 shadow-md border border-gray-200">
              <div className="flex gap-1.5">
                <div className="w-2.5 h-2.5 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                <div className="w-2.5 h-2.5 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                <div className="w-2.5 h-2.5 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="p-4 bg-white border-t border-gray-200 shadow-lg">
        <div className="relative">
          <textarea
            ref={textareaRef}
            value={input}
            onChange={handleInputChange}
            onKeyPress={handleKeyPress}
            placeholder="Message Hermes..."
            rows={1}
            className="w-full pl-4 pr-12 py-3 bg-gray-100 border-2 border-transparent rounded-2xl focus:outline-none focus:ring-2 focus:ring-blue-400 focus:bg-white resize-none transition-all duration-200 placeholder:text-gray-400 overflow-y-auto"
            disabled={connectionStatus !== 'connected'}
            style={{ minHeight: '48px', maxHeight: '128px', resize: 'none' }}
          />
          <button
            onClick={handleSend}
            disabled={!input.trim() || connectionStatus !== 'connected'}
            className={clsx(
              'absolute right-2 bottom-3 w-8 h-8 rounded-full transition-all duration-200 flex items-center justify-center',
              input.trim() && connectionStatus === 'connected'
                ? 'bg-gradient-to-br from-blue-500 to-blue-600 hover:from-blue-600 hover:to-blue-700 text-white shadow-md hover:shadow-lg active:scale-90'
                : 'bg-gray-300 text-gray-400 cursor-not-allowed'
            )}
            title={input.trim() && connectionStatus === 'connected' ? 'Send message' : 'Type a message...'}
          >
            <svg className="w-4 h-4 transform rotate-45" fill="currentColor" viewBox="0 0 24 24">
              <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
            </svg>
          </button>
        </div>
      </div>
    </aside>
  );
}
