import { describe, expect, it, vi } from 'vitest';
import { of } from 'rxjs';
import { PaperCardImportService } from './paper-card-import.service';

describe('PaperCardImportService', () => {
  it('posts multipart parse payload with both images', () => {
    const post = vi.fn().mockReturnValue(of({}));
    const http = { post } as any;
    const service = new PaperCardImportService(http);

    const front = new File(['front'], 'front.jpg', { type: 'image/jpeg' });
    const back = new File(['back'], 'back.jpg', { type: 'image/jpeg' });

    service.parse(front, back).subscribe();

    const [, body] = post.mock.calls[0];
    expect(post).toHaveBeenCalledWith('http://localhost:5000/api/import/paper-card/parse', expect.any(FormData));
    expect(body.get('frontImage')).toBe(front);
    expect(body.get('backImage')).toBe(back);
  });

  it('posts commit payload', () => {
    const post = vi.fn().mockReturnValue(of({ recipeId: 'r1' }));
    const http = { post } as any;
    const service = new PaperCardImportService(http);

    const request = { draftId: 'd1', selectedServings: 2 };
    service.commit(request).subscribe();

    expect(post).toHaveBeenCalledWith('http://localhost:5000/api/import/paper-card/commit', request);
  });
});
