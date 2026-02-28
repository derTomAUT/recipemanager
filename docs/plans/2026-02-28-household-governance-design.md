# Household Governance Design

## Scope
- Invite lifecycle with stored invites, 5-day expiry, and regeneration.
- Member governance with explicit owner constraints:
  - cannot disable last active member,
  - cannot disable last active owner.
- Role management endpoint/UI to appoint new owners.
- Owner-facing household activity feed.
- Browser e2e coverage for critical ownership/invite flows.

## Backend model changes
- `Household` gains invite metadata:
  - `InviteCodeCreatedAtUtc`
  - `InviteCodeExpiresAtUtc`
- New `HouseholdInvite` table:
  - tracks generated/regenerated invite code snapshots + actor + expiry + active flag.
- New `HouseholdActivityLog` table:
  - event stream for member role/active changes and invite lifecycle actions.
- Existing `HouseholdMember` keeps `IsActive`; no delete-state.

## Rules
- Join by invite code remains in place.
- Invite code expires exactly 5 days after generation.
- Regenerating invite creates a new code and expires old one.
- Disable member blocked when:
  - target is last active member in household,
  - target is last active owner.
- Owner self-disable blocked with remediation message.
- Role changes are owner-only; only owner can promote/demote.

## API changes
- `GET /api/household/invite` -> current code + expiry info.
- `POST /api/household/invite/regenerate` -> rotates code, returns new invite.
- `GET /api/household/activity` -> paged recent owner-visible events.
- `POST /api/household/members/{id}/role` -> set role (`Owner`/`Member`/`Viewer`).
- Existing disable/enable endpoints preserved and guarded.

## Frontend changes
- Household settings:
  - Invite panel shows code/link, expiry date, regenerate + copy.
  - Members table includes role select and disable/enable.
  - Clear remediation banners on blocked actions.
  - Household activity feed section (recent events).
- Household setup:
  - expired invite join shows explicit message.

## Testing
- Backend xUnit:
  - invite expiry/reject and regenerate path,
  - last-owner disable prevention,
  - activity log write assertions.
- Frontend unit tests:
  - invite expiry formatting/link generation,
  - API calls for role/disable/enable/regenerate.
- Playwright e2e:
  - promote new owner then disable old owner workflow,
  - expired invite rejected, regenerated invite accepted,
  - activity feed shows executed actions.
