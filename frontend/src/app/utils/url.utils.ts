import { environment } from '../../environments/environment';

export function resolveImageUrl(url?: string): string | undefined {
  if (!url) return undefined;
  if (url.startsWith('http://') || url.startsWith('https://')) return url;
  if (url.startsWith('/')) {
    const base = environment.apiUrl.replace(/\/api\/?$/, '');
    return `${base}${url}`;
  }
  return url;
}
