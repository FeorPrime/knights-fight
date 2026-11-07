import { apiClient } from '@/shared/api/client';
import type { RankedChallenger } from './model';

/**
 * The API owns the ranking: it scores 3 points per win and 1 per draw, then
 * orders by score, wins and id. The client only renders what it returns.
 */
export const rankedChallengerApi = {
    getLeaderboard: (limit = 20) =>
        apiClient.get('leaderboard', { searchParams: { limit } }).json<RankedChallenger[]>(),
};
