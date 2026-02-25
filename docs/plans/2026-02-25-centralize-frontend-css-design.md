# Centralize Frontend CSS Design

Date: 2026-02-25

## Goal
Consolidate all inline component CSS into a single global stylesheet and deduplicate identical CSS declarations across pages by introducing shared utility/component classes.

## Scope
- Move all Angular inline `styles: [\`...\`]` blocks into `frontend/src/styles.css`.
- Add page-level wrapper classes in templates for scoping page-specific styles.
- Deduplicate identical declarations even when they were scoped to different pages by creating shared classes.

## Non-Goals
- No visual redesign or new UI features.
- No behavioral changes to components beyond class additions.
- No automated test additions.

## Approach
1. **Centralize CSS**
   - Remove `styles: [\`...\`]` from component metadata.
   - Copy their CSS into `frontend/src/styles.css`.

2. **Page Scoping**
   - Add a unique wrapper class to each page template (e.g., `page-home`, `page-login`).
   - Keep page-specific rules scoped under these wrappers.

3. **Deduplication**
   - Identify identical declarations across pages.
   - Extract shared utilities or component classes (e.g., `.card`, `.btn`, `.section-title`, `.form-row`).
   - Update templates to use shared classes and remove redundant page-scoped rules.

## Tradeoffs
- **Pros:** Reduced duplication, consistent styling, easier maintenance.
- **Cons:** Requires template class updates; risk of overly generic utilities if not narrowly named.

## Risks & Mitigations
- **Risk:** Unintended styling from shared classes.
  - **Mitigation:** Use narrowly named shared classes; keep page wrappers for overrides.
- **Risk:** Missed dedupe cases.
  - **Mitigation:** Systematic scan of all inline styles during consolidation.

## Testing Plan
- Manual visual check across all pages:
  - Home
  - Login
  - Household setup
  - Preferences
  - Recipe list
  - Recipe detail
  - Recipe editor
  - Voting
  - Cook history

## Success Criteria
- No inline styles remain in component metadata.
- `frontend/src/styles.css` is the single source of styling.
- Duplicate declarations are consolidated into shared classes.
- Visual appearance remains unchanged.
