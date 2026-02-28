import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { IngredientInput, ImportedImageInput, PaperCardParseResponse, StepInput } from '../../models/recipe.model';
import { PaperCardImportService } from '../../services/paper-card-import.service';
import { resolveImageUrl } from '../../utils/url.utils';
import { getApiErrorMessage } from '../household-settings/household-settings.utils';
import { applyCropHandleDrag, CropHandle, CropRect } from './paper-card-import-crop.utils';

type ImageSide = 'front' | 'back';

interface ImageEditState {
  sourceFile: File;
  sourceUrl: string;
  image: HTMLImageElement;
  rotation: number;
  cropX: number;
  cropY: number;
  cropWidth: number;
  cropHeight: number;
  previewWidth: number;
  previewHeight: number;
}

interface ParsedImageEditState {
  url: string;
  orderIndex: number;
  isTitleImage: boolean;
  image: HTMLImageElement | null;
  rotation: number;
  cropX: number;
  cropY: number;
  cropWidth: number;
  cropHeight: number;
  previewWidth: number;
  previewHeight: number;
  applying: boolean;
}

@Component({
  selector: 'app-paper-card-import',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="paper-import-page">
      <header class="page-header">
        <h1>Import from Paper Card</h1>
        <a routerLink="/home" class="btn btn-secondary">Back</a>
      </header>

      <div *ngIf="error" class="error">{{ error }}</div>
      <div *ngIf="message" class="message">{{ message }}</div>

      <section class="card">
        <div class="section-header">
          <h2>1. Add Card Photos</h2>
          <button *ngIf="parsed" class="btn btn-secondary" type="button" (click)="uploadCollapsed = !uploadCollapsed">
            {{ uploadCollapsed ? 'Show Upload Editor' : 'Hide Upload Editor' }}
          </button>
        </div>
        <p class="help" *ngIf="!uploadCollapsed">Upload front and back, then rotate and drag crop handles to frame each image before parsing.</p>

        <div class="editor-grid" *ngIf="!uploadCollapsed">
          <div class="editor-panel">
            <h3>Front Side</h3>
            <input type="file" accept="image/*" capture="environment" (change)="onFrontSelected($event)" />
            <small *ngIf="frontEdit">{{ frontEdit.sourceFile.name }}</small>

            <div class="preview-shell" *ngIf="frontEdit">
              <canvas #frontCanvas class="preview-canvas"></canvas>
              <div
                class="crop-overlay"
                [style.left.%]="frontEdit.cropX"
                [style.top.%]="frontEdit.cropY"
                [style.width.%]="frontEdit.cropWidth"
                [style.height.%]="frontEdit.cropHeight">
                <div
                  *ngFor="let handle of cropHandleOrder"
                  [class]="'crop-handle crop-handle-' + handle"
                  (pointerdown)="onCropHandlePointerDown('front', handle, $event)">
                </div>
              </div>
            </div>

            <div class="control-row" *ngIf="frontEdit">
              <button class="btn btn-secondary" type="button" (click)="rotate('front', -90)">Rotate Left</button>
              <button class="btn btn-secondary" type="button" (click)="rotate('front', 90)">Rotate Right</button>
              <button class="btn btn-secondary" type="button" (click)="rotate('front', -1)">-1deg</button>
              <button class="btn btn-secondary" type="button" (click)="rotate('front', 1)">+1deg</button>
              <button class="btn btn-secondary" type="button" (click)="resetCrop('front')">Reset Crop</button>
              <span class="meta">Rotation: {{ frontEdit.rotation }}deg</span>
            </div>
          </div>

          <div class="editor-panel">
            <h3>Back Side</h3>
            <input type="file" accept="image/*" capture="environment" (change)="onBackSelected($event)" />
            <small *ngIf="backEdit">{{ backEdit.sourceFile.name }}</small>

            <div class="preview-shell" *ngIf="backEdit">
              <canvas #backCanvas class="preview-canvas"></canvas>
              <div
                class="crop-overlay"
                [style.left.%]="backEdit.cropX"
                [style.top.%]="backEdit.cropY"
                [style.width.%]="backEdit.cropWidth"
                [style.height.%]="backEdit.cropHeight">
                <div
                  *ngFor="let handle of cropHandleOrder"
                  [class]="'crop-handle crop-handle-' + handle"
                  (pointerdown)="onCropHandlePointerDown('back', handle, $event)">
                </div>
              </div>
            </div>

            <div class="control-row" *ngIf="backEdit">
              <button class="btn btn-secondary" type="button" (click)="rotate('back', -90)">Rotate Left</button>
              <button class="btn btn-secondary" type="button" (click)="rotate('back', 90)">Rotate Right</button>
              <button class="btn btn-secondary" type="button" (click)="rotate('back', -1)">-1deg</button>
              <button class="btn btn-secondary" type="button" (click)="rotate('back', 1)">+1deg</button>
              <button class="btn btn-secondary" type="button" (click)="resetCrop('back')">Reset Crop</button>
              <span class="meta">Rotation: {{ backEdit.rotation }}deg</span>
            </div>
          </div>
        </div>

        <button class="btn btn-primary" *ngIf="!uploadCollapsed" [disabled]="!canParse || parsing" (click)="parse()">
          <span *ngIf="parsing" class="spinner" aria-hidden="true"></span>
          {{ parsing ? 'Parsing...' : 'Parse Paper Card' }}
        </button>
      </section>

      <section #parsedSection class="card" *ngIf="parsed">
        <h2>2. Review & Select Serving Scale</h2>
        <label class="field">
          <span>Recipe Title</span>
          <input [(ngModel)]="title" />
        </label>
        <label class="field">
          <span>Description</span>
          <textarea rows="2" [(ngModel)]="description"></textarea>
        </label>

        <div class="parsed-images" *ngIf="parsedImageEditors.length">
          <div class="parsed-editor" *ngFor="let editor of parsedImageEditors; let i = index">
            <h4>{{ editor.isTitleImage ? 'Hero Image' : ('Step Image ' + i) }}</h4>
            <div class="preview-shell">
              <canvas #parsedCanvas class="preview-canvas"></canvas>
              <div
                class="crop-overlay"
                [style.left.%]="editor.cropX"
                [style.top.%]="editor.cropY"
                [style.width.%]="editor.cropWidth"
                [style.height.%]="editor.cropHeight">
                <div
                  *ngFor="let handle of cropHandleOrder"
                  [class]="'crop-handle crop-handle-' + handle"
                  (pointerdown)="onParsedCropHandlePointerDown(i, handle, $event)">
                </div>
              </div>
            </div>
            <div class="control-row">
              <button class="btn btn-secondary" type="button" (click)="rotateParsed(editor, -90)">Rotate Left</button>
              <button class="btn btn-secondary" type="button" (click)="rotateParsed(editor, 90)">Rotate Right</button>
              <button class="btn btn-secondary" type="button" (click)="rotateParsed(editor, -1)">-1deg</button>
              <button class="btn btn-secondary" type="button" (click)="rotateParsed(editor, 1)">+1deg</button>
              <button class="btn btn-secondary" type="button" (click)="resetParsedCrop(editor)">Reset Crop</button>
            </div>
            <button class="btn btn-secondary" type="button" [disabled]="editor.applying" (click)="applyParsedImageEdit(editor)">
              {{ editor.applying ? 'Applying...' : 'Apply Image Edit' }}
            </button>
          </div>
        </div>

        <div class="sub-section">
          <h3>Ingredients</h3>
          <div class="servings-selector">
            <label class="field">
              <span>Store ingredient list for servings</span>
              <select [(ngModel)]="selectedServings" (ngModelChange)="onServingChange()">
                <option [ngValue]="null">Choose serving scale</option>
                <option *ngFor="let s of parsed.servingsAvailable" [ngValue]="s">{{ s }} people</option>
              </select>
            </label>
            <small class="selector-help" *ngIf="!selectedServings">Choose a serving scale to populate ingredients and enable save.</small>
          </div>
          <div *ngFor="let ingredient of ingredients; let i = index" class="row">
            <input [(ngModel)]="ingredient.quantity" [name]="'qty'+i" placeholder="Qty" />
            <input [(ngModel)]="ingredient.unit" [name]="'unit'+i" placeholder="Unit" />
            <input [(ngModel)]="ingredient.name" [name]="'name'+i" placeholder="Ingredient" />
            <input [(ngModel)]="ingredient.notes" [name]="'notes'+i" placeholder="Notes" />
          </div>
          <button class="btn btn-secondary" type="button" (click)="addIngredient()">Add Ingredient</button>
        </div>

        <div class="sub-section">
          <h3>Steps</h3>
          <div *ngFor="let step of steps; let i = index" class="step-row">
            <textarea rows="2" [(ngModel)]="step.instruction" [name]="'step'+i" placeholder="Instruction"></textarea>
          </div>
          <button class="btn btn-secondary" type="button" (click)="addStep()">Add Step</button>
        </div>

        <ul class="warnings" *ngIf="parsed.warnings.length">
          <li *ngFor="let warning of parsed.warnings">{{ warning }}</li>
        </ul>

        <button class="btn btn-primary" [disabled]="saving || !selectedServings" (click)="commit()">
          {{ saving ? 'Saving...' : 'Save Recipe' }}
        </button>
      </section>
    </div>
  `,
  styles: [`
    .paper-import-page { max-width: 960px; margin: 0 auto; padding: 1rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; margin-bottom: 1rem; }
    .section-header { display: flex; justify-content: space-between; align-items: center; gap: 0.6rem; }
    .card { background: var(--surface); border-radius: var(--radius-md); box-shadow: var(--shadow-soft); border: 1px solid color-mix(in srgb, var(--text) 12%, transparent); padding: 1rem; margin-bottom: 1rem; }
    .help { color: var(--muted); margin-top: 0; }
    .editor-grid { display: grid; grid-template-columns: 1fr; gap: 1rem; margin-bottom: 1rem; }
    .editor-panel { background: var(--surface-2); border-radius: var(--radius-sm); padding: 0.75rem; display: grid; gap: 0.55rem; }
    .preview-shell { position: relative; border-radius: var(--radius-sm); overflow: hidden; border: 1px solid color-mix(in srgb, var(--text) 18%, transparent); width: fit-content; max-width: 100%; }
    .preview-canvas { display: block; max-width: 100%; touch-action: none; cursor: crosshair; background: color-mix(in srgb, var(--surface) 86%, black); }
    .crop-overlay { position: absolute; border: 2px solid var(--primary); background: color-mix(in srgb, var(--primary) 20%, transparent); box-sizing: border-box; pointer-events: auto; touch-action: none; }
    .crop-handle { position: absolute; width: 22px; height: 22px; border-radius: 999px; background: var(--primary); border: 2px solid #fff; box-shadow: 0 0 0 1px color-mix(in srgb, black 24%, transparent); pointer-events: auto; touch-action: none; }
    .crop-handle-n { left: 50%; top: 0; transform: translate(-50%, -50%); cursor: n-resize; }
    .crop-handle-s { left: 50%; bottom: 0; transform: translate(-50%, 50%); cursor: s-resize; }
    .crop-handle-e { right: 0; top: 50%; transform: translate(50%, -50%); cursor: e-resize; }
    .crop-handle-w { left: 0; top: 50%; transform: translate(-50%, -50%); cursor: w-resize; }
    .crop-handle-ne { right: 0; top: 0; transform: translate(50%, -50%); cursor: nesw-resize; }
    .crop-handle-se { right: 0; bottom: 0; transform: translate(50%, 50%); cursor: nwse-resize; }
    .crop-handle-sw { left: 0; bottom: 0; transform: translate(-50%, 50%); cursor: nesw-resize; }
    .crop-handle-nw { left: 0; top: 0; transform: translate(-50%, -50%); cursor: nwse-resize; }
    .control-row { display: flex; gap: 0.45rem; flex-wrap: wrap; align-items: center; }
    .meta { color: var(--muted); font-size: 0.9rem; }
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 0.75rem; }
    .servings-selector { background: color-mix(in srgb, var(--surface-2) 86%, var(--primary)); border: 1px solid color-mix(in srgb, var(--text) 18%, transparent); border-radius: var(--radius-sm); padding: 0.7rem; margin-bottom: 0.75rem; }
    .servings-selector .field { margin-bottom: 0.35rem; }
    .servings-selector select { background: var(--surface); color: var(--text); border: 1px solid color-mix(in srgb, var(--text) 24%, transparent); border-radius: var(--radius-sm); padding: 0.45rem 0.55rem; font-weight: 600; }
    .selector-help { color: var(--muted); display: block; }
    .parsed-images { display: grid; gap: 0.8rem; margin-bottom: 1rem; }
    .parsed-editor { border: 1px solid color-mix(in srgb, var(--text) 14%, transparent); border-radius: var(--radius-sm); padding: 0.6rem; background: var(--surface-2); display: grid; gap: 0.45rem; }
    .parsed-editor h4 { margin: 0; }
    .spinner { width: 14px; height: 14px; border: 2px solid rgba(255,255,255,0.55); border-top-color: #fff; border-radius: 50%; display: inline-block; vertical-align: middle; margin-right: 0.45rem; animation: spin 0.8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .sub-section { margin-bottom: 1rem; }
    .row { display: grid; grid-template-columns: 90px 90px 1fr 1fr; gap: 0.5rem; margin-bottom: 0.5rem; }
    .step-row { margin-bottom: 0.5rem; }
    .step-row textarea { width: 100%; }
    .warnings { background: color-mix(in srgb, var(--accent) 22%, var(--surface)); border-radius: var(--radius-sm); padding: 0.75rem 1rem; }
    .btn { border: none; border-radius: var(--radius-sm); padding: 0.65rem 1rem; cursor: pointer; }
    .btn-primary { background: var(--primary); color: white; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
    .btn-secondary { background: var(--surface-2); color: var(--text); text-decoration: none; display: inline-block; }
    .error { background: color-mix(in srgb, var(--primary) 20%, var(--surface)); color: var(--text); padding: 0.75rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    .message { background: color-mix(in srgb, var(--secondary) 20%, var(--surface)); color: var(--text); padding: 0.75rem; border-radius: var(--radius-sm); margin-bottom: 1rem; }
    @media (max-width: 720px) {
      .row { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class PaperCardImportComponent implements OnDestroy, AfterViewInit {
  @ViewChild('frontCanvas') frontCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('backCanvas') backCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('parsedSection') parsedSection?: ElementRef<HTMLElement>;
  @ViewChildren('parsedCanvas') parsedCanvases?: QueryList<ElementRef<HTMLCanvasElement>>;

  frontEdit: ImageEditState | null = null;
  backEdit: ImageEditState | null = null;
  parsing = false;
  saving = false;
  uploadCollapsed = false;
  error = '';
  message = '';

  parsed: PaperCardParseResponse | null = null;
  title = '';
  description = '';
  selectedServings: number | null = null;
  importedImages: ImportedImageInput[] = [];
  parsedImageEditors: ParsedImageEditState[] = [];
  ingredientsByServings: Record<number, IngredientInput[]> = {};
  ingredients: IngredientInput[] = [];
  steps: StepInput[] = [];
  readonly cropHandleOrder: CropHandle[] = ['n', 'e', 's', 'w', 'ne', 'se', 'sw', 'nw'];

  private handleDragState:
    | { type: 'side'; side: ImageSide; handle: CropHandle; startX: number; startY: number; startCrop: CropRect }
    | { type: 'parsed'; index: number; handle: CropHandle; startX: number; startY: number; startCrop: CropRect }
    | null = null;

  constructor(
    private paperCardImportService: PaperCardImportService,
    private router: Router
  ) {}

  ngAfterViewInit(): void {
    this.scheduleRedraw('front');
    this.scheduleRedraw('back');
    this.parsedCanvases?.changes.subscribe(() => this.redrawAllParsedPreviews());
  }

  ngOnDestroy(): void {
    if (this.frontEdit) URL.revokeObjectURL(this.frontEdit.sourceUrl);
    if (this.backEdit) URL.revokeObjectURL(this.backEdit.sourceUrl);
  }

  @HostListener('window:resize')
  onWindowResize() {
    this.scheduleRedraw('front');
    this.scheduleRedraw('back');
    this.redrawAllParsedPreviews();
  }

  @HostListener('window:pointermove', ['$event'])
  onWindowPointerMove(event: PointerEvent) {
    this.updateHandleDrag(event, false);
  }

  @HostListener('window:pointerup', ['$event'])
  onWindowPointerUp(event: PointerEvent) {
    this.updateHandleDrag(event, true);
  }

  get canParse(): boolean {
    return !!this.frontEdit && !!this.backEdit;
  }

  onFrontSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    void this.replaceEditState('front', file);
  }

  onBackSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    void this.replaceEditState('back', file);
  }

  rotate(side: ImageSide, delta: number) {
    const state = this.getEditState(side);
    if (!state) return;
    const next = (((state.rotation + delta) % 360) + 360) % 360;
    state.rotation = Math.round(next * 10) / 10;
    this.scheduleRedraw(side);
  }

  resetCrop(side: ImageSide) {
    const state = this.getEditState(side);
    if (!state) return;
    state.cropX = 0;
    state.cropY = 0;
    state.cropWidth = 100;
    state.cropHeight = 100;
  }

  onCropHandlePointerDown(side: ImageSide, handle: CropHandle, event: PointerEvent) {
    const state = this.getEditState(side);
    const canvas = this.getCanvas(side);
    if (!canvas || !state || state.previewWidth <= 0 || state.previewHeight <= 0) return;
    const point = this.toCanvasPoint(canvas, event);
    this.handleDragState = {
      type: 'side',
      side,
      handle,
      startX: point.x,
      startY: point.y,
      startCrop: this.toCropRect(state)
    };
    event.preventDefault();
  }

  onParsedCropHandlePointerDown(index: number, handle: CropHandle, event: PointerEvent) {
    const state = this.parsedImageEditors[index];
    const canvas = this.getParsedCanvas(index);
    if (!canvas || !state || state.previewWidth <= 0 || state.previewHeight <= 0) return;
    const point = this.toCanvasPoint(canvas, event);
    this.handleDragState = {
      type: 'parsed',
      index,
      handle,
      startX: point.x,
      startY: point.y,
      startCrop: this.toCropRect(state)
    };
    event.preventDefault();
  }

  async parse() {
    if (!this.frontEdit || !this.backEdit) return;
    this.parsing = true;
    this.error = '';
    this.message = '';

    try {
      const frontFile = await this.buildEditedFile(this.frontEdit, 'front');
      const backFile = await this.buildEditedFile(this.backEdit, 'back');

      this.paperCardImportService.parse(frontFile, backFile).subscribe({
        next: async (response) => {
          this.parsing = false;
          this.parsed = response;
          this.title = response.title;
          this.description = response.description ?? '';
          this.importedImages = response.importedImages;
          await this.initializeParsedImageEditors(response.importedImages);
          this.ingredientsByServings = response.ingredientsByServings ?? {};
          this.selectedServings = null;
          this.ingredients = [];
          this.steps = (response.steps ?? []).map(step => ({ ...step }));
          this.message = 'Paper card parsed. Choose a serving scale before saving.';
          this.uploadCollapsed = true;
          this.scrollToParsedResult();
        },
        error: (error) => {
          this.parsing = false;
          this.error = getApiErrorMessage(error, 'Failed to parse paper card photos.');
        }
      });
    } catch {
      this.parsing = false;
      this.error = 'Could not prepare edited images. Please reselect the photos and try again.';
    }
  }

  onServingChange() {
    if (!this.selectedServings) {
      this.ingredients = [];
      return;
    }

    const selected = this.ingredientsByServings[this.selectedServings] ?? [];
    this.ingredients = selected.map(i => ({ ...i }));
  }

  addIngredient() {
    this.ingredients.push({ name: '', quantity: '', unit: '', notes: '' });
  }

  addStep() {
    this.steps.push({ instruction: '', timerSeconds: undefined });
  }

  commit() {
    if (!this.parsed || !this.selectedServings) {
      this.error = 'Please choose a serving scale first.';
      return;
    }

    this.saving = true;
    this.error = '';

    this.paperCardImportService.commit({
      draftId: this.parsed.draftId,
      selectedServings: this.selectedServings,
      title: this.title.trim(),
      description: this.description.trim() || undefined,
      ingredients: this.ingredients,
      steps: this.steps,
      tags: ['hellofresh', 'paper-card']
    }).subscribe({
      next: ({ recipeId }) => {
        this.saving = false;
        this.router.navigate(['/recipes', recipeId]);
      },
      error: (error) => {
        this.saving = false;
        this.error = getApiErrorMessage(error, 'Failed to save imported paper card recipe.');
      }
    });
  }

  resolveImage(url: string): string {
    return resolveImageUrl(url) ?? url;
  }

  rotateParsed(editor: ParsedImageEditState, delta: number) {
    const next = (((editor.rotation + delta) % 360) + 360) % 360;
    editor.rotation = Math.round(next * 10) / 10;
    const index = this.parsedImageEditors.indexOf(editor);
    if (index >= 0) {
      this.scheduleParsedRedraw(index);
    }
  }

  resetParsedCrop(editor: ParsedImageEditState) {
    editor.cropX = 0;
    editor.cropY = 0;
    editor.cropWidth = 100;
    editor.cropHeight = 100;
  }

  async applyParsedImageEdit(editor: ParsedImageEditState) {
    if (!this.parsed) return;
    editor.applying = true;
    this.error = '';
    try {
      const image = editor.image ?? await this.loadImage(this.resolveImage(editor.url), true);
      const edited = await this.buildEditedFileFromImage(image, editor, editor.isTitleImage ? 'hero' : `step_${editor.orderIndex}`);
      this.paperCardImportService.updateDraftImage(this.parsed.draftId, editor.orderIndex, edited).subscribe({
        next: async (response) => {
          this.importedImages = response.importedImages ?? [];
          await this.initializeParsedImageEditors(this.importedImages);
          editor.applying = false;
          this.message = 'Image edit applied to draft.';
        },
        error: (error) => {
          editor.applying = false;
          this.error = getApiErrorMessage(error, 'Failed to apply image edit.');
        }
      });
    } catch {
      editor.applying = false;
      this.error = 'Unable to edit this image in browser. Please try another image.';
    }
  }

  private updateHandleDrag(event: PointerEvent, finalize: boolean) {
    const drag = this.handleDragState;
    if (!drag) return;

    const targetState = drag.type === 'side' ? this.getEditState(drag.side) : this.parsedImageEditors[drag.index];
    const canvas = drag.type === 'side' ? this.getCanvas(drag.side) : this.getParsedCanvas(drag.index);
    if (!targetState || !canvas || targetState.previewWidth <= 0 || targetState.previewHeight <= 0) {
      if (finalize) this.handleDragState = null;
      return;
    }

    const point = this.toCanvasPoint(canvas, event);
    const deltaXPercent = ((point.x - drag.startX) / targetState.previewWidth) * 100;
    const deltaYPercent = ((point.y - drag.startY) / targetState.previewHeight) * 100;
    const next = applyCropHandleDrag(drag.startCrop, drag.handle, deltaXPercent, deltaYPercent, 1);

    targetState.cropX = next.cropX;
    targetState.cropY = next.cropY;
    targetState.cropWidth = next.cropWidth;
    targetState.cropHeight = next.cropHeight;

    if (finalize) {
      this.handleDragState = null;
    }
  }

  private toCanvasPoint(canvas: HTMLCanvasElement, event: PointerEvent): { x: number; y: number } {
    const rect = canvas.getBoundingClientRect();
    const scaleX = rect.width > 0 ? canvas.width / rect.width : 1;
    const scaleY = rect.height > 0 ? canvas.height / rect.height : 1;
    const x = this.clamp(event.clientX - rect.left, 0, rect.width) * scaleX;
    const y = this.clamp(event.clientY - rect.top, 0, rect.height) * scaleY;
    return { x, y };
  }

  private async replaceEditState(side: ImageSide, file: File): Promise<void> {
    const state = await this.createEditState(file);
    const current = this.getEditState(side);
    if (current) URL.revokeObjectURL(current.sourceUrl);

    if (side === 'front') this.frontEdit = state;
    else this.backEdit = state;

    this.scheduleRedraw(side);
  }

  private async createEditState(file: File): Promise<ImageEditState> {
    const sourceUrl = URL.createObjectURL(file);
    const image = await this.loadImage(sourceUrl);
    return {
      sourceFile: file,
      sourceUrl,
      image,
      rotation: 0,
      cropX: 0,
      cropY: 0,
      cropWidth: 100,
      cropHeight: 100,
      previewWidth: 0,
      previewHeight: 0
    };
  }

  private loadImage(url: string, withCors = false): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const image = new Image();
      if (withCors) {
        image.crossOrigin = 'anonymous';
      }
      image.onload = () => resolve(image);
      image.onerror = reject;
      image.src = url;
    });
  }

  private scheduleRedraw(side: ImageSide, attempt = 0) {
    setTimeout(() => {
      const rendered = this.redrawPreview(side);
      if (!rendered && attempt < 8) {
        this.scheduleRedraw(side, attempt + 1);
      }
    }, attempt === 0 ? 0 : 40);
  }

  private redrawPreview(side: ImageSide): boolean {
    const state = this.getEditState(side);
    const canvas = this.getCanvas(side);
    if (!state || !canvas) return false;

    const sourceWidth = state.image.naturalWidth;
    const sourceHeight = state.image.naturalHeight;
    const rotatedWidth = state.rotation % 180 === 0 ? sourceWidth : sourceHeight;
    const rotatedHeight = state.rotation % 180 === 0 ? sourceHeight : sourceWidth;

    const maxWidth = 760;
    const maxHeight = 460;
    const scale = Math.min(maxWidth / rotatedWidth, maxHeight / rotatedHeight, 1);
    const drawWidth = Math.max(1, Math.round(rotatedWidth * scale));
    const drawHeight = Math.max(1, Math.round(rotatedHeight * scale));

    canvas.width = drawWidth;
    canvas.height = drawHeight;
    state.previewWidth = drawWidth;
    state.previewHeight = drawHeight;

    const ctx = canvas.getContext('2d');
    if (!ctx) return false;
    ctx.clearRect(0, 0, drawWidth, drawHeight);
    ctx.save();
    ctx.translate(drawWidth / 2, drawHeight / 2);
    ctx.scale(scale, scale);
    ctx.rotate((state.rotation * Math.PI) / 180);
    ctx.drawImage(state.image, -sourceWidth / 2, -sourceHeight / 2);
    ctx.restore();
    return true;
  }

  private redrawAllParsedPreviews() {
    for (let i = 0; i < this.parsedImageEditors.length; i++) {
      this.scheduleParsedRedraw(i);
    }
  }

  private scheduleParsedRedraw(index: number, attempt = 0) {
    setTimeout(() => {
      const rendered = this.redrawParsedPreview(index);
      if (!rendered && attempt < 8) {
        this.scheduleParsedRedraw(index, attempt + 1);
      }
    }, attempt === 0 ? 0 : 40);
  }

  private redrawParsedPreview(index: number): boolean {
    const state = this.parsedImageEditors[index];
    const canvas = this.getParsedCanvas(index);
    if (!state || !canvas || !state.image) return false;

    const sourceWidth = state.image.naturalWidth;
    const sourceHeight = state.image.naturalHeight;
    const rotatedWidth = state.rotation % 180 === 0 ? sourceWidth : sourceHeight;
    const rotatedHeight = state.rotation % 180 === 0 ? sourceHeight : sourceWidth;

    const maxWidth = 760;
    const maxHeight = 460;
    const scale = Math.min(maxWidth / rotatedWidth, maxHeight / rotatedHeight, 1);
    const drawWidth = Math.max(1, Math.round(rotatedWidth * scale));
    const drawHeight = Math.max(1, Math.round(rotatedHeight * scale));

    canvas.width = drawWidth;
    canvas.height = drawHeight;
    state.previewWidth = drawWidth;
    state.previewHeight = drawHeight;

    const ctx = canvas.getContext('2d');
    if (!ctx) return false;
    ctx.clearRect(0, 0, drawWidth, drawHeight);
    ctx.save();
    ctx.translate(drawWidth / 2, drawHeight / 2);
    ctx.scale(scale, scale);
    ctx.rotate((state.rotation * Math.PI) / 180);
    ctx.drawImage(state.image, -sourceWidth / 2, -sourceHeight / 2);
    ctx.restore();
    return true;
  }

  private getEditState(side: ImageSide): ImageEditState | null {
    return side === 'front' ? this.frontEdit : this.backEdit;
  }

  private getCanvas(side: ImageSide): HTMLCanvasElement | null {
    if (side === 'front') return this.frontCanvas?.nativeElement ?? null;
    return this.backCanvas?.nativeElement ?? null;
  }

  private getParsedCanvas(index: number): HTMLCanvasElement | null {
    const list = this.parsedCanvases?.toArray() ?? [];
    return list[index]?.nativeElement ?? null;
  }

  private async buildEditedFile(state: ImageEditState, side: ImageSide): Promise<File> {
    return await this.buildEditedFileFromImage(state.image, state, side);
  }

  private async buildEditedFileFromImage(
    image: HTMLImageElement,
    state: { rotation: number; cropX: number; cropY: number; cropWidth: number; cropHeight: number; },
    name: string
  ): Promise<File> {
    const sourceWidth = image.naturalWidth;
    const sourceHeight = image.naturalHeight;
    const rotatedWidth = state.rotation % 180 === 0 ? sourceWidth : sourceHeight;
    const rotatedHeight = state.rotation % 180 === 0 ? sourceHeight : sourceWidth;

    const rotatedCanvas = document.createElement('canvas');
    rotatedCanvas.width = rotatedWidth;
    rotatedCanvas.height = rotatedHeight;
    const rotatedContext = rotatedCanvas.getContext('2d');
    if (!rotatedContext) throw new Error('Could not build rotated image canvas.');

    rotatedContext.translate(rotatedWidth / 2, rotatedHeight / 2);
    rotatedContext.rotate((state.rotation * Math.PI) / 180);
    rotatedContext.drawImage(image, -sourceWidth / 2, -sourceHeight / 2);

    const cropX = Math.round((state.cropX / 100) * rotatedWidth);
    const cropY = Math.round((state.cropY / 100) * rotatedHeight);
    const cropWidth = Math.max(1, Math.round((state.cropWidth / 100) * rotatedWidth));
    const cropHeight = Math.max(1, Math.round((state.cropHeight / 100) * rotatedHeight));
    const boundedWidth = Math.min(cropWidth, rotatedWidth - cropX);
    const boundedHeight = Math.min(cropHeight, rotatedHeight - cropY);

    const outputCanvas = document.createElement('canvas');
    outputCanvas.width = boundedWidth;
    outputCanvas.height = boundedHeight;
    const outputContext = outputCanvas.getContext('2d');
    if (!outputContext) throw new Error('Could not build cropped image canvas.');

    outputContext.drawImage(rotatedCanvas, cropX, cropY, boundedWidth, boundedHeight, 0, 0, boundedWidth, boundedHeight);
    const blob = await this.canvasToBlob(outputCanvas, 'image/jpeg', 0.92);
    return new File([blob], `${name}_edited.jpg`, { type: 'image/jpeg' });
  }

  private toParsedEditor = (img: ImportedImageInput): ParsedImageEditState => ({
    url: img.url,
    orderIndex: img.orderIndex,
    isTitleImage: img.isTitleImage,
    image: null,
    rotation: 0,
    cropX: 0,
    cropY: 0,
    cropWidth: 100,
    cropHeight: 100,
    previewWidth: 0,
    previewHeight: 0,
    applying: false
  });

  private async initializeParsedImageEditors(images: ImportedImageInput[]): Promise<void> {
    this.parsedImageEditors = images.map(this.toParsedEditor);
    await Promise.all(this.parsedImageEditors.map(async (editor) => {
      try {
        editor.image = await this.loadImage(this.resolveImage(editor.url), true);
      } catch {
        editor.image = null;
      }
    }));
    this.redrawAllParsedPreviews();
  }

  private scrollToParsedResult() {
    setTimeout(() => {
      this.parsedSection?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 80);
  }

  private canvasToBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob> {
    return new Promise((resolve, reject) => {
      canvas.toBlob((blob) => {
        if (!blob) {
          reject(new Error('Canvas conversion failed.'));
          return;
        }
        resolve(blob);
      }, type, quality);
    });
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.min(max, Math.max(min, value));
  }

  private toCropRect(state: { cropX: number; cropY: number; cropWidth: number; cropHeight: number }): CropRect {
    return {
      cropX: state.cropX,
      cropY: state.cropY,
      cropWidth: state.cropWidth,
      cropHeight: state.cropHeight
    };
  }
}
