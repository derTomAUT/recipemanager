import { describe, expect, it } from 'vitest';
import { applyCropHandleDrag, CropRect } from './paper-card-import-crop.utils';

describe('applyCropHandleDrag', () => {
  it('moves the west edge and expands width to the left', () => {
    const start: CropRect = { cropX: 20, cropY: 10, cropWidth: 50, cropHeight: 60 };
    const next = applyCropHandleDrag(start, 'w', -10, 0);

    expect(next.cropX).toBe(10);
    expect(next.cropY).toBe(10);
    expect(next.cropWidth).toBe(60);
    expect(next.cropHeight).toBe(60);
  });

  it('clamps east edge movement to image bounds', () => {
    const start: CropRect = { cropX: 20, cropY: 10, cropWidth: 50, cropHeight: 60 };
    const next = applyCropHandleDrag(start, 'e', 40, 0);

    expect(next.cropX).toBe(20);
    expect(next.cropY).toBe(10);
    expect(next.cropWidth).toBe(80);
    expect(next.cropHeight).toBe(60);
  });

  it('moves northwest corner on both axes', () => {
    const start: CropRect = { cropX: 20, cropY: 10, cropWidth: 50, cropHeight: 60 };
    const next = applyCropHandleDrag(start, 'nw', -5, 10);

    expect(next.cropX).toBe(15);
    expect(next.cropY).toBe(20);
    expect(next.cropWidth).toBe(55);
    expect(next.cropHeight).toBe(50);
  });

  it('enforces minimum width and height', () => {
    const start: CropRect = { cropX: 20, cropY: 10, cropWidth: 50, cropHeight: 60 };
    const next = applyCropHandleDrag(start, 'nw', 80, 90, 1);

    expect(next.cropX).toBe(69);
    expect(next.cropY).toBe(69);
    expect(next.cropWidth).toBe(1);
    expect(next.cropHeight).toBe(1);
  });
});
