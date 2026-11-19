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
            {activeView === 'docs' && (
                <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-6">
                    <h2 className="text-lg font-semibold text-slate-900">API Documentation</h2>
                    <p className="text-sm text-slate-500 mt-1">Choose a documentation interface.</p>
                    <div className="flex flex-wrap gap-4 mt-4">
                        <a className="text-sm font-medium text-blue-700 hover:underline" href="/api/swagger">Swagger</a>
                        <a className="text-sm font-medium text-blue-700 hover:underline" href="/api/scalar">Scalar</a>
                    </div>
                </section>
            )}
        </main>
    );
}