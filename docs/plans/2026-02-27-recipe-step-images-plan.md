# Recipe Step Images Layout Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Show step images next to instructions on desktop and as a horizontal carousel on mobile while keeping the hero image section unchanged.

**Architecture:** Add a small mapping helper in the recipe detail page that finds step images by `orderIndex = stepIndex + 1`, then update the template and CSS for responsive layout.

**Tech Stack:** Angular, CSS.

---

### Task 1: Add failing UI test for step image mapping (optional if no test harness)

**Files:**
- Create: `frontend/src/app/pages/recipe-detail/recipe-detail.component.spec.ts`

**Step 1: Write the failing test**

```ts
it('maps step image by orderIndex', () => {
  // create component and verify helper returns image for step 0 when orderIndex=1
});
```

**Step 2: Run test to verify it fails**

Run: `npm test -- --watch=false --include=**/recipe-detail.component.spec.ts`
Expected: FAIL (helper missing)

---

### Task 2: Implement step image mapping + layout

**Files:**
- Modify: `frontend/src/app/pages/recipe-detail/recipe-detail.component.ts`
- Modify: `frontend/src/styles.css`

**Step 1: Add helper to map step images**

```ts
getStepImage(stepIndex: number) {
  return this.recipe?.images.find(img => img.orderIndex === stepIndex + 1);
}
```

**Step 2: Update template**
- Add an image slot per step using the helper.
- Keep hero image section unchanged.

**Step 3: Add responsive styles**
- Desktop: two-column layout for steps.
- Mobile: stack text + horizontal scroll carousel.

**Step 4: Run tests**

Run: `npm test -- --watch=false --include=**/recipe-detail.component.spec.ts`
Expected: PASS

**Step 5: Commit**

```bash
git add frontend/src/app/pages/recipe-detail/recipe-detail.component.ts frontend/src/styles.css frontend/src/app/pages/recipe-detail/recipe-detail.component.spec.ts
git commit -m "feat: align step images with instructions"
```

---

### Task 3: Manual verification

**Step 1: Manual UI check**
- Desktop: step image appears beside text when available.
- Mobile: step image appears in horizontal scroll under text.
- Missing images show only text.

**Step 2: Commit any fixes**

```bash
git add frontend/src/app/pages/recipe-detail/recipe-detail.component.ts frontend/src/styles.css
git commit -m "fix: refine step image layout"
```
