export type ServerEvent = { type: string; [k: string]: any };

type Unsubscribe = () => void;

export function connectServerEvents(onMessage: (ev: ServerEvent) => void): Unsubscribe {
	const es = new EventSource('/api/events');
	const handler = (e: MessageEvent) => {
		try {
			const data = JSON.parse(e.data);
			onMessage(data);
		} catch {}
	};
	// Safari may not fire 'message' on comments; only data events are parsed
	es.addEventListener('message', handler as any);
	return () => {
		es.removeEventListener('message', handler as any);
		es.close();
	};
}