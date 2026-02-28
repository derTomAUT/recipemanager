export function resolveApiUrl(loc: { hostname?: string } = globalThis.location ?? {}): string {
  const hostname = (loc.hostname ?? '').toLowerCase();
  if (hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1') {
    return 'http://localhost:5000/api';
  }

  return '/api';
}
