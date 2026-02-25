import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  { path: 'household/setup', loadComponent: () => import('./pages/household-setup/household-setup.component').then(m => m.HouseholdSetupComponent) },
  { path: '', redirectTo: '/recipes', pathMatch: 'full' },
  { path: 'recipes', canActivate: [authGuard], loadComponent: () => import('./pages/recipe-list/recipe-list.component').then(m => m.RecipeListComponent) }
];
