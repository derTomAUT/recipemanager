# Logging + Live Log Page Design

**Date:** 2026-02-25
**Owner:** Codex

## Goal
Add file-based backend logging with global exception handling and controller catch logging, plus a live, authenticated log viewer page in the frontend using Server-Sent Events (SSE).

## Non-Goals
- No role-based restriction beyond authentication for the log page.
- No log retention management beyond daily rolling files.
- No structured querying/filtering UI beyond basic display.

## Architecture
**Backend logging**
- Configure Serilog file sink to write daily rolling logs at `backend/logs/recipemanager-.log`.
- Add request logging with `UseSerilogRequestLogging()`.
- Add global exception handling to log any unhandled exceptions.
- Add explicit `ILogger<T>` logging to existing controller `catch` blocks to capture swallowed exceptions.

**Log streaming**
- Add an authenticated SSE endpoint at `GET /api/logs/stream` that tails the current log file and emits new lines.
- Stream log lines to all connected authenticated users.

**Frontend**
- Add a `Logs` page (authenticated users only) that connects via `EventSource` and displays lines live.
- Provide basic controls: pause/resume, clear, and auto-scroll.

## Data Flow
1. Serilog writes log events to file.
2. SSE endpoint tails the active log file and sends new lines as `data:` events.
3. Frontend subscribes with `EventSource` and appends each line to the UI.

## Error Handling
- If the log file is missing or unavailable, the SSE endpoint sends a one-time error event and closes.
- If the SSE stream disconnects, the browser auto-reconnects (EventSource default). UI shows a connection status.
- Authentication failures return `401/403` from the SSE endpoint.

## Security
- Log page and SSE endpoint require authentication.
- No log download or search endpoints added (to limit exposure).

## Testing
- Manual: trigger an error in a controller (e.g., invalid import) and confirm it appears in `backend/logs/recipemanager-YYYYMMDD.log`.
- Manual: open the Logs page, confirm live updates, pause/resume behavior, and auto-scroll.
