import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  { path: 'household/setup', loadComponent: () => import('./pages/household-setup/household-setup.component').then(m => m.HouseholdSetupComponent) },
  { path: 'home', canActivate: [authGuard], loadComponent: () => import('./pages/home/home.component').then(m => m.HomeComponent) },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: 'recipes', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-list/recipe-list.component').then(m => m.RecipeListComponent) },
  { path: 'recipes/new', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-editor/recipe-editor.component').then(m => m.RecipeEditorComponent) },
  { path: 'recipes/:id', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-detail/recipe-detail.component').then(m => m.RecipeDetailComponent) },
  { path: 'recipes/:id/edit', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-editor/recipe-editor.component').then(m => m.RecipeEditorComponent) },
  { path: 'preferences', canActivate: [authGuard], loadComponent: () => import('./pages/preferences/preferences.component').then(m => m.PreferencesComponent) }
];
