import { describe, expect, it } from 'vitest';
import { resolveImageUrl } from './url.utils';

describe('resolveImageUrl', () => {
  it('routes uploaded images through the backend origin', () => {
    expect(resolveImageUrl('/uploads/photo.jpg')).toBe('http://localhost:5000/uploads/photo.jpg');
  });

  it('rewrites localhost absolute upload urls through the backend origin', () => {
    expect(resolveImageUrl('http://localhost:5000/uploads/photo.jpg')).toBe('http://localhost:5000/uploads/photo.jpg');
    expect(resolveImageUrl('http://127.0.0.1:5000/uploads/photo.jpg')).toBe('http://localhost:5000/uploads/photo.jpg');
  });

  it('keeps absolute URLs unchanged', () => {
    expect(resolveImageUrl('https://cdn.example.com/photo.jpg')).toBe('https://cdn.example.com/photo.jpg');
  });
});
