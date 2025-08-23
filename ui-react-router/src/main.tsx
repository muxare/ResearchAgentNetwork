import React from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import './index.css';
import { AppLayout } from './routes/AppLayout';
import { TasksPage } from './routes/TasksPage';
import { ReportPage } from './routes/ReportPage';
import { ChatPage } from './routes/ChatPage';
import { ResearchListPage } from './routes/ResearchListPage';
import { ResearchDetailPage } from './routes/ResearchDetailPage';
import { MonitoringPage } from './routes/MonitoringPage';
import { AdminPage } from './routes/AdminPage';
import { TaskProgressPage } from './routes/TaskProgressPage';

const router = createBrowserRouter([
	{
		path: '/',
		element: <AppLayout />,
		children: [
			{ index: true, element: <TasksPage /> },
			{ path: 'chat', element: <ChatPage /> },
			{ path: 'research', element: <ResearchListPage /> },
			{ path: 'research/:slug', element: <ResearchDetailPage /> },
			{ path: 'monitoring', element: <MonitoringPage /> },
			{ path: 'admin', element: <AdminPage /> },
			{ path: 'tasks/:id/progress', element: <TaskProgressPage /> },
			{ path: 'tasks/:id/report', element: <ReportPage /> },
		],
	},
]);

const root = createRoot(document.getElementById('root')!);
root.render(
	<React.StrictMode>
		<RouterProvider router={router} />
	</React.StrictMode>
);

