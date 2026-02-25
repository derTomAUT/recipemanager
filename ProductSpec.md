
1) Product scope and MVP boundaries
Goal

A multi-user family recipe manager with:

household-shared recipes

personalized discovery (avoid dislikes/allergens)

favorites + cooking history

a “meal planner / voting game”

AI-assisted import (URL scrape + photo/card OCR)

MVP (Phase 1)

Google login, households, roles

CRUD recipes (title, images, ingredients, steps, tags)

Recipe overview + search/filter

Favorites, dislikes/allergens, cuisine preferences

Cooking history (record “cooked”)

Voting game (nominate + vote + tie-breaker by “least cooked”)

Import v1:

URL import (manual paste + backend fetch + extraction)

Image/card import (upload → OCR → structured recipe draft)

Post-MVP (Phase 2)

Meal planning calendar (week view)

Shopping list generation + pantry inventory

Nutrition estimates

Sharing/export (PDF, link share)

Offline-friendly PWA

2) Core concepts and rules
Household & access model

A Household is the primary tenant boundary.

Users join exactly one household in MVP (simplifies everything). (Can extend to multi-household later.)

Roles:

Owner: manage household, invite/remove users, full access

Member: CRUD recipes, vote, cook history

Optional Viewer: read-only

Recipe ownership & sharing

All recipes are owned by the household, not by individual users.

A recipe can be marked:

visibility: household (default)

(future) visibility: private if you want personal recipes later

Personalization inputs

Per user:

dislikedIngredients[] (soft avoid)

allergens[] (hard exclude by default)

favoriteCuisines[]

favoriteRecipes[]

Recommendation logic (MVP):

Exclude recipes that contain any allergen-matched ingredient (simple string/category match).

Downrank recipes containing disliked ingredients.

Up-rank favorite cuisines and recipes not cooked recently.



3) Import / AI design (practical + compact)
Guiding principle

Keep the app easy to set up:
“AI mode”: user provides API key (stored encrypted) OR uses server env var for your own
Supported AI Providers: OpenAI (ChatGPT) and Anthropic (Claude)

URL import pipeline (MVP)

Backend fetches HTML (server-side) from provided URL

Try structured extraction:

Parse JSON-LD schema.org Recipe if present (many sites have it)

Fallback heuristics:

Look for ingredient lists and instruction blocks by common selectors

Produce a RecipeDraft object

Frontend shows draft editor for confirmation → save

Image/card import pipeline (MVP)

Upload image(s) (HelloFresh card photos)

OCR:

Use AI model

Parse OCR text into sections via rules:

Ingredients section, steps, servings/time

Return RecipeDraft + extracted “food image” (if present; else user uploads separately)

RecipeDraft schema

title

description

servings

prepMinutes / cookMinutes

ingredients[] (name, quantity, unit, notes)

steps[] (instruction, timerSeconds?)

cuisines[], tags[]

images[] (url/tempId)

confidenceScore + warnings[] (e.g., “units missing”)

4) Non-functional requirements
Security

Tenant isolation by household_id in every query.

Role-based authorization.

Upload validation (file type, size limits).

Rate limiting on import endpoints (avoid abuse).

Performance

Recipe list endpoints paginated.

Caching for recommendations (e.g., 1–5 minutes).

Observability

Structured logs (Serilog)

Minimal health endpoint /health

5) Deployment: “compact/free-tier” strategy
Target architecture

Single container for API + Angular static files served by API (simplest).

Postgres via free-tier managed DB (or Neon/Supabase) OR docker-compose local.

Storage

Images:
Local filesystem in dev
In prod: S3-compatible (Cloudflare R2 is good) or Supabase storage
Store only URLs in DB.
Hosting candidates (practical)
Fly.io / Render / Railway / Azure Container Apps (free tiers vary)

Keep memory small by:
disabling heavy background workers

6) Repo + CI/CD (GitHub: derTomAUT)
Repo structure (monorepo)
/frontend   (Angular)
/backend    (ASP.NET Core)
/infra      (docker-compose, deployment manifests)
/docs       (architecture, API, ADRs)
CI pipeline (GitHub Actions)

On PR: build + test frontend + backend
On main: build container image + deploy

Suggested:
Docker build with multi-stage

Deploy via:
Fly.io action OR
Render deploy hook OR
Railway deploy

Also include:
EF Core migrations applied on deploy (careful with prod; run as step)

7) Implementation plan (Epics → stories)
Epic A: Auth + Household
Google login flow (Angular + backend token exchange)
Create household + join via invite
RBAC checks and middleware

Epic B: Recipe CRUD
Recipe editor (ingredients list + steps builder)
Image upload + title image selection
Detail page + delete confirmation

Epic C: Discovery + Search
Recipe list endpoint with filtering/sorting
Recommendation endpoint (exclude allergens, downrank dislikes)
Welcome page cards

Epic D: Favorites + Preferences
Preferences UI (chips + autocomplete)
Favorite toggle + “Favorites” filter

Epic E: Cooking history
“Mark cooked” + list history
Aggregations: cook count, last cooked

Epic F: Voting Game
Create round, nominate up to 4 recipes total
Voting UI + close round + tie-breaker logic

Epic G: Import
URL import (JSON-LD first)
Image import (upload → OCR → draft)
Draft review → save as recipe

8) Concrete acceptance criteria (examples)

Recipe recommendation
Given allergens list contains “peanut”
When requesting recommendations with “exclude allergens” enabled
Then no recipe whose ingredient names match “peanut” (case-insensitive substring) is returned
Voting tie-break
Given round ends with tie between R1 and R2
And household cookedCount(R1)=3, cookedCount(R2)=1
Then system selects R2

Nomination limit
Given 4 nominations already exist
When a user tries to nominate a 5th recipe
Then API returns 409 with message “Nomination limit reached (4)”

9) Open decisions (pick defaults now to avoid rework)
To keep Claude Code moving, adopt these defaults:
Single-household-per-user in MVP
Use Postgres + EF Core
Use R2 (or any S3-compatible) for images; local storage fallback
AI import is optional; start with JSON-LD + OCR + manual review
