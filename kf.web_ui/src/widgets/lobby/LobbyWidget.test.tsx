import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { LobbyStatus } from '@/entities/lobby';

const { getStatus, setReady } = vi.hoisted(() => ({
    getStatus: vi.fn(),
    setReady: vi.fn(),
}));

vi.mock('@/entities/lobby', () => ({
    lobbyApi: { getStatus, setReady },
}));

import { LobbyWidget } from './LobbyWidget';

declare global {
    // eslint-disable-next-line no-var
    var IS_REACT_ACT_ENVIRONMENT: boolean | undefined;
}

const REFRESH_INTERVAL = 3_000;

async function renderWidget() {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const root = createRoot(container);
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false, gcTime: 0 } },
    });

    await act(async () => {
        root.render(
            <QueryClientProvider client={queryClient}>
                <LobbyWidget />
            </QueryClientProvider>,
        );
    });

    return { container, root, queryClient };
}

describe('LobbyWidget', () => {
    beforeEach(() => {
        globalThis.IS_REACT_ACT_ENVIRONMENT = true;
        vi.useFakeTimers();
        getStatus.mockReset();
        setReady.mockReset();
    });

    afterEach(() => {
        vi.useRealTimers();
        document.body.innerHTML = '';
    });

    it('fetches lobby status on mount', async () => {
        getStatus.mockResolvedValue({ waitingCount: 0, queue: [], players: [] } satisfies LobbyStatus);
        await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(getStatus).toHaveBeenCalledTimes(1);
    });

    it('renders empty state when no fighters are in lobby', async () => {
        getStatus.mockResolvedValue({ waitingCount: 0, queue: [], players: [] } satisfies LobbyStatus);
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.textContent).toContain('No fighters currently in lobby');
        expect(container.textContent).toContain('Total in Lobby0');
    });

    it('renders fighters with ready and unready statuses and avatars', async () => {
        const readySince = new Date().toISOString();
        const mockData: LobbyStatus = {
            waitingCount: 2,
            queue: ['Alice', 'Bob'],
            players: [
                {
                    name: 'Alice',
                    isReady: false,
                    status: 'unready',
                    lastActive: new Date().toISOString(),
                    readySince: null,
                    avatarUrl: '/api/challengers/1/avatar',
                },
                {
                    name: 'Bob',
                    isReady: true,
                    status: 'ready',
                    lastActive: new Date().toISOString(),
                    readySince,
                    avatarUrl: null,
                },
            ],
        };

        getStatus.mockResolvedValue(mockData);
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.textContent).toContain('Alice');
        expect(container.textContent).toContain('Bob');
        expect(container.textContent).toContain('Unready');
        expect(container.textContent).toContain('Ready');
        expect(container.textContent).toContain('Total in Lobby2');
        expect(container.textContent).toContain('Ready1');
        expect(container.textContent).toContain('Unready1');
        expect(container.textContent).toContain('Ready since');

        const img = container.querySelector('img');
        expect(img).not.toBeNull();
        expect(img?.getAttribute('src')).toBe('/api/challengers/1/avatar');
    });

    it('polls lobby status automatically every 3 seconds', async () => {
        getStatus.mockResolvedValue({ waitingCount: 0, queue: [], players: [] } satisfies LobbyStatus);
        await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });
        expect(getStatus).toHaveBeenCalledTimes(1);

        await act(async () => {
            await vi.advanceTimersByTimeAsync(REFRESH_INTERVAL);
        });
        expect(getStatus).toHaveBeenCalledTimes(2);
    });
});
