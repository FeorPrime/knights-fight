import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { RankedChallenger } from '@/entities/ranked-challenger';

const { getLeaderboard } = vi.hoisted(() => ({ getLeaderboard: vi.fn() }));

vi.mock('@/entities/ranked-challenger', () => ({
    rankedChallengerApi: { getLeaderboard },
}));

import { LeaderboardWidget } from './LeaderboardWidget';

declare global {
    // eslint-disable-next-line no-var
    var IS_REACT_ACT_ENVIRONMENT: boolean | undefined;
}

const REFRESH_INTERVAL = 5_000;

function challenger(overrides: Partial<RankedChallenger> = {}): RankedChallenger {
    return {
        id: 1,
        name: 'Alice',
        rank: 1,
        wins: 3,
        losses: 1,
        score: 9,
        ...overrides,
    };
}

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
                <LeaderboardWidget />
            </QueryClientProvider>,
        );
    });

    return { container, root };
}

describe('LeaderboardWidget', () => {
    beforeEach(() => {
        globalThis.IS_REACT_ACT_ENVIRONMENT = true;
        vi.useFakeTimers();
        getLeaderboard.mockReset();
    });

    afterEach(() => {
        vi.useRealTimers();
        document.body.innerHTML = '';
    });

    it('fetches the leaderboard on mount', async () => {
        getLeaderboard.mockResolvedValue([]);
        await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(getLeaderboard).toHaveBeenCalledTimes(1);
        expect(getLeaderboard).toHaveBeenCalledWith(20);
    });

    it('re-fetches every 5 seconds', async () => {
        getLeaderboard.mockResolvedValue([]);
        await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });
        expect(getLeaderboard).toHaveBeenCalledTimes(1);

        await act(async () => {
            await vi.advanceTimersByTimeAsync(REFRESH_INTERVAL);
        });
        expect(getLeaderboard).toHaveBeenCalledTimes(2);

        await act(async () => {
            await vi.advanceTimersByTimeAsync(REFRESH_INTERVAL);
        });
        expect(getLeaderboard).toHaveBeenCalledTimes(3);
    });

    it('does not re-fetch before the interval elapses', async () => {
        getLeaderboard.mockResolvedValue([]);
        await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        await act(async () => {
            await vi.advanceTimersByTimeAsync(REFRESH_INTERVAL - 1_000);
        });

        expect(getLeaderboard).toHaveBeenCalledTimes(1);
    });

    it('renders every challenger the API returns', async () => {
        getLeaderboard.mockResolvedValue([
            challenger({ id: 1, name: 'Alice', rank: 1, wins: 3, losses: 1, score: 9 }),
            challenger({ id: 2, name: 'Bob', rank: 2, wins: 1, losses: 2, score: 3 }),
            challenger({ id: 3, name: 'Carol', rank: 3, wins: 0, losses: 0, score: 0 }),
        ]);

        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.querySelectorAll('li')).toHaveLength(3);
        expect(container.textContent).toContain('Alice');
        expect(container.textContent).toContain('Bob');
        expect(container.textContent).toContain('Carol');
    });

    it('keeps a challenger who has never fought on the board', async () => {
        getLeaderboard.mockResolvedValue([
            challenger({ id: 1, name: 'Alice', rank: 1, wins: 3, losses: 1, score: 9 }),
            challenger({ id: 2, name: 'Fresh', rank: 2, wins: 0, losses: 0, score: 0 }),
        ]);

        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.querySelectorAll('li')).toHaveLength(2);
        expect(container.textContent).toContain('Fresh');
    });

    it('shows when the board was last refreshed', async () => {
        getLeaderboard.mockResolvedValue([challenger()]);
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.textContent).toContain('Updated');
    });

    it('re-renders with fresh data after a refresh', async () => {
        getLeaderboard.mockResolvedValue([challenger({ id: 1, name: 'Alice', rank: 1 })]);
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });
        expect(container.textContent).toContain('Alice');

        // the next poll brings a changed board
        getLeaderboard.mockResolvedValue([
            challenger({ id: 1, name: 'Alice', rank: 2, wins: 9, score: 27 }),
            challenger({ id: 2, name: 'Zoe', rank: 1, wins: 10, score: 30 }),
        ]);

        await act(async () => {
            await vi.advanceTimersByTimeAsync(REFRESH_INTERVAL);
        });
        // let the in-flight request settle and React re-render
        await act(async () => {
            await vi.advanceTimersByTimeAsync(1);
        });

        expect(container.textContent).toContain('Zoe');
        expect(container.textContent).toContain('30');
    });
});
