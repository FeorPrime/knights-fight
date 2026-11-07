export type SSEHandler<T> = (data: T) => void;

export function createSSE<T>(
    url: string,
    onMessage: SSEHandler<T>,
    onError?: (err: Event) => void,
): () => void {
    const source = new EventSource(url);

    source.onmessage = (event) => {
        try {
            const data = JSON.parse(event.data) as T;
            onMessage(data);
        } catch (err) {
            console.error('[SSE] parse error', err);
        }
    };

    source.onerror = (err) => {
        onError?.(err);
        // EventSource auto-reconnects; we just log
    };

    return () => source.close();
}