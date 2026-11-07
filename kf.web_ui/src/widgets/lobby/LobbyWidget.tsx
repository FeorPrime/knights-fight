import { useQuery } from '@tanstack/react-query';
import { ErrorBoundary } from 'react-error-boundary';
import { lobbyApi, type LobbyPlayer } from '@/entities/lobby';
import { cn } from '@/shared/lib/cn';

const QUERY_KEY = ['lobby'] as const;
const REFRESH_INTERVAL = 3_000;

export function LobbyWidget() {
    return (
        <ErrorBoundary fallback={<LobbyError />}>
            <LobbyWidgetInner />
        </ErrorBoundary>
    );
}

function LobbyWidgetInner() {
    const { data, isLoading, isError, refetch, isFetching, dataUpdatedAt } = useQuery({
        queryKey: QUERY_KEY,
        queryFn: () => lobbyApi.getStatus(),
        staleTime: 2_000,
        refetchInterval: REFRESH_INTERVAL,
        refetchIntervalInBackground: false,
    });

    const players = data?.players ?? [];
    const readyCount = players.filter((p) => p.isReady).length;
    const unreadyCount = players.filter((p) => !p.isReady).length;

    return (
        <section className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            {/* Header */}
            <header className="px-6 py-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-4">
                <div>
                    <div className="flex items-center gap-2">
                        <span className="text-xl">🛡️</span>
                        <h2 className="text-lg font-semibold text-slate-900">Fighters Lobby</h2>
                    </div>
                    <p className="text-sm text-slate-500 mt-0.5">
                        Active challengers · auto-removes after 1m inactivity · refreshes every {REFRESH_INTERVAL / 1000}s
                    </p>
                </div>

                <div className="flex items-center gap-4">
                    <LastUpdated at={dataUpdatedAt} />
                    <button
                        onClick={() => refetch()}
                        disabled={isFetching}
                        className="text-sm font-medium text-slate-600 hover:text-slate-900 disabled:opacity-50 transition-colors"
                    >
                        {isFetching ? 'Refreshing…' : 'Refresh'}
                    </button>
                </div>
            </header>

            {/* Metrics cards */}
            <div className="grid grid-cols-3 divide-x divide-slate-100 border-b border-slate-100 bg-slate-50/50">
                <div className="p-4 text-center">
                    <p className="text-xs font-medium text-slate-500 uppercase tracking-wider">Total in Lobby</p>
                    <p className="text-xl font-bold text-slate-900 mt-1 tabular-nums">{players.length}</p>
                </div>
                <div className="p-4 text-center">
                    <p className="text-xs font-medium text-emerald-600 uppercase tracking-wider">Ready</p>
                    <p className="text-xl font-bold text-emerald-600 mt-1 tabular-nums">{readyCount}</p>
                </div>
                <div className="p-4 text-center">
                    <p className="text-xs font-medium text-amber-600 uppercase tracking-wider">Unready</p>
                    <p className="text-xl font-bold text-amber-600 mt-1 tabular-nums">{unreadyCount}</p>
                </div>
            </div>

            {/* Main content */}
            {isLoading && <LobbySkeleton />}
            {isError && <LobbyError onRetry={() => refetch()} />}

            {!isLoading && !isError && players.length === 0 && (
                <div className="p-10 text-center">
                    <div className="text-3xl mb-2">⚔️</div>
                    <p className="text-sm font-medium text-slate-700">No fighters currently in lobby</p>
                    <p className="text-xs text-slate-500 mt-1">
                        Log in via the desktop client or API to appear in the lobby with unready status.
                    </p>
                </div>
            )}

            {!isLoading && !isError && players.length > 0 && (
                <ul className="divide-y divide-slate-100">
                    {players.map((p) => (
                        <LobbyPlayerRow key={p.name} player={p} />
                    ))}
                </ul>
            )}
        </section>
    );
}

function LobbyPlayerRow({ player }: { player: LobbyPlayer }) {
    return (
        <li className="px-6 py-3.5 flex items-center justify-between gap-4 hover:bg-slate-50 transition-colors">
            <div className="flex items-center gap-3 min-w-0">
                {player.avatarUrl ? (
                    <img
                        src={player.avatarUrl}
                        alt=""
                        className="h-9 w-9 rounded-full object-cover shrink-0"
                    />
                ) : (
                    <div className="h-9 w-9 rounded-full bg-gradient-to-br from-slate-700 to-slate-900 text-white flex items-center justify-center text-sm font-bold shrink-0 shadow-sm">
                        {player.name.charAt(0).toUpperCase()}
                    </div>
                )}
                <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-900 truncate">{player.name}</p>
                    <p className="text-xs text-slate-400">
                        Active: {new Date(player.lastActive).toLocaleTimeString()}
                    </p>
                    {player.isReady && player.readySince && (
                        <p className="text-xs text-emerald-600">
                            Ready since {new Date(player.readySince).toLocaleTimeString()}
                        </p>
                    )}
                </div>
            </div>

            <div className="flex items-center gap-3">
                <span
                    className={cn(
                        'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border',
                        player.isReady
                            ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                            : 'bg-amber-50 text-amber-700 border-amber-200',
                    )}
                >
                    <span
                        className={cn(
                            'h-1.5 w-1.5 rounded-full',
                            player.isReady ? 'bg-emerald-500 animate-pulse' : 'bg-amber-500',
                        )}
                    />
                    {player.isReady ? 'Ready' : 'Unready'}
                </span>
            </div>
        </li>
    );
}

function LastUpdated({ at }: { at: number }) {
    if (!at) return null;
    return (
        <span className="text-xs text-slate-400 tabular-nums">
            Updated {new Date(at).toLocaleTimeString()}
        </span>
    );
}

function LobbySkeleton() {
    return (
        <div className="p-6 space-y-3">
            {[1, 2, 3].map((i) => (
                <div key={i} className="flex items-center justify-between animate-pulse">
                    <div className="flex items-center gap-3">
                        <div className="h-9 w-9 bg-slate-200 rounded-full" />
                        <div className="space-y-1.5">
                            <div className="h-3.5 w-24 bg-slate-200 rounded" />
                            <div className="h-2.5 w-16 bg-slate-200 rounded" />
                        </div>
                    </div>
                    <div className="h-6 w-20 bg-slate-200 rounded-full" />
                </div>
            ))}
        </div>
    );
}

function LobbyError({ onRetry }: { onRetry?: () => void } = {}) {
    return (
        <div className="p-8 text-center">
            <p className="text-sm text-rose-600">Couldn't load the lobby status.</p>
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
