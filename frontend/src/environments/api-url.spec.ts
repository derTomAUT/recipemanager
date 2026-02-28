import { describe, expect, it } from 'vitest';
import { resolveApiUrl } from './api-url';

describe('resolveApiUrl', () => {
  it('uses localhost backend for local development hosts', () => {
    expect(resolveApiUrl({ hostname: 'localhost' })).toBe('http://localhost:5000/api');
    expect(resolveApiUrl({ hostname: '127.0.0.1' })).toBe('http://localhost:5000/api');
    expect(resolveApiUrl({ hostname: '::1' })).toBe('http://localhost:5000/api');
  });

  it('uses relative api path for non-local hosts', () => {
    expect(resolveApiUrl({ hostname: 'kaia5.tzis.net' })).toBe('/api');
    expect(resolveApiUrl({ hostname: '192.168.1.100' })).toBe('/api');
  });
});
