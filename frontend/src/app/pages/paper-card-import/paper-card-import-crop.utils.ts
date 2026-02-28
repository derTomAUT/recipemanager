export type CropHandle = 'n' | 'e' | 's' | 'w' | 'ne' | 'se' | 'sw' | 'nw';

export interface CropRect {
  cropX: number;
  cropY: number;
  cropWidth: number;
  cropHeight: number;
}

export function applyCropHandleDrag(
  startRect: CropRect,
  handle: CropHandle,
  deltaXPercent: number,
  deltaYPercent: number,
  minSizePercent = 1
): CropRect {
  let left = startRect.cropX;
  let top = startRect.cropY;
  let right = startRect.cropX + startRect.cropWidth;
  let bottom = startRect.cropY + startRect.cropHeight;

  if (handle.includes('w')) {
    left += deltaXPercent;
  }
  if (handle.includes('e')) {
    right += deltaXPercent;
  }
  if (handle.includes('n')) {
    top += deltaYPercent;
  }
  if (handle.includes('s')) {
    bottom += deltaYPercent;
  }

  if (handle.includes('w')) {
    left = clamp(left, 0, right - minSizePercent);
  }
  if (handle.includes('e')) {
    right = clamp(right, left + minSizePercent, 100);
  }
  if (handle.includes('n')) {
    top = clamp(top, 0, bottom - minSizePercent);
  }
  if (handle.includes('s')) {
    bottom = clamp(bottom, top + minSizePercent, 100);
  }

  left = clamp(left, 0, 100 - minSizePercent);
  top = clamp(top, 0, 100 - minSizePercent);
  right = clamp(right, left + minSizePercent, 100);
  bottom = clamp(bottom, top + minSizePercent, 100);

  return {
    cropX: left,
    cropY: top,
    cropWidth: right - left,
    cropHeight: bottom - top
  };
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
