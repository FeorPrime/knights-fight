import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppHeader } from '@/app/AppHeader';
import { useViewStore } from '@/app/model/viewStore';
import { MainPage } from './MainPage';

vi.mock('@/widgets/leaderboard', () => ({ LeaderboardWidget: () => null }));
vi.mock('@/widgets/current-matches', () => ({ CurrentMatchesWidget: () => null }));
vi.mock('@/widgets/lobby', () => ({ LobbyWidget: () => null }));

describe('API documentation view', () => {
    afterEach(() => {
        useViewStore.getState().setActiveView('leaderboard');
        document.body.innerHTML = '';
    });

    it('opens the documentation tab with same-origin links', async () => {
        globalThis.IS_REACT_ACT_ENVIRONMENT = true;
        const container = document.createElement('div');
        document.body.appendChild(container);
        const root = createRoot(container);

        await act(async () => {
            root.render(<><AppHeader /><MainPage /></>);
        });
        const tab = Array.from(container.querySelectorAll('button')).find((button) =>
            button.textContent?.includes('API Documentation'),
        );
        expect(tab).toBeDefined();
        await act(async () => { tab?.click(); });

        expect(useViewStore.getState().activeView).toBe('docs');
        expect(Array.from(container.querySelectorAll('main a')).map((link) => link.getAttribute('href')))
            .toEqual(['/api/swagger', '/api/scalar']);
        await act(async () => { root.unmount(); });
    });
});