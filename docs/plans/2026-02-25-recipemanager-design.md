# Recipe Manager — MVP Design

Date: 2026-02-25
Scope: MVP Phase 1 (Epics A–F). Import (Epic G) deferred to Phase 2.

---

## High-Level Architecture

**Runtime topology:**
- Single container serving Angular SPA (static files via wwwroot) + ASP.NET Core API
- PostgreSQL: Docker Compose locally; managed free-tier (Neon / Supabase / Railway) in prod
- Images: `IStorageService` abstraction — `LocalFileStorageService` in dev, `S3StorageService` in prod (swapped via config)
- Auth: Angular → Google Sign-In → sends `id_token` to backend → backend validates with Google, issues app JWT → Angular stores JWT, attaches as `Authorization: Bearer`

**Tech stack:**
- Frontend: Angular 17+ standalone components, RxJS services, no state management library
- Backend: ASP.NET Core Web API, controllers, EF Core + Npgsql, UUID PKs, no lazy loading
- Tests: xUnit + Moq, service layer only
- CI/CD: GitHub Actions — PR: build + test; main: Docker build + deploy

---

## Build Phases

| Phase | Epics | Deliverable |
|---|---|---|
| 0 — Foundation | — | Monorepo scaffold, Docker, CI/CD, DB schema, auth skeleton |
| 1 — Core recipes | A, B | Login, households, full recipe CRUD |
| 2 — Discovery | C, D | Search/filter, recommendations, preferences |
| 3 — Social | E, F | Cooking history, voting game |

---

## Phase 0 — Foundation

**Monorepo layout:**
```
/frontend         Angular 17+ standalone app
/backend          ASP.NET Core Web API
/infra            docker-compose.yml, Dockerfile
/docs             ProductSpec.md, architecture, plans
```

**Backend scaffold:**
- `Program.cs` — EF Core, JWT auth, CORS, Swagger, Serilog, `/health`
- `Data/AppDbContext.cs` — explicit DbSets, no lazy loading
- `Infrastructure/Storage/IStorageService.cs` + `LocalFileStorageService.cs`
- `Infrastructure/Auth/GoogleTokenValidator.cs`
- Initial EF Core migration — all tables, UUID PKs, `household_id` on all tenant-scoped tables
- `appsettings.json` / `appsettings.Development.json`

**Entities (all defined in foundation):**
`User`, `Household`, `HouseholdMember`, `Recipe`, `RecipeIngredient`, `RecipeStep`, `RecipeImage`, `CookEvent`, `VotingRound`, `VotingVote`, `UserPreference`

**Frontend scaffold:**
- Standalone `AppComponent`, shell layout (nav + router outlet)
- `app.routes.ts` with lazy-loaded route groups
- `AuthService`, JWT HTTP interceptor, `AuthGuard`
- `environment.ts` / `environment.prod.ts`
- Global HTTP error handler (401 → login, 403 → error page)

**Infra:**
- `docker-compose.yml` — Postgres + pgAdmin
- `Dockerfile` — multi-stage: build Angular → build .NET → copy dist into wwwroot
- `.github/workflows/ci.yml` — PR: restore/build/test; main: Docker build + deploy hook

---

## Phase 1 — Epic A: Auth + Household

**Backend endpoints:**
- `POST /api/auth/google` — validate Google `id_token`, upsert User, return app JWT
- `POST /api/households` — create household, assign caller as Owner
- `POST /api/households/join` — join via invite code
- `GET /api/households/me` — current user's household + role
- `DELETE /api/households/members/{userId}` — Owner removes member
- `HouseholdAccessFilter` middleware — extracts `household_id` from JWT, applied to all scoped routes

**Frontend:**
- `/login` — Google Sign-In button → calls `/api/auth/google` → stores JWT → redirect
- `/household/setup` — create or join household (shown if user has no household)
- `AuthService` — current user + household observables
- Guards: unauthenticated → `/login`; no household → `/household/setup`

---

## Phase 1 — Epic B: Recipe CRUD

**Backend endpoints:**
- `GET /api/recipes` — paginated, household-scoped; query: `search`, `tags`, `cuisines`
- `GET /api/recipes/{id}` — full detail with ingredients, steps, images, cook count, last cooked
- `POST /api/recipes` — create with all sub-entities in one request
- `PUT /api/recipes/{id}` — full replace
- `DELETE /api/recipes/{id}` — Member deletes own; Owner deletes any
- `POST /api/recipes/{id}/images` — upload → `IStorageService` → store URL
- `PATCH /api/recipes/{id}/title-image` — set title image

**Frontend:**
- `/recipes` — list with search bar, tag chips, cuisine filter
- `/recipes/{id}` — detail: ingredients table, steps list, image gallery
- `/recipes/new` and `/recipes/{id}/edit` — editor: dynamic ingredient rows, drag-reorder steps, image upload
- `RecipeService`

