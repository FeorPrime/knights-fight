import type { Challenger } from '@/entities/challenger';

export const MAX_VISIBLE = 15;
export const NEWCOMER_MAX_AGE_MS = 15 * 60 * 1000; // 15 minutes

export function isNewcomer(registeredAt: string, now: number = Date.now()): boolean {
    const timestamp = new Date(registeredAt).getTime();
    if (Number.isNaN(timestamp)) return false;
    const age = now - timestamp;
    return age <= NEWCOMER_MAX_AGE_MS;
}

export function filterNewcomers(items: Challenger[], now: number = Date.now()): Challenger[] {
    return items.filter((item) => isNewcomer(item.registeredAt, now)).slice(0, MAX_VISIBLE);
}
