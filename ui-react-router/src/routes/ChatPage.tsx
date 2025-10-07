import React from 'react';

export const ChatPage: React.FC = () => {
	return (
		<div className="flex flex-col h-full">
			<div className="flex-1 overflow-y-auto p-4 space-y-4">
				<div className="flex items-start gap-3">
					<div className="w-8 h-8 rounded-full bg-indigo-500 flex items-center justify-center text-white font-bold text-sm">AI</div>
					<div className="bg-white dark:bg-gray-800 rounded-lg p-3 max-w-lg shadow">
						<p className="text-sm">Hello! I am your research assistant. How can I help you today?</p>
					</div>
				</div>
				<div className="flex items-start gap-3 justify-end">
					<div className="bg-indigo-500 text-white rounded-lg p-3 max-w-lg shadow">
						<p className="text-sm">Tell me about the latest findings on ethical AI.</p>
					</div>
					<div className="w-8 h-8 rounded-full bg-gray-600 flex items-center justify-center text-white font-bold text-sm">You</div>
				</div>
			</div>
			<div className="p-4 bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700 mt-auto">
				<div className="relative">
					<input type="text" placeholder="Ask a question based on the vector storage..." className="input py-3 pl-4 pr-12 bg-gray-100 dark:bg-gray-700 border-0 rounded-full" />
					<button className="absolute inset-y-0 right-0 flex items-center justify-center w-12 text-indigo-600 hover:text-indigo-700">
						<svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" /></svg>
					</button>
				</div>
			</div>
		</div>
	);
};

