export function buildHouseholdInviteLink(inviteCode: string, origin: string): string {
  if (!inviteCode) return '';
  return `${origin}/household/setup?invite=${encodeURIComponent(inviteCode)}`;
}

export function getApiErrorMessage(error: unknown, fallback: string): string {
  const maybeError = error as { error?: unknown } | null;
  if (!maybeError?.error) {
    return fallback;
  }

  if (typeof maybeError.error === 'string') {
    return maybeError.error;
  }

  const apiPayload = maybeError.error as { error?: string; title?: string } | null;
  if (apiPayload?.error && apiPayload.error.trim()) {
    return apiPayload.error;
  }
  if (apiPayload?.title && apiPayload.title.trim()) {
    return apiPayload.title;
  }

  return fallback;
}
