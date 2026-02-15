import type { ActiveView } from './Sidebar';
import UserConfigForm from '../forms/UserConfigForm';
import TeamConfigForm from '../forms/TeamConfigForm';
import Card from '../shared/Card';
import FocusView from '../views/FocusView';

interface MainContentProps {
  activeView: ActiveView;
  focusContent?: string;
  onExitFocus?: () => void;
}

function AboutView() {
  return (
    <Card title="About Hermes">
      <div className="space-y-4">
        <div>
          <h4 className="font-semibold text-gray-900 mb-2">What is Hermes?</h4>
          <p className="text-gray-600">
            Hermes is an AI-powered project assistant that integrates with Azure DevOps to
            generate executive communications and validate work item hierarchies. It operates
            as both a Microsoft Teams bot and REST/WebSocket API.
          </p>
        </div>

        <div>
          <h4 className="font-semibold text-gray-900 mb-2">Features</h4>
          <ul className="list-disc list-inside text-gray-600 space-y-1">
            <li>SLA violation monitoring and notifications</li>
            <li>Work item hierarchy validation</li>
            <li>Newsletter generation for features and epics</li>
            <li>Real-time chat with AI assistant</li>
            <li>Customizable notification preferences</li>
          </ul>
        </div>

        <div>
          <h4 className="font-semibold text-gray-900 mb-2">Technology Stack</h4>
          <p className="text-gray-600">
            <strong>Backend:</strong> .NET 8.0, ASP.NET Core, Azure OpenAI, Azure Cosmos DB, Azure DevOps SDK
            <br />
            <strong>Frontend:</strong> React 18, TypeScript, Vite, TailwindCSS, React Query
          </p>
        </div>

        <div className="pt-4 border-t border-gray-200">
          <p className="text-sm text-gray-500">
            Version 1.0.0 | © 2026 Microsoft
          </p>
        </div>
      </div>
    </Card>
  );
}

export default function MainContent({
  activeView,
  focusContent = '',
  onExitFocus
}: MainContentProps) {
  return (
    <main className="flex-1 overflow-y-auto bg-gradient-to-br from-gray-50 via-white to-blue-50">
      {activeView === 'focus' ? (
        <FocusView
          content={focusContent}
          onExit={onExitFocus || (() => {})}
        />
      ) : (
        <div className="p-8">
          <div className="max-w-5xl mx-auto">
            {activeView === 'user-config' && <UserConfigForm />}
            {activeView === 'team-config' && <TeamConfigForm />}
            {activeView === 'about' && <AboutView />}
          </div>
        </div>
      )}
    </main>
  );
}
