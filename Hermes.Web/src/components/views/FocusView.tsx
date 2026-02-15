import { useEffect } from 'react';
import MarkdownRenderer from '../shared/MarkdownRenderer';
import Button from '../shared/Button';

interface FocusViewProps {
  content: string;
  onExit: () => void;
}

export default function FocusView({ content, onExit }: FocusViewProps) {
  // Add keyboard shortcut to exit focus mode (Escape key)
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onExit();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onExit]);
  const handleCopy = () => {
    navigator.clipboard.writeText(content);
    // Show toast notification (optional - could be added later)
  };

  const handleExport = () => {
    const blob = new Blob([content], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `hermes-response-${Date.now()}.md`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="h-full flex flex-col bg-white">
      {/* Toolbar */}
      <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 bg-gradient-to-r from-blue-50 to-purple-50">
        <h2 className="text-xl font-bold text-gray-900">📖 Focus Mode</h2>
        <div className="flex gap-2">
          <Button
            onClick={handleCopy}
            className="bg-white border border-gray-300 text-gray-700 hover:bg-gray-50"
          >
            📋 Copy
          </Button>
          <Button
            onClick={handleExport}
            className="bg-white border border-gray-300 text-gray-700 hover:bg-gray-50"
          >
            💾 Export
          </Button>
          <Button
            onClick={onExit}
            className="bg-blue-600 hover:bg-blue-700 text-white"
          >
            ✕ Exit Focus
          </Button>
        </div>
      </div>

      {/* Content with full-width markdown */}
      <div className="flex-1 overflow-y-auto p-12 bg-gradient-to-br from-gray-50 via-white to-blue-50">
        <div className="max-w-4xl mx-auto bg-white rounded-2xl shadow-lg p-8 border border-gray-200">
          <MarkdownRenderer content={content} mode="full" />
        </div>
      </div>
    </div>
  );
}
