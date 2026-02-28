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
  sourceUrl?: string;
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
  sourceUrl?: string;
  servings?: number;
  prepMinutes?: number;
  cookMinutes?: number;
  ingredients: IngredientInput[];
  steps: StepInput[];
  tags: string[];
  importedImages?: ImportedImageInput[];
}

export interface UpdateRecipeRequest extends CreateRecipeRequest {}

export interface RecipeDraft {
  title: string;
  description?: string;
  sourceUrl?: string;
  servings?: number;
  prepMinutes?: number;
  cookMinutes?: number;
  ingredients: IngredientInput[];
  steps: StepInput[];
  tags: string[];
  importedImages?: ImportedImageInput[];
  candidateImages?: CandidateImageInput[];
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

export interface ImportedImageInput {
  url: string;
  isTitleImage: boolean;
  orderIndex: number;
}

export interface CandidateImageInput {
  url: string;
  isHeroCandidate: boolean;
  orderIndex: number;
}

export interface PaperCardParseResponse {
  draftId: string;
  title: string;
  description?: string;
  ingredientsByServings: Record<number, IngredientInput[]>;
  servingsAvailable: number[];
  steps: StepInput[];
  importedImages: ImportedImageInput[];
  confidenceScore?: number;
  warnings: string[];
}

export interface PaperCardCommitRequest {
  draftId: string;
  selectedServings: number;
  title?: string;
  description?: string;
  ingredients?: IngredientInput[];
  steps?: StepInput[];
  tags?: string[];
  prepMinutes?: number;
  cookMinutes?: number;
}

export interface PaperCardCommitResponse {
  recipeId: string;
}

export interface PaperCardUpdateImagesResponse {
  importedImages: ImportedImageInput[];
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
