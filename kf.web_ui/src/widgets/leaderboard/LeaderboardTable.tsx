import type { RankedChallenger } from '@/entities/ranked-challenger';
import { cn } from '@/shared/lib/cn';

export function LeaderboardTable({ items }: { items: RankedChallenger[] }) {
    if (items.length === 0) {
        return <p className="p-6 text-sm text-slate-500">No fighters ranked yet.</p>;
    }

    return (
        <ul className="divide-y divide-slate-100">
            {items.map((c) => (
                <li
                    key={c.id}
                    className={cn(
                        'px-6 py-3 flex items-center gap-4 hover:bg-slate-50 transition-colors',
                        c.rank <= 3 && 'bg-amber-50/40',
                    )}
                >
                    <RankBadge rank={c.rank} />

                    {c.avatarUrl ? (
                        <img
                            src={c.avatarUrl}
                            alt=""
                            className="h-9 w-9 rounded-full object-cover shrink-0"
                        />
                    ) : (
                        <div className="h-9 w-9 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white text-sm font-bold shrink-0">
                            {c.name.charAt(0).toUpperCase()}
                        </div>
                    )}

                    <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{c.name}</p>
                        <p className="text-xs text-slate-500">
                            {c.wins}W / {c.losses}L
                        </p>
                    </div>

                    <div className="text-right">
                        <p className="text-sm font-semibold text-slate-900 tabular-nums">
                            {c.score.toLocaleString()}
                        </p>
                        <p className="text-xs text-slate-500">pts</p>
                    </div>
                </li>
            ))}
        </ul>
    );
}

function RankBadge({ rank }: { rank: number }) {
    const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null;
    return (
        <div className="w-8 text-center">
            {medal ? (
                <span className="text-lg">{medal}</span>
            ) : (
                <span className="text-sm font-semibold text-slate-400 tabular-nums">
          {rank}
        </span>
            )}
        </div>
    );
}
