export type Challenger = {
    id: number;
    name: string;
    registeredAt: string; // ISO
    avatarUrl?: string | null;
};

export type ChallengersPage = {
    items: Challenger[];
    totalCount: number;
    page: number;
    pageSize: number;
};
