import { describe, expect, it } from 'vitest';
import { VIEW_LABELS, useViewStore, type ViewKey } from './viewStore';

describe('viewStore', () => {
    it('starts on the leaderboard', () => {
        expect(useViewStore.getState().activeView).toBe('leaderboard');
    });

    it('switches the active view', () => {
        useViewStore.getState().setActiveView('lobby');
        expect(useViewStore.getState().activeView).toBe('lobby');
        useViewStore.getState().setActiveView('leaderboard');
    });

    it('labels every view', () => {
        const keys = Object.keys(VIEW_LABELS) as ViewKey[];
        expect(keys).toContain('leaderboard');
        expect(keys).toContain('matches');
        expect(keys).toContain('lobby');
    });
});
