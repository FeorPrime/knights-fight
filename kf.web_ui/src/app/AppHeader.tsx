import { TabBar } from '@/shared/ui/TabBar';
import { useViewStore, VIEW_LABELS, type ViewKey } from './model/viewStore';

const TABS = (Object.keys(VIEW_LABELS) as ViewKey[]).map((key) => ({
    key,
    label: VIEW_LABELS[key],
}));

export function AppHeader() {
    const { activeView, setActiveView } = useViewStore();

    return (
        <header className="sticky top-0 z-40 bg-white/80 backdrop-blur border-b border-slate-200">
            <div className="max-w-7xl mx-auto px-6 py-3 flex items-center justify-between gap-6">
                <div className="flex items-center gap-3">
                    {/* Brand mark */}
                    <div className="h-9 w-9 rounded-lg bg-gradient-to-br from-amber-500 to-rose-600 flex items-center justify-center text-white font-bold shadow-sm">
                        ⚔
                    </div>
                    <h1 className="text-lg font-bold text-slate-900 tracking-tight">
                        Knights Fights
                    </h1>
                </div>

                <TabBar<ViewKey>
                    tabs={TABS}
                    activeKey={activeView}
                    onChange={setActiveView}
                />

                {/* Right side — reserved for user menu / notifications later */}
                <div className="w-24" />
            </div>
        </header>
    );
}