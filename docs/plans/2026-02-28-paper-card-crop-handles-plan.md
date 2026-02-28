# Paper Card Crop Handles Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace fragile drag-to-select crop behavior with a static crop rectangle adjusted via edge/corner handles for mobile reliability.

**Architecture:** Extract crop-handle drag math into a small pure utility, then update `PaperCardImportComponent` to use handle-based pointer interactions for both upload and parsed editors. Keep existing crop persistence and image export paths unchanged.

**Tech Stack:** Angular standalone component, TypeScript, Vitest.

---

### Task 1: Add failing tests for crop handle math

**Files:**
- Create: `frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`
- Test: `frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`

**Step 1: Write the failing test**

Add tests for:
- west edge drag updates `cropX` and `cropWidth`
- east edge drag clamps to max width
- northwest corner updates `cropX`, `cropY`, `cropWidth`, `cropHeight`
- minimum size enforcement when dragging beyond opposite edge

**Step 2: Run test to verify it fails**

Run: `npx vitest run frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`  
Expected: FAIL because utility does not exist yet.

**Step 3: Commit**

```bash
git add frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts
git commit -m "test: add failing tests for crop handle drag math"
```

### Task 2: Implement crop handle utility to satisfy tests

**Files:**
- Create: `frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.ts`
- Modify: `frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`
- Test: `frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`

**Step 1: Write minimal implementation**

Implement:
- `CropHandle` union: `n|e|s|w|ne|se|sw|nw`
- `CropRect` interface with `cropX/cropY/cropWidth/cropHeight`
- `applyCropHandleDrag(startRect, handle, deltaXPercent, deltaYPercent, minSizePercent = 1)`

Clamp behavior:
- bounds in `0..100`
- enforce minimum size on affected dimensions
- preserve opposite edges

**Step 2: Run test to verify it passes**

Run: `npx vitest run frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`  
Expected: PASS.

**Step 3: Commit**

```bash
git add frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.ts frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts
git commit -m "feat: add crop handle drag utility"
```

### Task 3: Wire handle-based crop interaction into paper card import component

**Files:**
- Modify: `frontend/src/app/pages/paper-card-import/paper-card-import.component.ts`
- Test: `frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`

**Step 1: Replace drag-to-select with handle drag state**

Remove pointerdown handlers on canvases and old drag state logic.  
Add handle-based drag session state for:
- upload editors (`front`/`back`)
- parsed editor by index

Track:
- active handle
- start pointer location
- start crop rectangle
- target editor

**Step 2: Add overlay handles in template**

For each crop overlay (front/back/parsed):
- add 8 handle elements (`n,e,s,w,ne,se,sw,nw`)
- bind pointerdown to new handlers
- prevent default to avoid touch scroll conflict while dragging

**Step 3: Apply utility in pointer move/up**

On pointer move:
- convert event to canvas space
- compute percent deltas from preview dimensions
- apply `applyCropHandleDrag(...)`
- write updated crop values to target state

On pointer up:
- perform final update
- clear drag session

**Step 4: Update styles for touch-friendly handles**

Add styles for:
- visible handles
- larger hit area
- directional cursors
- `touch-action: none` on handles

**Step 5: Run focused tests**

Run:
- `npx vitest run frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts`

Expected: PASS.

**Step 6: Commit**

```bash
git add frontend/src/app/pages/paper-card-import/paper-card-import.component.ts frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.ts frontend/src/app/pages/paper-card-import/paper-card-import-crop.utils.spec.ts
git commit -m "feat: switch paper card crop interaction to edge and corner handles"
```
