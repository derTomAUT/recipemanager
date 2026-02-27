export const PWA_REGISTRATION_STRATEGY = 'registerWhenStable:30000';

export function isPwaEnabled(production: boolean): boolean {
  return production;
}
