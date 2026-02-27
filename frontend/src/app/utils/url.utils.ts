import { environment } from '../../environments/environment';

function getBackendOrigin(): string {
  return environment.apiUrl.replace(/\/api\/?$/, '');
}

export function resolveImageUrl(url?: string): string | undefined {
  if (!url) return undefined;
  if (/^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?\/uploads\//i.test(url)) {
    const path = url.replace(/^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?/i, '');
    return `${getBackendOrigin()}${path}`;
  }
  if (url.startsWith('http://') || url.startsWith('https://')) return url;
  if (url.startsWith('/uploads/')) {
    return `${getBackendOrigin()}${url}`;
  }
  if (url.startsWith('/')) {
    return `${getBackendOrigin()}${url}`;
  }
  return url;
}
