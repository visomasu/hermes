import { useState } from 'react';
import Header from './Header';
import Sidebar from './Sidebar';
import type { ActiveView } from './Sidebar';
import MainContent from './MainContent';
import ChatPane from './ChatPane';

export default function AppLayout() {
  const [activeView, setActiveView] = useState<ActiveView>('user-config');
  const [chatOpen, setChatOpen] = useState(true);
  const [sidebarOpen, setSidebarOpen] = useState(true);

  return (
    <div className="flex flex-col h-screen bg-gray-100">
      {/* Top Header Banner */}
      <Header />

      {/* Main Content Area */}
      <div className="flex flex-1 overflow-hidden">
        {/* Left Sidebar */}
        <Sidebar
          activeView={activeView}
          onViewChange={setActiveView}
          isOpen={sidebarOpen}
          onToggle={() => setSidebarOpen(!sidebarOpen)}
        />

        {/* Main Content */}
        <MainContent activeView={activeView} />

        {/* Right Chat Pane (Collapsible) */}
        {chatOpen && <ChatPane onClose={() => setChatOpen(false)} />}

        {/* Toggle Chat Button (when closed) */}
        {!chatOpen && (
          <button
            onClick={() => setChatOpen(true)}
            className="fixed bottom-8 right-8 bg-gradient-to-br from-blue-500 to-blue-600 text-white p-5 rounded-2xl shadow-2xl hover:from-blue-600 hover:to-blue-700 transition-all hover:scale-110 active:scale-95 group"
            title="Open Chat"
          >
            <svg className="w-6 h-6 group-hover:scale-110 transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
            </svg>
          </button>
        )}
      </div>
    </div>
  );
}
