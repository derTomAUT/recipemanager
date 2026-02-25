# Import Image Candidates Design

**Date:** 2026-02-25  
**Owner:** Codex

## Goal
When importing a recipe from URL, show all found candidate images on the Edit page and let the user choose the hero image. Candidates are temporary and deleted on navigation if the user doesn’t save.

## Non-Goals
- No long-term storage for unselected candidates.
- No background cleanup jobs (delete on navigation only).
- No UI changes outside the Edit page.

## Architecture
- **Backend import** downloads candidate images and uploads them as temporary files (e.g., `/uploads/temp/...`), returning them in the draft as `candidateImages`.
- **Frontend edit page** displays candidate images (including AI-selected hero preselected), provides “Set as Hero” action.
- **Save flow** sends `selectedImages` (hero + chosen) to backend; backend deletes unselected temp images and keeps selected as `RecipeImage`.
- **Cancel/navigation** calls a cleanup endpoint to delete all temporary images for the draft.

## Data Flow
1. Import URL → extract candidate images → upload temp files → return draft with `candidateImages` and `heroImageUrl`.
2. Edit page shows candidates; user selects hero.
3. Save → backend persists selected images, deletes unselected temp images.
4. Cancel/navigation → frontend calls cleanup to delete temp images.

## Error Handling
- If temp cleanup fails, log and continue (avoid blocking user).
- If candidate upload fails, continue without candidates.

## Testing
- Manual: import with many images; verify candidates show and hero selection updates.
- Manual: cancel editor; verify temp files removed.
