import { useViewStore } from '@/app/model/viewStore';
import { LeaderboardWidget } from '@/widgets/leaderboard';
import { CurrentMatchesWidget } from '@/widgets/current-matches';
import { LobbyWidget } from '@/widgets/lobby';

export function MainPage() {
    const { activeView } = useViewStore();

    return (
        <main className="max-w-7xl mx-auto px-6 py-8 pr-[22rem]">
            {activeView === 'leaderboard' && <LeaderboardWidget />}
            {activeView === 'matches' && <CurrentMatchesWidget />}
            {activeView === 'lobby' && <LobbyWidget />}
        </main>
    );
}