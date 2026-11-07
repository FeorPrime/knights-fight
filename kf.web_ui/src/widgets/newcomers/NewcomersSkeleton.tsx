export function NewcomersSkeleton() {
    return (
        <aside className="fixed top-24 right-4 w-80 bg-white rounded-xl shadow-lg border border-slate-200 overflow-hidden z-50">
            <div className="px-4 py-3 border-b border-slate-200 bg-slate-50">
                <div className="h-4 w-24 bg-slate-200 rounded animate-pulse" />
            </div>
            <ul className="divide-y divide-slate-100">
                {Array.from({ length: 3 }).map((_, i) => (
                    <li key={i} className="px-4 py-3 flex items-center gap-3">
                        <div className="h-8 w-8 rounded-full bg-slate-200 animate-pulse" />
                        <div className="flex-1 space-y-2">
                            <div className="h-3 w-24 bg-slate-200 rounded animate-pulse" />
                            <div className="h-2 w-16 bg-slate-100 rounded animate-pulse" />
                        </div>
                    </li>
                ))}
            </ul>
        </aside>
    );
}