**Unit tests:** `RecipeService` — household isolation, ingredient/step ordering

---

## Phase 2 — Epic D: Favorites + Preferences

*Implemented before Epic C — recommendations depend on preference data.*

**Backend endpoints:**
- `GET /api/preferences` — current user's preferences
- `PUT /api/preferences` — replace allergens, disliked ingredients, favorite cuisines
- `POST /api/recipes/{id}/favorite` / `DELETE /api/recipes/{id}/favorite`
- `GET /api/recipes/favorites`

**Frontend:**
- `/profile/preferences` — chips inputs: allergens (red), disliked ingredients (yellow), favorite cuisines (blue)
- Favorite toggle on recipe detail + list card
- `PreferencesService`

---

## Phase 2 — Epic C: Discovery + Search

**Backend endpoints:**
- `GET /api/recipes` extended — scoring added to existing list endpoint:
  - Hard exclude: recipes with allergen-matched ingredients (case-insensitive substring)
  - Downrank: recipes with disliked ingredients
  - Uprank: favorite cuisines + not recently cooked
  - Scoring computed in service layer (deterministic, not in SQL)
- `GET /api/recipes/recommended` — top-N scored recipes, cached 5 min per user via `IMemoryCache`

**Frontend:**
- Home page — recommended recipe cards
- Sort options on `/recipes`: Recommended, Newest, Most Cooked, Alphabetical
- Filter sidebar: tags, cuisines, "exclude my allergens" toggle (default on)
- `DiscoveryService`

**Unit tests:** `RecommendationService` — allergen exclusion, dislike downranking, cook-count tie-breaking

---

## Phase 3 — Epic E: Cooking History

**Backend endpoints:**
- `POST /api/recipes/{id}/cook` — record CookEvent (caller, timestamp, optional servings)
- `GET /api/recipes/{id}/cook-history` — cook events for a recipe
- `GET /api/cook-history` — household feed, paginated, optional date range

**Frontend:**
- "Mark as Cooked" button on recipe detail → confirmation modal (optional servings)
- Cook history tab on recipe detail
- `/cook-history` — household feed
- `CookHistoryService`

---

## Phase 3 — Epic F: Voting Game

**Backend endpoints:**
- `POST /api/voting/rounds` — create round (Owner only; 409 if active round exists)
- `GET /api/voting/rounds/active` — active round with nominations + vote counts
- `POST /api/voting/rounds/{id}/nominations` — nominate recipe (max 4 total; 409 on 5th)
- `DELETE /api/voting/rounds/{id}/nominations/{recipeId}` — withdraw own nomination
- `POST /api/voting/rounds/{id}/votes` — cast vote (one per user; 409 if already voted)
- `POST /api/voting/rounds/{id}/close` — Owner closes; tie-breaker = least cooked; stores `winnerId`
- `GET /api/voting/rounds` — history with winners

**Frontend:**
- `/voting` — active round card: nominees, vote counts, Vote button, Nominate picker
- Nominate flow: search/select from household recipes
- Close round (Owner only) → winner reveal
- Past rounds history
- `VotingService`

**Unit tests:** `VotingService` — nomination limit (409 on 5th), tie-breaker (least cooked wins), single-vote enforcement, no-active-round guard

---

## Mobile UI

The app must be mobile-friendly throughout. No separate "cooking mode" — the standard recipe detail page is the cooking view.

**Global principles:**
- Responsive layout via CSS flexbox/grid; no third-party UI framework
- Minimum touch target size: 44×44px for all interactive elements
- Font sizes readable at arm's length (body min 16px, step text 18px+)
- No hover-only interactions; all actions accessible by tap

**Recipe detail page (priority):**
- Single-column layout on mobile
- Ingredients rendered as a clean checklist (visually, not necessarily interactive checkboxes in MVP)
- Steps rendered as large numbered blocks with generous padding
- Sticky header showing recipe title + "Mark as Cooked" button

**Recipe list / home page:**
- Card grid: 1 column on mobile, 2+ on tablet/desktop
- Search bar and filters collapse into a toggleable panel on mobile

**Forms (recipe editor, preferences):**
- Full-width inputs on mobile
- Ingredient rows stack vertically on small screens

---

## Security

- All data queries scoped by `household_id` from JWT claims
- Role checks: Owner-only actions enforced in service layer
- Image upload: file type + size validation before `IStorageService`
- Rate limiting on import endpoints (Phase 2)

## Non-functional

- Recipe list endpoints paginated (default page size: 20)
- Recommendation cache: `IMemoryCache`, 5 min TTL per user
- Structured logging: Serilog
- Health endpoint: `GET /health`
