import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Challenger, ChallengersPage } from '@/entities/challenger';

const { list } = vi.hoisted(() => ({ list: vi.fn() }));
const { createSSE } = vi.hoisted(() => ({ createSSE: vi.fn() }));

vi.mock('@/entities/challenger', () => ({
    challengerApi: { list },
}));

vi.mock('@/shared/lib/sse', () => ({
    createSSE,
}));

import { NewcomersWidget } from './NewcomersWidget';
import { isNewcomer, filterNewcomers, MAX_VISIBLE } from './filter';

declare global {
    // eslint-disable-next-line no-var
    var IS_REACT_ACT_ENVIRONMENT: boolean | undefined;
}

function makeChallenger(id: number, minutesAgo: number): Challenger {
    const registeredAt = new Date(Date.now() - minutesAgo * 60 * 1000).toISOString();
    return {
        id,
        name: `Knight_${id}`,
        registeredAt,
        avatarUrl: null,
    };
}

function makePage(items: Challenger[]): ChallengersPage {
    return {
        items,
        totalCount: items.length,
        page: 0,
        pageSize: 15,
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
                <NewcomersWidget />
            </QueryClientProvider>,
        );
    });

    return { container, root, queryClient };
}

describe('isNewcomer & filterNewcomers', () => {
    it('identifies challengers registered within 15 minutes as newcomers', () => {
        const now = 1_000_000_000_000;
        const recent = new Date(now - 14 * 60 * 1000).toISOString();
        const boundary = new Date(now - 15 * 60 * 1000).toISOString();
        const old = new Date(now - 16 * 60 * 1000).toISOString();

        expect(isNewcomer(recent, now)).toBe(true);
        expect(isNewcomer(boundary, now)).toBe(true);
        expect(isNewcomer(old, now)).toBe(false);
    });

    it('filters out newcomers older than 15 minutes and caps at 15 items', () => {
        const now = 1_000_000_000_000;
        const items: Challenger[] = [];

        // 20 newcomers within 10 minutes
        for (let i = 1; i <= 20; i++) {
            items.push({
                id: i,
                name: `Knight_${i}`,
                registeredAt: new Date(now - 5 * 60 * 1000).toISOString(),
                avatarUrl: null,
            });
        }
        // 5 older challengers (> 15 min)
        for (let i = 21; i <= 25; i++) {
            items.push({
                id: i,
                name: `OldKnight_${i}`,
                registeredAt: new Date(now - 20 * 60 * 1000).toISOString(),
                avatarUrl: null,
            });
        }

        const filtered = filterNewcomers(items, now);
        expect(filtered).toHaveLength(MAX_VISIBLE);
        expect(filtered.every((c) => !c.name.startsWith('OldKnight'))).toBe(true);
        expect(filtered.map((c) => c.id)).toEqual(
            Array.from({ length: 15 }, (_, i) => i + 1),
        );
    });
});

describe('NewcomersWidget', () => {
    let sseCallback: ((newcomer: Challenger) => void) | undefined;

    beforeEach(() => {
        globalThis.IS_REACT_ACT_ENVIRONMENT = true;
        vi.useFakeTimers();
        list.mockReset();
        createSSE.mockReset();
        createSSE.mockImplementation((_url: string, onMessage: (msg: Challenger) => void) => {
            sseCallback = onMessage;
            return () => {
                sseCallback = undefined;
            };
        });
    });

    afterEach(() => {
        vi.useRealTimers();
        document.body.innerHTML = '';
    });

    it('fetches newcomers with page size 15 on mount', async () => {
        list.mockResolvedValue(makePage([]));
        await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(list).toHaveBeenCalledTimes(1);
        expect(list).toHaveBeenCalledWith(0, 15);
    });

    it('shows only challengers registered within 15 minutes', async () => {
        const fresh1 = makeChallenger(1, 2); // 2 min ago
        const fresh2 = makeChallenger(2, 10); // 10 min ago
        const expired = makeChallenger(3, 16); // 16 min ago

        list.mockResolvedValue(makePage([fresh1, fresh2, expired]));
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        const listItems = container.querySelectorAll('li');
        expect(listItems).toHaveLength(2);
        expect(container.textContent).toContain('Knight_1');
        expect(container.textContent).toContain('Knight_2');
        expect(container.textContent).not.toContain('Knight_3');
    });

    it('shows "No newcomers yet." when all challengers are older than 15 minutes', async () => {
        const expired1 = makeChallenger(1, 16);
        const expired2 = makeChallenger(2, 60);

        list.mockResolvedValue(makePage([expired1, expired2]));
        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.querySelectorAll('li')).toHaveLength(0);
        expect(container.textContent).toContain('No newcomers yet.');
    });

    it('limits display to at most 15 newcomers when more than 15 exist', async () => {
        const challengers = Array.from({ length: 20 }, (_, i) => makeChallenger(i + 1, 1));
        list.mockResolvedValue(makePage(challengers));

        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        const listItems = container.querySelectorAll('li');
        expect(listItems).toHaveLength(15);
    });

    it('integrates real-time SSE newcomer events and filters out expired newcomers', async () => {
        const initial = Array.from({ length: 15 }, (_, i) => makeChallenger(i + 1, 5));
        list.mockResolvedValue(makePage(initial));

        const { container } = await renderWidget();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(0);
        });

        expect(container.querySelectorAll('li')).toHaveLength(15);
        expect(container.textContent).toContain('Knight_1');

        // New challenger arrives via SSE
        const realTimeNewcomer = makeChallenger(99, 0);
        await act(async () => {
            sseCallback?.(realTimeNewcomer);
            await vi.advanceTimersByTimeAsync(0);
        });

        const listItems = container.querySelectorAll('li');
        expect(listItems).toHaveLength(15);
        expect(container.textContent).toContain('Knight_99');
        // The 15th old challenger should have been pushed out
        expect(container.textContent).not.toContain('Knight_15');
    });
});
