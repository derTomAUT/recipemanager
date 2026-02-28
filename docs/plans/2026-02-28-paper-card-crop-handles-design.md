# Paper Card Crop Handles Design

## Summary
Mobile drag-to-select on the image is unreliable because touch contact can be lost during drag. Replace freeform drag selection with a persistent crop rectangle that is adjusted by dragging edge and corner handles.

## Goals
- Keep crop editing directly on the image.
- Remove dependency on drawing a selection by dragging across the image.
- Support four edge handles and four corner handles.
- Apply the same interaction to upload front/back images and parsed image editors.

## Non-Goals
- No slider controls.
- No separate advanced/basic crop mode.
- No changes to parse/apply backend API contracts.

## Interaction Model
- The crop rectangle remains visible at all times.
- Users can drag only handles, not the image itself.
- Edge handles move one side: `n`, `e`, `s`, `w`.
- Corner handles move two sides: `ne`, `se`, `sw`, `nw`.
- Opposite sides remain fixed while the active side(s) move.
- Crop bounds remain inside image bounds (`0..100%`).
- Minimum crop size is enforced to prevent collapse.

## UI Changes
- Add 8 draggable handle elements to each crop overlay.
- Increase handle hit area for touch usability.
- Keep existing rotate/reset/apply actions unchanged.

## Data and Logic
- Store drag session state at pointer-down:
  - active handle
  - start pointer point
  - start crop rectangle
  - target editor (front/back/parsed index)
- On pointer-move:
  - convert pointer delta to percent of preview size
  - apply deltas only to sides controlled by active handle
  - clamp and enforce minimum size
- On pointer-up:
  - finalize crop and clear drag session

## Testing Strategy
- Add unit tests for handle-drag crop math (edge movement, corner movement, clamping, min-size guard).
- Verify component still builds and preserves existing rotate/crop output behavior.
