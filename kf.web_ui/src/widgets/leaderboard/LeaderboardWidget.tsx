import { useQuery } from '@tanstack/react-query';
import { ErrorBoundary } from 'react-error-boundary';
import { rankedChallengerApi } from '@/entities/ranked-challenger';
import { LeaderboardTable } from './LeaderboardTable';
import { LeaderboardSkeleton } from './LeaderboardSkeleton';

const QUERY_KEY = ['leaderboard'] as const;
const REFRESH_INTERVAL = 5_000;

export function LeaderboardWidget() {
    return (
        <ErrorBoundary fallback={<LeaderboardError />}>
            <LeaderboardWidgetInner />
        </ErrorBoundary>
    );
}

function LeaderboardWidgetInner() {
    const { data, isLoading, isError, refetch, isFetching, dataUpdatedAt } = useQuery({
        queryKey: QUERY_KEY,
        queryFn: () => rankedChallengerApi.getLeaderboard(20),
        staleTime: 15_000,
        // Auto-refresh the board.
        refetchInterval: REFRESH_INTERVAL,
        // Don't poll a tab the user isn't looking at.
        refetchIntervalInBackground: false,
    });

    return (
        <section className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <header className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
                <div>
                    <h2 className="text-lg font-semibold text-slate-900">Top Fighters</h2>
                    <p className="text-sm text-slate-500">
                        Ranked by points · refreshes every {REFRESH_INTERVAL / 1000}s
                    </p>
                </div>
                <div className="flex items-center gap-4">
                    <LastUpdated at={dataUpdatedAt} />
                    <button
                        onClick={() => refetch()}
                        disabled={isFetching}
                        className="text-sm text-slate-600 hover:text-slate-900 disabled:opacity-50"
                    >
                        {isFetching ? 'Refreshing…' : 'Refresh'}
                    </button>
                </div>
            </header>

            {isLoading && <LeaderboardSkeleton />}
            {isError && <LeaderboardError onRetry={() => refetch()} />}
            {data && <LeaderboardTable items={data} />}
        </section>
    );
}

/** Shows when the board was last refreshed — the clock ticks on every auto-refresh. */
function LastUpdated({ at }: { at: number }) {
    if (!at) return null;

    return (
        <span className="text-xs text-slate-400 tabular-nums">
            Updated {new Date(at).toLocaleTimeString()}
        </span>
    );
}

function LeaderboardError({ onRetry }: { onRetry?: () => void } = {}) {
    return (
        <div className="p-8 text-center">
            <p className="text-sm text-rose-600">Couldn't load the leaderboard.</p>
            {onRetry && (
                <button
                    onClick={onRetry}
                    className="mt-3 text-sm font-medium text-slate-700 hover:text-slate-900 underline"
                >
                    Try again
                </button>
            )}
        </div>
    );
}
