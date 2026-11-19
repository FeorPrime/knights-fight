import { create } from 'zustand';

export type ViewKey = 'leaderboard' | 'matches' | 'lobby' | 'docs';

type ViewStore = {
    activeView: ViewKey;
    setActiveView: (view: ViewKey) => void;
};

export const useViewStore = create<ViewStore>((set) => ({
    activeView: 'leaderboard',
    setActiveView: (view) => set({ activeView: view }),
}));

export const VIEW_LABELS: Record<ViewKey, string> = {
    leaderboard: 'Leaderboard',
    matches: 'Current Matches',
    lobby: 'Lobby',
    docs: 'API Documentation',
};