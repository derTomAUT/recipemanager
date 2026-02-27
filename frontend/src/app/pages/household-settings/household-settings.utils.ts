export function buildHouseholdInviteLink(inviteCode: string, origin: string): string {
  if (!inviteCode) return '';
  return `${origin}/household/setup?invite=${encodeURIComponent(inviteCode)}`;
}
