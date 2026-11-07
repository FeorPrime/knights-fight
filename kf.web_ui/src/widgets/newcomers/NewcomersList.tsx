import type { Challenger } from '@/entities/challenger';

export function NewcomersList({ items }: { items: Challenger[] }) {
    if (items.length === 0) {
        return <p className="p-4 text-sm text-slate-500">No newcomers yet.</p>;
    }

    return (
        <ul className="divide-y divide-slate-100 max-h-96 overflow-y-auto">
            {items.map((c) => (
                <li
                    key={c.id}
                    className="px-4 py-3 flex items-center gap-3 animate-in slide-in-from-right fade-in duration-300"
                >
                    {c.avatarUrl ? (
                        <img
                            src={c.avatarUrl}
                            alt=""
                            className="h-8 w-8 rounded-full object-cover shrink-0"
                        />
                    ) : (
                        <div className="h-8 w-8 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-white text-xs font-bold shrink-0">
                            {c.name.charAt(0).toUpperCase()}
                        </div>
                    )}
                    <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-slate-800 truncate">{c.name}</p>
                        <p className="text-xs text-slate-500">
                            {new Date(c.registeredAt).toLocaleTimeString()}
                        </p>
                    </div>
                </li>
            ))}
        </ul>
    );
}
