# Household Members Disable + Invite Link Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add owner-facing household member management (disable/enable) and invite-link generation/copy in household settings.

**Architecture:** Extend household membership with an `IsActive` flag and enforce active membership in household-gated backend endpoints. Expose owner-only disable/enable endpoints and surface member status + invite link UI in Angular household settings.

**Tech Stack:** ASP.NET Core 10 + EF Core/PostgreSQL, Angular 21 + Vitest.

---

### Task 1: Backend Contract + Data Model

**Files:**
- Modify: `backend/src/RecipeManager.Api/Models/Household.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/HouseholdDtos.cs`
- Create: `backend/src/RecipeManager.Api/Migrations/*_AddHouseholdMemberIsActive.cs`

**Steps:**
1. Add failing backend tests asserting inactive members are denied household access.
2. Add `IsActive` to `HouseholdMember` with default `true`.
3. Extend `MemberDto` with `isActive`.
4. Add EF migration for `HouseholdMembers.IsActive`.
5. Re-run tests.

### Task 2: Backend API + Authz Enforcement

**Files:**
- Modify: `backend/src/RecipeManager.Api/Controllers/HouseholdController.cs`
- Modify: household-gated controllers where membership is checked

**Steps:**
1. Add failing tests for owner disable/enable endpoints and forbidden self-disable.
2. Implement `POST /api/household/members/{targetUserId}/disable`.
3. Implement `POST /api/household/members/{targetUserId}/enable`.
4. Ensure membership checks require active membership.
5. Re-run backend tests.

### Task 3: Frontend Service + Settings UI

**Files:**
- Modify: `frontend/src/app/services/household-settings.service.ts`
- Modify: `frontend/src/app/pages/household-settings/household-settings.component.ts`

**Steps:**
1. Add failing frontend tests for invite link generation and member action buttons.
2. Add service methods: get household, disable member, enable member.
3. Render members list with status + actions.
4. Add invite link + copy button behavior.
5. Re-run frontend tests.

### Task 4: Verification

**Steps:**
1. Run targeted frontend vitest specs.
2. Run backend tests in Release.
3. Run frontend PWA build.
4. Review git diff for only intended files.

