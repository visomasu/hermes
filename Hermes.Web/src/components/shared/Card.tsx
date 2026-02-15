import type { HTMLAttributes } from 'react';
import clsx from 'clsx';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  title?: string;
}

export default function Card({ title, children, className, ...props }: CardProps) {
  return (
    <div
      className={clsx(
        'bg-white rounded-2xl shadow-lg border border-gray-100 overflow-hidden',
        'hover:shadow-xl transition-shadow duration-300',
        className
      )}
      {...props}
    >
      {title && (
        <div className="px-8 py-5 bg-gradient-to-r from-gray-50 to-white border-b border-gray-100">
          <h3 className="text-xl font-bold text-gray-900">{title}</h3>
        </div>
      )}
      <div className="p-8">{children}</div>
    </div>
  );
}
