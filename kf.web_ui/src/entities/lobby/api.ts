import { apiClient } from '@/shared/api/client';
import type { LobbyJoinRequest, LobbyJoinResponse, LobbyStatus } from './model';

export const lobbyApi = {
    getStatus: () => apiClient.get('lobby/status').json<LobbyStatus>(),
    setReady: (challengerName: string, isReady = true) =>
        apiClient
            .post('lobby', {
                json: { challengerName, isReady } satisfies LobbyJoinRequest,
            })
            .json<LobbyJoinResponse>(),
};
