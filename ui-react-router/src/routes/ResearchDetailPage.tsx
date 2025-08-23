import React from 'react';
import { useParams } from 'react-router-dom';

const mockDetails: Record<string, { title: string; documents: Array<{ id: number; title: string; type: string }> }> = {
	'ai-ethics-q1': {
		title: 'Ethical Implications of AI in Healthcare',
		documents: [
			{ id: 1, title: 'Paper on AI Bias in Medical Imaging', type: 'PDF' },
			{ id: 2, title: 'Patient Data Privacy Concerns', type: 'Article' },
			{ id: 3, title: 'Regulatory Frameworks for AI in Medicine', type: 'PDF' },
		],
	},
	'climate-change-q2': {
		title: 'Impact of Climate Change on Coastal Cities',
		documents: [
			{ id: 1, title: 'Sea Level Rise Projections', type: 'Report' },
			{ id: 2, title: 'Economic Impact on Fisheries', type: 'Article' },
		],
	},
	'quantum-computing-q3': {
		title: 'Advancements in Quantum Computing',
		documents: [
			{ id: 1, title: 'Quantum Supremacy Milestone', type: 'News' },
			{ id: 2, title: 'Cryptography in the Quantum Era', type: 'PDF' },
			{ id: 3, title: 'Hardware Development Challenges', type: 'Article' },
		],
	},
};

export const ResearchDetailPage: React.FC = () => {
	const { slug = '' } = useParams();
	const details = mockDetails[slug];
	if (!details) return <h1 className="text-3xl font-bold">Query Not Found</h1>;
	return (
		<div>
			<h1 className="text-3xl font-bold">{details.title}</h1>
			<div className="bg-white dark:bg-gray-800 p-6 rounded-lg shadow-md mt-4">
				<h2 className="text-xl font-semibold mb-4">Related Documents</h2>
				<ul className="space-y-3">
					{details.documents.map(doc => (
						<li key={doc.id} className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-md shadow-sm">
							<div className="flex items-center">
								<span className="mr-3 text-indigo-500">
									<svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
								</span>
								<span className="text-sm font-medium">{doc.title}</span>
							</div>
							<span className="text-xs font-semibold uppercase px-2 py-1 bg-indigo-200 text-indigo-800 rounded-full">{doc.type}</span>
						</li>
					))}
				</ul>
			</div>
		</div>
	);
};

