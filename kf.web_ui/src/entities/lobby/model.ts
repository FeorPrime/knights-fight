export type LobbyPlayer = {
    name: string;
    isReady: boolean;
    status: 'ready' | 'unready' | string;
    lastActive: string;
    readySince: string | null;
    avatarUrl?: string | null;
};

export type LobbyStatus = {
    waitingCount: number;
    queue: string[];
    players: LobbyPlayer[];
};

export type LobbyJoinRequest = {
    challengerName: string;
    isReady?: boolean;
};

export type LobbyJoinResponse = {
    success: boolean;
    message: string;
    matchId?: string | null;
    opponent?: string | null;
};
