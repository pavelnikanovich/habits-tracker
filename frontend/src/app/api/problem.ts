import { HttpErrorResponse } from '@angular/common/http';

/** The problem codes of `contracts/http-api.md` that a page of the sign-in shows; each has a text `error.<code>`. */
export const knownCodes = new Set([
  'invalid-email',
  'invalid-locale',
  'invalid-code-format',
  'code-incorrect',
  'new-code-required',
  'not-signed-in',
  'email-limit',
  'ip-limit',
  'email-service-limit',
  'email-service-unavailable',
  'invalid-request',
  'not-found',
  'internal-error',
]);

/** What the page keeps of an error response: `code` (or `network` when nothing usable came back), `retryAt`, `traceId`. */
export interface Problem {
  code: string;
  retryAt?: string;
  traceId?: string;
}

export const NETWORK_PROBLEM: Problem = { code: 'network' };

/**
 * The problem an HTTP error carries (RFC 9457 with `code`, `retryAt`, `traceId`). A request that got no response, or a
 * response that is not one of the API's problems (a proxy's error page), is `network`: the page can say no more.
 */
export function problemOf(error: unknown): Problem {
  if (!(error instanceof HttpErrorResponse) || error.status === 0) return NETWORK_PROBLEM;

  const body: unknown = error.error;
  if (typeof body !== 'object' || body === null) return NETWORK_PROBLEM;

  const { code, retryAt, traceId } = body as Record<string, unknown>;
  if (typeof code !== 'string' || !knownCodes.has(code)) return NETWORK_PROBLEM;

  return {
    code,
    retryAt: typeof retryAt === 'string' ? retryAt : undefined,
    traceId: typeof traceId === 'string' ? traceId : undefined,
  };
}
