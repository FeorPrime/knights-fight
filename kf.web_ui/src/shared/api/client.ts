import ky from 'ky';

/**
 * Origin of the API for requests that cannot go through the dev proxy.
 *
 * The Vite dev proxy buffers streaming responses, so Server-Sent Events never
 * arrive over `/api/...` in development — they have to hit the API directly.
 * In production the SPA and the API share an origin behind nginx, so this is
 * empty and every request stays same-origin.
 */
export const API_ORIGIN =
    import.meta.env.DEV ? `http://${window.location.hostname}:5000` : '';

export const apiClient = ky.create({
    // ky 2.x renamed `prefixUrl` to `prefix`.
    prefix: '/api',
    timeout: 10_000,
    retry: 2,
    hooks: {
        beforeError: [
            (state) => {
                // ky hands us a BeforeErrorState and expects an Error back,
                // so log the cause and re-throw the original error.
                console.error(`[API] ${state.request.url}`, state.error.message);
                return state.error;
            },
        ],
    },
});
