import { apiClient } from '@/shared/api/client';
import type { ChallengersPage } from './model';

export const challengerApi = {
    /** Newest challengers first, paged. */
    list: (page = 0, pageSize = 20) =>
        apiClient
            .get('challengers', { searchParams: { page, page_size: pageSize } })
            .json<ChallengersPage>(),
};
