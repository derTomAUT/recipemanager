import { describe, expect, it } from 'vitest';
import { isPwaEnabled, PWA_REGISTRATION_STRATEGY } from './pwa.config';

describe('pwa config', () => {
  it('enables pwa in production', () => {
    expect(isPwaEnabled(true)).toBe(true);
  });

  it('disables pwa outside production', () => {
    expect(isPwaEnabled(false)).toBe(false);
  });

  it('uses a delayed registration strategy', () => {
    expect(PWA_REGISTRATION_STRATEGY).toBe('registerWhenStable:30000');
  });
});
