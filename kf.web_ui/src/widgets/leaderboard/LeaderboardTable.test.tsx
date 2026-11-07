import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { RankedChallenger } from '@/entities/ranked-challenger';

import { LeaderboardTable } from './LeaderboardTable';

declare global {
    // eslint-disable-next-line no-var
    var IS_REACT_ACT_ENVIRONMENT: boolean | undefined;
}

function challenger(overrides: Partial<RankedChallenger> = {}): RankedChallenger {
    return {
        id: 1,
        name: 'Alice',
        rank: 1,
        wins: 3,
        losses: 1,
        score: 9,
        avatarUrl: null,
        ...overrides,
    };
}

async function renderTable(items: RankedChallenger[]) {
    const container = document.createElement('div');
    document.body.appendChild(container);

    const root = createRoot(container);

    await act(async () => {
        root.render(<LeaderboardTable items={items} />);
    });

    return container;
}

describe('LeaderboardTable', () => {
    beforeEach(() => {
        globalThis.IS_REACT_ACT_ENVIRONMENT = true;
    });

    afterEach(() => {
        document.body.innerHTML = '';
    });

    it('renders the avatar image when the challenger has one', async () => {
        const container = await renderTable([
            challenger({ avatarUrl: '/api/challengers/1/avatar' }),
        ]);

        const image = container.querySelector('img');
        expect(image).not.toBeNull();
        expect(image?.getAttribute('src')).toBe('/api/challengers/1/avatar');
    });

    it('falls back to the initial letter when there is no avatar', async () => {
        const container = await renderTable([challenger({ avatarUrl: null })]);

        expect(container.querySelector('img')).toBeNull();
        expect(container.textContent).toContain('A');
    });
});
