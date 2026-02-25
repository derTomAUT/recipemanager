
# Claude.md – Recipe Manager Monorepo

## Project Identity

Project: Recipe Manager (multi-user household recipe application)
Frontend: Angular
Backend: ASP.NET Core (.NET)
Database: PostgreSQL
Architecture Style: Simple, deterministic, minimal layers, no over-engineering

Primary Goal:
Generate production-like, readable, maintainable code with predictable structure.
Avoid unnecessary abstractions, frameworks, patterns, or speculative features.

---

## Global Rules

1. NEVER introduce technologies not explicitly listed.
2. NEVER refactor existing architecture unless explicitly instructed.
3. Prefer clarity and explicitness over cleverness.
4. Avoid meta-framework patterns, CQRS, MediatR, DDD layers, etc.
5. Keep dependencies minimal.
6. Code must be understandable by a senior engineer without explanation.

---

## Repository Layout (Monorepo)

Root structure MUST remain:

/frontend     → Angular application
/backend      → ASP.NET Core Web API
/infra        → Docker / deployment / local setup
/docs         → Documentation only

Do not invent new top-level directories.

---

## Backend Constraints (ASP.NET Core)

Framework:
- Latest stable .NET version available
- ASP.NET Core Web API

Required Characteristics:

• Use Controllers (no minimal APIs)
• Use EF Core with PostgreSQL provider
• Use explicit DbContext
• Use simple service classes when logic is non-trivial
• No generic repository pattern
• No mediator pattern
• No event sourcing / domain layers

### Structure

/backend/src/

Controllers/
Services/
Data/
Models/
DTO/
Infrastructure/

### Database

• EF Core Code First
• Use UUID primary keys
• Explicit entity configurations if needed
• No lazy loading proxies

### Entities

Entities represent database tables directly.

Example style:

- Recipe
- RecipeIngredient
- RecipeStep
- RecipeImage
- CookEvent
- VotingRound
- VotingVote
- UserPreference
- Household
- HouseholdMember
- User

No artificial domain separation.

---

## Backend Coding Style

• Constructor injection only
• No static service locators
• No magic frameworks
• Explicit async/await usage
• Avoid hidden behavior

Naming:

• PascalCase for C# types
• camelCase for variables
• Verb-based controller actions

Error handling:

• Use proper HTTP status codes
• Do not swallow exceptions
• Do not introduce complex exception hierarchies

---

## Authentication Rules

• Google authentication only
• Backend validates Google token
• Backend issues its own JWT or cookie session
• All APIs assume authenticated user

Authorization:

• Household-scoped access checks required
• No global data access

---

## Frontend Constraints (Angular)

Framework:
- Standard Angular (no alternative UI frameworks)

Hard Rules:

• Use standalone components OR classic modules, but be consistent
• No state management libraries (no NgRx, Akita, etc.)
• Use services with RxJS only
• No unnecessary abstraction layers

Structure:

/frontend/src/app/

pages/
components/
services/
models/

---

## Frontend Behavior Principles

• Backend is source of truth
• No duplicated business logic
• UI logic only handles presentation + interaction

Components should be:

• Small
• Explicit
• Predictable

Avoid:

• Over-generic UI components
• Complex reactive chains unless necessary

---

## Data Contracts

• Backend DTOs define API contracts
• Frontend models mirror DTOs closely
• No dynamic typing

---

## Recipe Rules

A Recipe consists of:

• Title
• Optional description
• Images (one title image)
• Ingredients (name, quantity, unit, notes)
• Steps (ordered)
• Cuisines / tags

Never collapse ingredients or steps into free-text blobs.

---

## Recommendation Logic

Keep logic deterministic and simple:

Allowed factors:

• Allergens = hard exclusion
• Disliked ingredients = downrank
• Favorite cuisines = uprank
• Cook count = ranking factor

Do NOT implement ML systems or complex scoring engines.

---

## Voting / Game Logic

Rules:

• One active voting round per household
• Max nominations enforced by backend
• One vote per user
• Tie-breaker = least cooked recipe

No speculative features.

---

## Import / AI Rules

AI features are OPTIONAL helpers, not core logic.

Design Principles:

• Always produce editable drafts
• Never auto-persist AI results
• Deterministic fallback required

Allowed pipeline:

• URL fetch → structured parse → draft
• Image upload → OCR → draft

Avoid:

• Hidden background processing
• Autonomous AI behavior

---

## File & Storage Rules

Images:

• Store externally (object storage)
• DB stores URLs only

Never embed binary blobs in database.

---

## Docker / Infra Rules

Provide minimal local development setup:

Required:

• docker-compose for Postgres
• Backend containerizable
• Frontend containerizable or static build

Avoid:

• Kubernetes manifests unless requested
• Complex cloud-specific templates

---

## CI/CD Rules

GitHub Actions only.

Pipeline principles:

• Build frontend
• Build backend
• Run tests if present
• Build Docker image
• Deploy step isolated

Do not add vendor-specific CI tools.

---

## Dependencies Philosophy

Every dependency must have justification.

Avoid:

• Heavy libraries
• Experimental frameworks
• Architecture-driven dependencies

Prefer:

• Built-in platform capabilities

---

## Refactoring Policy

Claude must NOT refactor working code unless:

1. Explicitly requested
2. Fixing a correctness issue
3. Resolving compile/runtime failure

Cosmetic refactors are forbidden.

---

## Code Generation Priorities

When generating code:

1. Correctness
2. Readability
3. Simplicity
4. Explicit behavior

NOT a priority:

• Clever abstractions
• Architectural purity
• Pattern completeness

---

## When Uncertain

If requirements conflict or are ambiguous:

• Choose simplest interpretation
• Avoid adding new concepts
• Prefer explicit over implicit behavior

---

## Explicit Non-Goals

Do NOT introduce:

• CQRS / Mediator patterns
• Event buses
• Domain-driven design layers
• Microservices
• Plugin systems
• Multi-tenant beyond household_id

Unless explicitly requested.

---

## Output Expectations

Generated code must:

• Compile
• Be consistent with existing structure
• Avoid unused scaffolding
• Avoid placeholder architecture

---
