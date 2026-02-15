import clsx from 'clsx';

export type ActiveView = 'user-config' | 'team-config' | 'about' | 'focus';

interface SidebarProps {
  activeView: ActiveView;
  onViewChange: (view: ActiveView) => void;
  isOpen: boolean;
  onToggle: () => void;
}

interface NavItemProps {
  id: ActiveView;
  label: string;
  icon: string;
  active: boolean;
  onClick: () => void;
  isOpen: boolean;
}

function NavItem({ label, icon, active, onClick, isOpen }: NavItemProps) {
  return (
    <button
      onClick={onClick}
      className={clsx(
        'w-full flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200',
        'group relative overflow-hidden',
        active
          ? 'bg-gradient-to-r from-blue-500 to-blue-600 text-white shadow-md'
          : 'text-gray-700 hover:bg-gray-100 hover:shadow-sm',
        !isOpen && 'justify-center'
      )}
      title={!isOpen ? label : undefined}
    >
      <span className={clsx(
        'text-xl transition-transform group-hover:scale-110',
        active && 'animate-bounce'
      )}>
        {icon}
      </span>
      {isOpen && (
        <span className="font-medium">{label}</span>
      )}
    </button>
  );
}

export default function Sidebar({ activeView, onViewChange, isOpen, onToggle }: SidebarProps) {
  const navItems: Omit<NavItemProps, 'active' | 'onClick' | 'isOpen'>[] = [
    { id: 'user-config', label: 'User Settings', icon: '⚙️' },
    { id: 'team-config', label: 'Team Settings', icon: '👥' },
    { id: 'about', label: 'About', icon: 'ℹ️' },
  ];

  return (
    <aside
      className={clsx(
        'bg-white border-r border-gray-200 flex flex-col transition-all duration-300 relative shadow-lg',
        isOpen ? 'w-72' : 'w-20'
      )}
    >
      {/* Toggle Button */}
      <button
        onClick={onToggle}
        className={clsx(
          'absolute -right-3 top-6 w-6 h-6 bg-gradient-to-br from-blue-500 to-blue-600',
          'text-white rounded-full hover:from-blue-600 hover:to-blue-700',
          'transition-all shadow-lg flex items-center justify-center z-10',
          'hover:scale-110 active:scale-95'
        )}
        title={isOpen ? 'Collapse sidebar' : 'Expand sidebar'}
      >
        {isOpen ? (
          <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M15 19l-7-7 7-7" />
          </svg>
        ) : (
          <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={3} d="M9 5l7 7-7 7" />
          </svg>
        )}
      </button>

      {/* Navigation */}
      <nav className="flex-1 p-4 space-y-2 mt-4">
        {navItems.map((item) => (
          <NavItem
            key={item.id}
            {...item}
            active={activeView === item.id}
            onClick={() => onViewChange(item.id)}
            isOpen={isOpen}
          />
        ))}
      </nav>

      {/* Footer */}
      {isOpen && (
        <div className="p-4 border-t border-gray-200">
          <div className="bg-gradient-to-br from-gray-50 to-gray-100 rounded-lg p-3">
            <p className="text-xs font-semibold text-gray-700">Hermes v1.0.0</p>
            <p className="text-xs text-gray-500 mt-1">© 2026 Microsoft</p>
          </div>
        </div>
      )}
    </aside>
  );
}
