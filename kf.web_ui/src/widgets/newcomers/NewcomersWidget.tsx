import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { ErrorBoundary } from 'react-error-boundary';
import { challengerApi, type Challenger, type ChallengersPage } from '@/entities/challenger';
import { API_ORIGIN } from '@/shared/api/client';
import { createSSE } from '@/shared/lib/sse';
import { MAX_VISIBLE, filterNewcomers } from './filter';
import { NewcomersList } from './NewcomersList';
import { NewcomersSkeleton } from './NewcomersSkeleton';

const QUERY_KEY = ['challengers', 'newcomers'] as const;

export function NewcomersWidget() {
    return (
        <ErrorBoundary fallback={<NewcomersError />}>
            <NewcomersWidgetInner />
        </ErrorBoundary>
    );
}

function NewcomersWidgetInner() {
    const queryClient = useQueryClient();

    const { data, isLoading, isError } = useQuery({
        queryKey: QUERY_KEY,
        queryFn: () => challengerApi.list(0, MAX_VISIBLE),
        refetchInterval: 5_000, // fallback polling if SSE dies
        staleTime: 60_000,
    });

    // SSE subscription — merges real-time arrivals into the cache
    useEffect(() => {
        const unsubscribe = createSSE<Challenger>(
            `${API_ORIGIN}/api/challengers/stream`,
            (newcomer) => {
                queryClient.setQueryData<ChallengersPage>(QUERY_KEY, (old) => {
                    if (!old) return old;
                    const exists = old.items.some((c) => c.id === newcomer.id);
                    if (exists) return old;
                    const merged = [newcomer, ...old.items];
                    return {
                        ...old,
                        items: filterNewcomers(merged),
                        totalCount: old.totalCount + 1,
                    };
                });
            },
        );
        return unsubscribe;
    }, [queryClient]);

    if (isLoading) return <NewcomersSkeleton />;
    if (isError || !data) return <NewcomersError />;

    const visibleItems = filterNewcomers(data.items);

    return (
        <aside
            className="fixed top-24 right-4 w-80 bg-white rounded-xl shadow-lg border border-slate-200 overflow-hidden z-50"
            aria-label="Newcomers"
        >
            <header className="px-4 py-3 border-b border-slate-200 bg-slate-50">
                <h2 className="text-sm font-semibold text-slate-700 flex items-center gap-2">
          <span className="relative flex h-2 w-2">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500" />
          </span>
                    Newcomers
                </h2>
            </header>
            <NewcomersList items={visibleItems} />
        </aside>
    );
}

function NewcomersError() {
    return (
        <aside className="fixed top-24 right-4 w-80 bg-white rounded-xl shadow-lg border border-rose-200 p-4 z-50">
            <p className="text-sm text-rose-600">Couldn't load newcomers.</p>
        </aside>
    );
}
