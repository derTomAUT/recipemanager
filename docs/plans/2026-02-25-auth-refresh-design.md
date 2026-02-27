# Auth Refresh Design

**Date:** 2026-02-25  
**Owner:** Codex

## Goal
Proactively refresh JWTs before expiry so the frontend doesn’t show “authenticated” while the backend returns 401.

## Approach
Use an interceptor‑based refresh with a 5‑minute buffer.

## Architecture
- Add `AuthService` helpers: `getTokenExpiry()` and `refreshTokenIfNeeded(bufferSeconds = 300)`.
- In `auth.interceptor`, if a token is near expiry, refresh before sending the request.
- Use a shared in‑flight refresh observable to prevent multiple concurrent refresh calls.
- On refresh failure, log out and redirect to login.

## Error Handling
- If refresh fails, clear auth and navigate to `/login`.
- If refresh succeeds, proceed with the original request.

## Testing
- Manual: set short expiry, verify refresh happens before expiry and requests succeed.
