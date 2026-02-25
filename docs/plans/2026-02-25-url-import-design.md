# URL Import for Recipes Design

Date: 2026-02-25

## Goal
Implement URL-based recipe import (manual paste) that fetches and extracts a recipe draft on the backend, then opens the Recipe Editor pre-filled for user review and save.

## Scope
- Add backend endpoint to import from URL and return a RecipeDraft.
- Add frontend import entry point on Recipe List page.
- Pass draft data into Recipe Editor for pre-fill.
- Surface import errors and draft warnings.

## Non-Goals
- Persist drafts server-side (no draft IDs or storage).
- Full AI/LLM extraction; JSON-LD + heuristic fallback only.
- Automatic recipe creation without user review.

## Architecture
- **Backend:** `POST /api/recipes/import/url` accepts `{ url }`, fetches HTML server-side, parses JSON-LD `schema.org/Recipe` if available, otherwise applies heuristics to find ingredients/steps. Returns `RecipeDraft`.
- **Frontend:** Recipe List page contains “Import from URL” flow (input + button). On success, navigate to `/recipes/new` and pre-fill the editor with the returned draft (via navigation state or a shared service).

## Components
- **Backend import service**: fetch + parse URL to `RecipeDraft`.
- **Import controller endpoint**: validates URL, returns draft or error.
- **RecipeImportService (frontend)**: posts URL and returns draft.
- **Recipe List page**: import UI + error display.
- **Recipe Editor**: accepts draft input and maps to editor fields.

## Data Flow
1. User pastes URL → clicks Import
2. Frontend posts URL to backend import endpoint
3. Backend returns `RecipeDraft`
4. Frontend opens editor with draft pre-filled
5. User edits → saves via existing create flow

## Error Handling
- Backend returns structured error for invalid URL or extraction failure.
- Frontend shows inline error on the import UI.
- Draft warnings (if present) are displayed in editor.

## Testing Plan
- Backend: unit tests for JSON-LD parsing and fallback heuristic.
- Frontend: manual verification of import flow + draft pre-fill.

## Success Criteria
- URL import produces a draft for typical recipe pages with JSON-LD.
- Draft is shown in editor without manual re-entry.
- Errors are surfaced clearly when import fails.
