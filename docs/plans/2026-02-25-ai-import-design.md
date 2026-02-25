# AI URL Import Fallback Design

Date: 2026-02-25

## Goal
Add AI-based URL import fallback using OpenAI or Anthropic when JSON-LD is missing and household AI settings are configured.

## Scope
- Store per-household AI provider, model, and encrypted API key.
- Owner-only Household Settings page to manage AI configuration.
- Fetch available models from the selected provider and block saving until the list loads.
- Import pipeline: JSON-LD first; if missing and AI configured, use AI; otherwise fall back to heuristics.

## Non-Goals
- No server-wide key fallback.
- No draft persistence; drafts remain in-memory only.
- No advanced AI guardrails beyond basic validation.

## Architecture
- Extend `Household` with encrypted AI settings: provider, model, encrypted key.
- Add endpoints:
  - `GET /api/household/settings` (owner-only)
  - `PUT /api/household/settings` (owner-only)
  - `GET /api/ai/providers/models` (owner-only, uses stored key to fetch models)
- Import flow in `RecipeImportService`: JSON-LD parse → if missing and AI configured, call AI → else heuristic.

## Components
- **Backend**
  - Data Protection-based encryption for API key.
  - AI client services for OpenAI and Anthropic.
  - Household settings controller endpoints.
- **Frontend**
  - New Household Settings page (owner-only route).
  - Settings form: provider dropdown, model dropdown (loaded from provider), API key field.
  - Save disabled until models are loaded.

## Data Flow
1. Owner sets provider + API key + model in Household Settings.
2. Import from URL:
   - JSON-LD found → return draft.
   - JSON-LD missing → if AI settings exist, call AI and return draft.
   - If AI not configured → heuristic extraction.

## Error Handling
- Invalid/missing AI settings → skip AI and fall back to heuristics.
- AI request failures → return 502 and include warning in draft.
- API key is write-only; never returned after save.

## Testing Plan
- Backend unit tests for import decision tree (JSON-LD vs AI vs heuristic).
- Backend unit tests for AI client mapping.
- Manual UI verification for settings page and import flow.

## Success Criteria
- Owners can configure AI provider/model/key per household.
- Import uses AI when JSON-LD missing and AI configured.
- Import still works without AI settings (heuristic fallback).
