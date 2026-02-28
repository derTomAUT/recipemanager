import { resolveApiUrl } from './api-url';

export function resolvePwaApiUrl(loc: { hostname?: string } = globalThis.location ?? {}): string {
  return resolveApiUrl(loc);
}

export const environment = {
  production: true,
  apiUrl: resolvePwaApiUrl(),
  googleClientId: '812118128928-9gubatmbdtkke4elg4gah5lim0hju3re.apps.googleusercontent.com'
};
