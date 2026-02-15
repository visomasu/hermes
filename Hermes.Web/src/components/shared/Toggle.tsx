import { forwardRef, useState } from 'react';
import type { InputHTMLAttributes } from 'react';
import clsx from 'clsx';

interface ToggleProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label?: string;
}

const Toggle = forwardRef<HTMLInputElement, ToggleProps>(
  ({ label, className, checked: controlledChecked, defaultChecked, onChange, ...props }, ref) => {
    // Support both controlled and uncontrolled modes
    const [internalChecked, setInternalChecked] = useState(defaultChecked ?? false);

    // Use controlled value if provided, otherwise use internal state
    const isChecked = controlledChecked !== undefined ? controlledChecked : internalChecked;

    const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
      // Update internal state if uncontrolled
      if (controlledChecked === undefined) {
        setInternalChecked(e.target.checked);
      }
      // Call parent onChange
      onChange?.(e);
    };

    return (
      <label className={clsx('flex items-center cursor-pointer group', className)}>
        <div className="relative">
          <input
            ref={ref}
            type="checkbox"
            className="sr-only"
            checked={controlledChecked}
            defaultChecked={defaultChecked}
            onChange={handleChange}
            {...props}
          />
          <div className={clsx(
            'block w-16 h-8 rounded-full transition-all duration-300 shadow-inner',
            isChecked
              ? 'bg-gradient-to-r from-blue-500 to-blue-600 shadow-blue-200'
              : 'bg-gray-300 group-hover:bg-gray-400'
          )}></div>
          <div className={clsx(
            'absolute left-1 top-1 w-6 h-6 rounded-full transition-all duration-300',
            'bg-white shadow-lg',
            isChecked && 'transform translate-x-8'
          )}></div>
        </div>
        {label && (
          <span className="ml-4 text-sm font-semibold text-gray-700 group-hover:text-gray-900 transition-colors">
            {label}
          </span>
        )}
      </label>
    );
  }
);

Toggle.displayName = 'Toggle';

export default Toggle;
