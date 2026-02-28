export function getHttpErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== 'object' || error === null) {
    return fallback;
  }

  const httpError = error as {
    status?: number;
    error?: unknown;
    message?: string;
  };

  if (typeof httpError.error === 'string' && httpError.error.trim().length > 0) {
    return httpError.error.trim();
  }

  if (typeof httpError.error === 'object' && httpError.error !== null) {
    const structured = httpError.error as { message?: unknown; title?: unknown };
    if (typeof structured.message === 'string' && structured.message.trim().length > 0) {
      return structured.message.trim();
    }
    if (typeof structured.title === 'string' && structured.title.trim().length > 0) {
      return structured.title.trim();
    }
  }

  if (httpError.status === 0) {
    return 'Network error while contacting the server.';
  }

  return fallback;
}
