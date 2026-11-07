export function LeaderboardSkeleton() {
    return (
        <ul className="divide-y divide-slate-100">
            {Array.from({ length: 6 }).map((_, i) => (
                <li key={i} className="px-6 py-3 flex items-center gap-4">
                    <div className="w-8 h-4 bg-slate-200 rounded animate-pulse" />
                    <div className="h-9 w-9 rounded-full bg-slate-200 animate-pulse" />
                    <div className="flex-1 space-y-2">
                        <div className="h-3 w-32 bg-slate-200 rounded animate-pulse" />
                        <div className="h-2 w-16 bg-slate-100 rounded animate-pulse" />
                    </div>
                    <div className="h-4 w-12 bg-slate-200 rounded animate-pulse" />
                </li>
            ))}
        </ul>
    );
}