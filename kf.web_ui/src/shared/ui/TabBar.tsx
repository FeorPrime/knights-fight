import { type ReactNode } from 'react';
import { cn } from '@/shared/lib/cn'; // tiny classnames helper

type Tab<K extends string> = {
    key: K;
    label: string;
    icon?: ReactNode;
};

type TabBarProps<K extends string> = {
    tabs: Tab<K>[];
    activeKey: K;
    onChange: (key: K) => void;
};

export function TabBar<K extends string>({ tabs, activeKey, onChange }: TabBarProps<K>) {
    return (
        <nav className="flex gap-1" role="tablist">
            {tabs.map((tab) => {
                const isActive = tab.key === activeKey;
                return (
                    <button
                        key={tab.key}
                        role="tab"
                        aria-selected={isActive}
                        onClick={() => onChange(tab.key)}
                        className={cn(
                            'px-4 py-2 text-sm font-medium rounded-lg transition-colors',
                            isActive
                                ? 'bg-slate-900 text-white shadow-sm'
                                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                        )}
                    >
                        {tab.icon && <span className="mr-2 inline-flex">{tab.icon}</span>}
                        {tab.label}
                    </button>
                );
            })}
        </nav>
    );
}