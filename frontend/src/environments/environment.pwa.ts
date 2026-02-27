export function resolvePwaApiUrl(loc: { hostname?: string } = globalThis.location ?? {}): string {
  const hostname = loc.hostname ?? '';
  if (hostname === 'localhost' || hostname === '127.0.0.1') {
    return 'http://localhost:5000/api';
  }
  return '/api';
}

export const environment = {
  production: true,
  apiUrl: resolvePwaApiUrl(),
  googleClientId: '812118128928-9gubatmbdtkke4elg4gah5lim0hju3re.apps.googleusercontent.com'
};
