import React from 'react';
import { TaskDetails } from './TaskDetails';

export const TaskDetailsModal: React.FC<{ open: boolean; taskId: string | null; onClose: () => void; onSelect?: (id: string) => void }>
	= ({ open, taskId, onClose, onSelect }) => {
	if (!open) return null;
	return (
		<div className="fixed inset-0 z-40 flex items-start md:items-center justify-center p-2 md:p-6">
			<button type="button" className="absolute inset-0 bg-black/30" aria-label="Close" onClick={onClose}></button>
			<div className="relative z-10 w-full md:w-5/6 lg:w-3/4 xl:w-2/3 max-h-[85vh] overflow-auto bg-white rounded-xl shadow-xl border p-3">
				<div className="flex items-center justify-between border-b pb-2 mb-3">
					<h2 className="text-sm font-semibold">Task details</h2>
					<button type="button" className="px-2 py-1 rounded border text-xs bg-gray-50 hover:bg-gray-100" onClick={onClose}>Close</button>
				</div>
				<TaskDetails taskId={taskId} onSelect={onSelect} />
			</div>
		</div>
	);
};

