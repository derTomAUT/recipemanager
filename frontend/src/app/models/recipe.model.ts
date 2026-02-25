export interface Recipe {
  id: string;
  title: string;
  description?: string;
  servings?: number;
  prepMinutes?: number;
  cookMinutes?: number;
  titleImageUrl?: string;
  tags: string[];
  cookCount: number;
  lastCooked?: string;
  createdAt: string;
}

export interface RecipeDetail extends Recipe {
  ingredients: RecipeIngredient[];
  steps: RecipeStep[];
  images: RecipeImage[];
  updatedAt: string;
  createdByUserId: string;
}

export interface RecipeIngredient {
  id: string;
  orderIndex: number;
  name: string;
  quantity?: string;
  unit?: string;
  notes?: string;
}

export interface RecipeStep {
  id: string;
  orderIndex: number;
  instruction: string;
  timerSeconds?: number;
}

export interface RecipeImage {
  id: string;
  url: string;
  isTitleImage: boolean;
  orderIndex: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateRecipeRequest {
  title: string;
  description?: string;
  servings?: number;
  prepMinutes?: number;
  cookMinutes?: number;
  ingredients: IngredientInput[];
  steps: StepInput[];
  tags: string[];
}

export interface UpdateRecipeRequest extends CreateRecipeRequest {}

export interface RecipeDraft {
  title: string;
  description?: string;
  servings?: number;
  prepMinutes?: number;
  cookMinutes?: number;
  ingredients: IngredientInput[];
  steps: StepInput[];
  tags: string[];
  confidenceScore?: number;
  warnings: string[];
}

export interface IngredientInput {
  name: string;
  quantity?: string;
  unit?: string;
  notes?: string;
}

export interface StepInput {
  instruction: string;
  timerSeconds?: number;
}

export interface CookEvent {
  id: string;
  recipeId: string;
  recipeTitle: string;
  recipeImageUrl?: string;
  userId: string;
  userName: string;
  cookedAt: string;
  servings?: number;
}
