# HelloFresh Paper Card Import Design

## Context
Users should be able to import a HelloFresh paper recipe card from mobile by capturing/uploading two images (front and back). The system should extract recipe metadata, hero image, step images, ingredients for multiple serving sizes, and cooking steps. Before saving, the user must choose which serving scale (2/3/4) to persist.

## Goals
- Support a mobile-first two-image import flow.
- Extract structured recipe data with editable review UX.
- Preserve card imagery (hero + step images).
- Force serving-scale choice before commit.

## Non-Goals
- Full offline OCR on device.
- Arbitrary multi-card batch import.
- Perfect extraction without review.

## Recommended Architecture
Use backend vision extraction with two endpoints:
- `POST /api/import/paper-card/parse` for preprocessing + extraction + draft creation.
- `POST /api/import/paper-card/commit` for saving selected-serving recipe.

The frontend provides a capture/upload wizard and review step before commit. The backend owns document interpretation and confidence scoring so improvements can be shipped centrally.

## End-to-End Workflow
1. User taps `Import from Paper Card`.
2. User captures/uploads `front` image.
3. User captures/uploads `back` image.
4. Frontend sends multipart parse request.
5. Backend preprocesses images (rotate/perspective/denoise) and extracts structured content.
6. Backend returns `PaperCardDraft` including serving variants, images, and confidence hints.
7. User reviews fields, selects serving size (required), edits any mismatches.
8. Frontend commits draft with selected serving.
9. Backend persists recipe + images and returns created recipe id.
10. Frontend routes to recipe detail/editor.

## Data Model (Draft)
- `PaperCardImportDraft`
  - `id`
  - `householdId`
  - `title`
  - `description?`
  - `nutritionJson?`
  - `ingredientsByServings` (map: 2/3/4 -> list of ingredient rows)
  - `steps` (ordered text)
  - `heroImageTempPath?`
  - `stepImageTempPaths[]`
  - `rawExtractedTextFront`
  - `rawExtractedTextBack`
  - `confidenceJson`
  - `createdAtUtc`, `expiresAtUtc`

## API Contracts
### Parse
`POST /api/import/paper-card/parse` (multipart)
- fields: `frontImage`, `backImage`

Response:
- `draftId`
- `title`, `description`, `nutrition`
- `servingsAvailable: number[]` (e.g. `[2,3,4]`)
- `ingredientsByServings`
- `steps`
- `heroImageUrl`
- `stepImageUrls`
- `confidence`
- `warnings[]`

### Commit
`POST /api/import/paper-card/commit`
- `draftId`
- `selectedServings` (required: 2/3/4)
- optional edited values (`title`, `ingredients`, `steps`, `nutrition`)

Response:
- `recipeId`

## UX Notes
- Show camera-or-upload controls for both sides.
- Show parse progress and actionable error states.
- Highlight low-confidence fields.
- Disable save until serving size is chosen.
- Keep manual edit path for all extracted fields.

## Validation & Error Handling
- Reject parse if either side missing.
- Reject unsupported mime/size.
- Commit fails if draft expired or selected serving unavailable.
- Partial parse should still return editable draft when possible.

## Security & Retention
- Restrict draft access to household members.
- Store temporary images with TTL cleanup.
- Log extraction diagnostics without leaking secrets.

## Testing Strategy
- Backend unit/integration tests for parse/commit validations.
- Frontend unit tests for wizard and serving-selection guard.
- Browser e2e for mobile capture/upload style flow with mocked parse response.

## Rollout
- Feature behind a UI flag initially.
- Instrument parse success rate and manual edit rate.
- Iterate extraction prompts/rules from real-world failures.
