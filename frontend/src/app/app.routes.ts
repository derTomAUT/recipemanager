import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  { path: 'household/setup', loadComponent: () => import('./pages/household-setup/household-setup.component').then(m => m.HouseholdSetupComponent) },
  { path: 'household/settings', canActivate: [authGuard], loadComponent: () => import('./pages/household-settings/household-settings.component').then(m => m.HouseholdSettingsComponent) },
  { path: 'import/paper-card', canActivate: [authGuard], loadComponent: () => import('./pages/paper-card-import/paper-card-import.component').then(m => m.PaperCardImportComponent) },
  { path: 'meal-assistant', canActivate: [authGuard], loadComponent: () => import('./pages/meal-assistant/meal-assistant.component').then(m => m.MealAssistantComponent) },
  { path: 'home', canActivate: [authGuard], loadComponent: () => import('./pages/home/home.component').then(m => m.HomeComponent) },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: 'recipes', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-list/recipe-list.component').then(m => m.RecipeListComponent) },
  { path: 'debug', canActivate: [authGuard], loadComponent: () => import('./pages/debug/debug.component').then(m => m.DebugComponent) },
  { path: 'logs', redirectTo: 'debug' },
  { path: 'recipes/new', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-editor/recipe-editor.component').then(m => m.RecipeEditorComponent) },
  { path: 'recipes/:id', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-detail/recipe-detail.component').then(m => m.RecipeDetailComponent) },
  { path: 'recipes/:id/edit', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-editor/recipe-editor.component').then(m => m.RecipeEditorComponent) },
  { path: 'preferences', canActivate: [authGuard], loadComponent: () => import('./pages/preferences/preferences.component').then(m => m.PreferencesComponent) },
  { path: 'cook-history', canActivate: [authGuard], loadComponent: () => import('./pages/cook-history/cook-history.component').then(m => m.CookHistoryComponent) },
  { path: 'voting', canActivate: [authGuard], loadComponent: () => import('./pages/voting/voting.component').then(m => m.VotingComponent) }
];
