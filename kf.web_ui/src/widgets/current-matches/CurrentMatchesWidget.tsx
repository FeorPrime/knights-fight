import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client';

export type Match = {
    id: string;
    challenger1: string;
    challenger2: string;
    status: string;
    startedAt: string;
};

export function CurrentMatchesWidget() {
    const { data: matches } = useQuery({
        queryKey: ['matches'],
        queryFn: () => apiClient.get('match').json<Match[]>(),
        staleTime: 10_000,
    });

    return (
        <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-8 text-center">
            <div className="text-4xl mb-3">⚔️</div>
            <h2 className="text-lg font-semibold text-slate-900">Current Matches</h2>
            {matches && matches.length > 0 ? (
                <div className="mt-4 grid gap-3">
                    {matches.map((m) => (
                        <div key={m.id} className="p-4 border border-slate-100 rounded-lg flex justify-between items-center">
                            <span className="font-medium text-slate-800">{m.challenger1} vs {m.challenger2}</span>
                            <span className="text-xs px-2 py-1 bg-slate-100 rounded text-slate-600">{m.status}</span>
                        </div>
                    ))}
                </div>
            ) : (
                <p className="text-sm text-slate-500 mt-1">
                    No active matches currently in progress.
                </p>
            )}
        </section>
    );
}