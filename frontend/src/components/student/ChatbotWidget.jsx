import { useState, useEffect, useRef } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkMath from 'remark-math';
import rehypeKatex from 'rehype-katex';
import MaterialIcon from '../ui/MaterialIcon';
import { askChatbot } from '../../services/chatbotApi';

/**
 * Floating chatbot widget hiển thị ở góc dưới phải màn hình.
 *
 * Khi học sinh bấm "Hỏi AI giải thích câu này" ở một câu hỏi bất kỳ,
 * widget sẽ mở ra với ngữ cảnh của câu hỏi đó (questionContent + đáp án đúng).
 *
 * @param {{
 *   isOpen: boolean,
 *   onClose: () => void,
 *   context: {
 *     sessionId: string,
 *     questionId: string,
 *     questionNo: number,
 *     questionContent: string,
 *     correctAnswer: string,
 *   } | null
 * }} props
 */
export default function ChatbotWidget({ isOpen, onClose, context }) {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [lastContextId, setLastContextId] = useState(null);
  const messagesEndRef = useRef(null);
  const inputRef = useRef(null);

  // Khi context thay đổi (bấm câu hỏi khác), thêm separator + thông báo
  useEffect(() => {
    if (!context) return;
    const newId = context.questionId;
    if (lastContextId && lastContextId !== newId) {
      setMessages((prev) => [
        ...prev,
        {
          id: `ctx-${newId}`,
          role: 'system',
          text: `Đã chuyển sang câu hỏi số **${context.questionNo}**.`,
        },
      ]);
    }
    setLastContextId(newId);
  }, [context?.questionId]); // eslint-disable-line react-hooks/exhaustive-deps

  // Auto-scroll xuống cuối khi có tin nhắn mới
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, loading]);

  // Focus input khi widget mở
  useEffect(() => {
    if (isOpen) {
      setTimeout(() => inputRef.current?.focus(), 100);
    }
  }, [isOpen]);

  const handleSend = async () => {
    if (!input.trim() || loading || !context) return;

    const userText = input.trim();
    setInput('');
    setError('');

    const userMsg = { id: Date.now(), role: 'user', text: userText };
    setMessages((prev) => [...prev, userMsg]);
    setLoading(true);

    try {
      const data = await askChatbot({
        sessionId: context.sessionId,
        questionId: context.questionId,
        questionContent: context.questionContent,
        studentAnswer: context.correctAnswer,
        userMessage: userText,
      });

      setMessages((prev) => [
        ...prev,
        { id: Date.now() + 1, role: 'assistant', text: data.explanation },
      ]);
    } catch (err) {
      const status = err?.response?.status;
      let errorMsg = 'Có lỗi xảy ra. Vui lòng thử lại.';
      if (status === 429) errorMsg = 'Bạn đã hỏi quá nhanh. Vui lòng thử lại sau ít phút.';
      if (status === 503) errorMsg = 'Dịch vụ AI đang bận. Vui lòng thử lại sau.';
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed bottom-6 right-6 z-50 flex flex-col w-[380px] max-h-[560px] bg-pure-surface border border-whisper-border rounded-2xl shadow-2xl overflow-hidden animate-in slide-in-from-bottom-4 fade-in duration-200"
      role="dialog"
      aria-label="Trợ lý AI"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 bg-primary text-white flex-shrink-0">
        <div className="flex items-center gap-2">
          <MaterialIcon name="smart_toy" size={20} />
          <div>
            <p className="font-bold text-sm leading-tight">Trợ lý AI</p>
            {context && (
              <p className="text-[11px] opacity-80 leading-tight">
                Câu {context.questionNo}
              </p>
            )}
          </div>
        </div>
        <button
          onClick={onClose}
          className="p-1.5 rounded-full hover:bg-white/20 transition-colors"
          aria-label="Đóng cửa sổ chat"
        >
          <MaterialIcon name="close" size={18} />
        </button>
      </div>

      {/* Context info */}
      {context && (
        <div className="px-4 py-2 bg-primary/5 border-b border-whisper-border flex-shrink-0">
          <p className="text-[11px] text-on-surface-variant line-clamp-2">
            <span className="font-bold text-primary">Đáp án đúng: </span>
            {context.correctAnswer}
          </p>
        </div>
      )}

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-4 py-3 space-y-3 min-h-0">
        {messages.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-center py-6">
            <span className="text-4xl">🤖</span>
            <p className="text-sm text-on-surface-variant">
              Hỏi tôi bất cứ điều gì về câu hỏi này!
            </p>
            <div className="flex flex-col gap-2 w-full">
              {['Tại sao đáp án lại là vậy?', 'Giải thích chi tiết hơn đi', 'Có cách giải nào khác không?'].map(
                (suggestion) => (
                  <button
                    key={suggestion}
                    onClick={() => setInput(suggestion)}
                    className="text-xs px-3 py-1.5 rounded-full border border-primary/30 text-primary hover:bg-primary/5 transition-colors text-left"
                  >
                    {suggestion}
                  </button>
                )
              )}
            </div>
          </div>
        )}

        {messages.map((msg) => {
          if (msg.role === 'system') {
            return (
              <div key={msg.id} className="text-center">
                <span className="text-[11px] text-outline bg-surface-container px-3 py-1 rounded-full inline-block">
                  <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}
                    components={{ p: ({ children }) => <span>{children}</span> }}>
                    {msg.text}
                  </ReactMarkdown>
                </span>
              </div>
            );
          }

          if (msg.role === 'user') {
            return (
              <div key={msg.id} className="flex justify-end">
                <div className="max-w-[80%] bg-primary text-white px-3 py-2 rounded-2xl rounded-tr-sm text-sm">
                  {msg.text}
                </div>
              </div>
            );
          }

          return (
            <div key={msg.id} className="flex justify-start">
              <div className="max-w-[85%] bg-surface-container-low border border-whisper-border px-3 py-2 rounded-2xl rounded-tl-sm text-sm text-on-surface">
                <div className="prose prose-sm max-w-none prose-p:my-1 prose-ul:my-1 prose-ol:my-1">
                  <ReactMarkdown remarkPlugins={[remarkMath]} rehypePlugins={[rehypeKatex]}>
                    {msg.text}
                  </ReactMarkdown>
                </div>
              </div>
            </div>
          );
        })}

        {/* Typing indicator */}
        {loading && (
          <div className="flex justify-start">
            <div className="bg-surface-container-low border border-whisper-border px-4 py-3 rounded-2xl rounded-tl-sm">
              <div className="flex gap-1 items-center">
                <span className="w-2 h-2 bg-primary/50 rounded-full animate-bounce [animation-delay:0ms]" />
                <span className="w-2 h-2 bg-primary/50 rounded-full animate-bounce [animation-delay:150ms]" />
                <span className="w-2 h-2 bg-primary/50 rounded-full animate-bounce [animation-delay:300ms]" />
              </div>
            </div>
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="text-xs text-deep-rose bg-deep-rose/5 border border-deep-rose/20 rounded-lg px-3 py-2">
            {error}
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input area */}
      <div className="border-t border-whisper-border px-3 py-3 flex gap-2 items-end flex-shrink-0 bg-pure-surface">
        <textarea
          ref={inputRef}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Nhập câu hỏi của bạn..."
          rows={1}
          disabled={loading || !context}
          className="flex-1 resize-none rounded-xl border border-outline-variant bg-surface-container-low px-3 py-2 text-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary disabled:opacity-50 max-h-24 leading-relaxed"
          style={{ height: 'auto' }}
          onInput={(e) => {
            e.target.style.height = 'auto';
            e.target.style.height = `${Math.min(e.target.scrollHeight, 96)}px`;
          }}
        />
        <button
          onClick={handleSend}
          disabled={!input.trim() || loading || !context}
          aria-label="Gửi câu hỏi"
          className="flex-shrink-0 w-9 h-9 rounded-full bg-primary text-white flex items-center justify-center hover:opacity-90 transition-all disabled:opacity-40 disabled:cursor-not-allowed"
        >
          <MaterialIcon name="send" size={18} />
        </button>
      </div>
    </div>
  );
}
