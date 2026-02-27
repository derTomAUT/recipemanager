import { describe, expect, it } from 'vitest';
import { environment, resolvePwaApiUrl } from './environment.pwa';

describe('environment.pwa', () => {
  it('uses relative api path by default so deployed pwa reaches backend', () => {
    expect(environment.apiUrl).toBe('/api');
  });

  it('uses localhost backend when running on localhost without proxy', () => {
    expect(resolvePwaApiUrl({ hostname: 'localhost' })).toBe('http://localhost:5000/api');
    expect(resolvePwaApiUrl({ hostname: '127.0.0.1' })).toBe('http://localhost:5000/api');
  });
});
