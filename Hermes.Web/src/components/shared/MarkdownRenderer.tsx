import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { atomDark } from 'react-syntax-highlighter/dist/esm/styles/prism';
import clsx from 'clsx';
import type { Components } from 'react-markdown';

interface MarkdownRendererProps {
  content: string;
  mode: 'compact' | 'full'; // compact for chat, full for focus mode
  className?: string;
}

export default function MarkdownRenderer({
  content,
  mode,
  className
}: MarkdownRendererProps) {
  const components: Components = {
    // Custom code block with syntax highlighting
    code({ node, className, children, ...props }) {
      const hasInline = 'inline' in props;
      const inline = hasInline ? (props as any).inline : false;
      const match = /language-(\w+)/.exec(className || '');
      const language = match ? match[1] : 'text';

      return !inline ? (
        <SyntaxHighlighter
          style={atomDark as any}
          language={language}
          PreTag="div"
          className="rounded-lg my-2"
        >
          {String(children).replace(/\n$/, '')}
        </SyntaxHighlighter>
      ) : (
        <code className="bg-gray-100 px-1.5 py-0.5 rounded text-sm" {...props}>
          {children}
        </code>
      );
    },
    // Open links in new tab
    a({ node, children, href, ...props }) {
      return (
        <a
          href={href}
          target="_blank"
          rel="noopener noreferrer"
          className="text-blue-600 hover:text-blue-800 underline"
          {...props}
        >
          {children}
        </a>
      );
    },
    // Style tables
    table({ node, children, ...props }) {
      return (
        <div className="overflow-x-auto my-4">
          <table className="min-w-full divide-y divide-gray-300" {...props}>
            {children}
          </table>
        </div>
      );
    },
  };

  return (
    <div
      className={clsx(
        'prose prose-sm max-w-none',
        mode === 'compact' && 'prose-headings:text-base prose-p:text-sm prose-p:my-1',
        mode === 'full' && 'prose-lg prose-headings:mb-4 prose-p:my-3',
        className
      )}
    >
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={components}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
