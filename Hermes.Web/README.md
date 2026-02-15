# Hermes Web UI

Modern React-based web interface for Hermes configuration management and chat.

## Features

- **User Configuration Management**: Manage notification preferences, quiet hours, and SLA settings
- **Real-time Chat**: Chat with Hermes AI assistant via WebSocket
- **Responsive 3-Pane Layout**: Left navigation, center configuration forms, right chat pane
- **Type-Safe**: Built with TypeScript for enhanced developer experience
- **Modern Stack**: React 18, Vite, TailwindCSS, React Query

## Prerequisites

- Node.js 18+ and npm
- Hermes backend running on `http://localhost:3978`

## Quick Start

### Install Dependencies

```bash
npm install
```

### Start Development Server

```bash
npm run dev
```

The application will be available at `http://localhost:5173`.

### Build for Production

```bash
npm run build
```

The production build will be in the `dist/` directory.

## Project Structure

```
src/
├── api/                    # API client modules
│   └── userConfigClient.ts
├── components/
│   ├── layout/             # Layout components
│   │   ├── AppLayout.tsx
│   │   ├── Sidebar.tsx
│   │   ├── MainContent.tsx
│   │   └── ChatPane.tsx
│   ├── forms/              # Form components
│   │   ├── UserConfigForm.tsx
│   │   └── TeamConfigForm.tsx
│   └── shared/             # Reusable UI components
│       ├── Button.tsx
│       ├── Input.tsx
│       ├── Toggle.tsx
│       └── Card.tsx
├── hooks/                  # Custom React hooks
│   ├── useWebSocket.ts
│   └── useUserConfig.ts
├── types/                  # TypeScript type definitions
│   ├── userConfig.ts
│   └── chat.ts
├── App.tsx                 # Root component
└── main.tsx                # Entry point
```

## Configuration

### Backend API URL

The default backend URL is `http://localhost:3978`. To change it, update:

- `src/api/userConfigClient.ts` - REST API base URL
- `src/components/layout/ChatPane.tsx` - WebSocket URL

### Test User

For development, the application uses a hardcoded test user ID: `testuser@microsoft.com`

To change it, update:
- `src/components/layout/ChatPane.tsx`
- `src/components/forms/UserConfigForm.tsx`

## Development

### Available Scripts

- `npm run dev` - Start development server with hot reload
- `npm run build` - Build for production
- `npm run preview` - Preview production build locally
- `npm run lint` - Run ESLint

### Code Style

- TypeScript for type safety
- React Hooks for state management
- React Query for server state
- TailwindCSS for styling
- Form validation with Zod and React Hook Form

## Architecture

### Communication with Backend

**REST API (User Configuration):**
- GET `/api/user-config/{userId}` - Fetch user configuration
- PUT `/api/user-config/{userId}` - Update user configuration
- DELETE `/api/user-config/{userId}` - Delete user configuration

**WebSocket (Chat):**
- URL: `ws://localhost:3978/api/hermes/ws`
- Message format:
  ```json
  {
    "text": "Hello Hermes",
    "userId": "user@example.com",
    "sessionId": "optional-session-id"
  }
  ```
- Response format:
  ```json
  {
    "type": "response" | "progress" | "error",
    "message": "Response text",
    "sessionId": "session-id"
  }
  ```

### State Management

- **Server State**: React Query for API data caching and synchronization
- **UI State**: React hooks (useState, useReducer) for local component state
- **WebSocket State**: Custom `useWebSocket` hook for connection management

## Known Limitations (MVP)

- No authentication (hardcoded test user)
- Team Configuration UI is placeholder only
- Chat history is not persisted (in-memory only)
- Single user session (no multi-user support)

## Future Enhancements

- Microsoft Entra ID authentication
- Team Configuration management
- Persistent chat history
- Markdown rendering in chat responses
- Export chat transcript
- Real-time notifications via SignalR
- Voice input support

## Troubleshooting

**Issue: "Failed to fetch" errors**
- Ensure the backend is running on `http://localhost:3978`
- Check CORS configuration in backend `Program.cs`

**Issue: WebSocket connection fails**
- Verify WebSocket middleware is enabled in backend
- Check WebSocket endpoint is accessible: `ws://localhost:3978/api/hermes/ws`
- Ensure firewall allows WebSocket connections

**Issue: TailwindCSS styles not applying**
- Clear Vite cache: `rm -rf node_modules/.vite`
- Restart dev server: `npm run dev`

## License

© 2026 Microsoft
