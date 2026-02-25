# UI Theme Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Apply the Culinary Studio system‑adaptive theme (rich/expressive) across the app with consistent typography, colors, spacing, and component styling.

**Architecture:** Centralize design tokens in a global stylesheet with CSS variables for light/dark, import Google Fonts, and update core layout/components (home, lists, editor, modals) to use tokens.

**Tech Stack:** Angular, global CSS, CSS variables, prefers-color-scheme.

---

### Task 1: Add global design tokens and fonts

**Files:**
- Modify: `frontend/src/styles.css`

**Step 1: Add font imports and CSS variables**

```css
@import url('https://fonts.googleapis.com/css2?family=Fraunces:wght@400;600;700&family=General+Sans:wght@400;500;600&display=swap');

:root {
  --font-display: 'Fraunces', serif;
  --font-body: 'General Sans', system-ui, sans-serif;
  --bg: #f7f2ec;
  --surface: #fff8f1;
  --text: #1f1a17;
  --muted: #6f655d;
  --primary: #d9502f;
  --secondary: #6e9f7a;
  --accent: #e7b84b;
  --radius-lg: 20px;
  --radius-md: 12px;
  --shadow: 0 8px 24px rgba(0,0,0,0.12);
}

@media (prefers-color-scheme: dark) {
  :root {
    --bg: #141210;
    --surface: #1c1916;
    --text: #f3ede6;
    --muted: #b5aaa1;
    --primary: #ff6b47;
    --secondary: #7abf8d;
    --accent: #ffc95c;
    --shadow: 0 8px 24px rgba(0,0,0,0.35);
  }
}

body {
  font-family: var(--font-body);
  background: var(--bg);
  color: var(--text);
}
```

**Step 2: Commit**

```bash
git add frontend/src/styles.css
git commit -m "feat: add global theme tokens and fonts"
```

---

### Task 2: Update shared components (buttons, cards, forms)

**Files:**
- Modify: `frontend/src/styles.css`

**Step 1: Add global component styles**

Define `.btn`, `.card`, `.input`, `.chip` with new tokens.

**Step 2: Commit**

```bash
git add frontend/src/styles.css
git commit -m "feat: style shared components with theme tokens"
```

---

### Task 3: Update key pages

**Files:**
- Modify: `frontend/src/app/pages/home/home.component.ts`
- Modify: `frontend/src/app/pages/recipe-list/recipe-list.component.ts`
- Modify: `frontend/src/app/pages/recipe-editor/recipe-editor.component.ts`
- Modify: `frontend/src/app/pages/recipe-detail/recipe-detail.component.ts`

**Step 1: Apply new card/layout styles**

Update templates to use `.card`, `.btn`, `.chip` classes where possible, and reduce inline colors.

**Step 2: Commit**

```bash
git add frontend/src/app/pages/home/home.component.ts frontend/src/app/pages/recipe-list/recipe-list.component.ts frontend/src/app/pages/recipe-editor/recipe-editor.component.ts frontend/src/app/pages/recipe-detail/recipe-detail.component.ts
git commit -m "feat: apply Culinary Studio theme to core pages"
```

---

### Task 4: Verification

**Step 1: Manual**
- Check light/dark in browser.
- Verify mobile layout in responsive mode.

**Step 2: Commit any fixes**

```bash
git add frontend/src/styles.css frontend/src/app/pages/home/home.component.ts
git commit -m "test: verify theme on light/dark and mobile"
```
