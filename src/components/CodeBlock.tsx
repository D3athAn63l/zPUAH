import { useState } from 'react';

interface CodeBlockProps {
  code: string;
  language?: string;
  filename?: string;
  maxHeight?: string;
}

export default function CodeBlock({ code, language = 'csharp', filename, maxHeight = '500px' }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="rounded-lg overflow-hidden border border-gray-700 my-4">
      <div className="flex items-center justify-between bg-gray-800 px-4 py-2 border-b border-gray-700">
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-400 font-mono">{filename || language}</span>
        </div>
        <button
          onClick={handleCopy}
          className="text-xs px-2 py-1 rounded bg-gray-700 hover:bg-gray-600 text-gray-300 transition-colors"
        >
          {copied ? '✓ Copied' : '📋 Copy'}
        </button>
      </div>
      <div
        className="overflow-auto bg-gray-900 p-4"
        style={{ maxHeight }}
      >
        <pre className="text-sm font-mono text-gray-300 leading-relaxed whitespace-pre">
          <code>{code}</code>
        </pre>
      </div>
    </div>
  );
}
