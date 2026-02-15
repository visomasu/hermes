export default function Header() {
  return (
    <header className="bg-gradient-to-r from-blue-600 to-blue-700 text-white shadow-lg">
      <div className="flex items-center justify-between px-8 py-4">
        {/* Logo/Title with icon */}
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 bg-white/20 rounded-lg flex items-center justify-center backdrop-blur-sm">
            <span className="text-xl">📨</span>
          </div>
          <h1 className="text-2xl font-bold tracking-tight">Hermes</h1>
        </div>

        {/* Right side */}
        <div className="flex items-center gap-4">
          <span className="text-xs bg-white/10 px-3 py-1 rounded-full backdrop-blur-sm">
            v1.0.0
          </span>
        </div>
      </div>
    </header>
  );
}
