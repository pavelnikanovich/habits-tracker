import { HttpErrorResponse } from '@angular/common/http';

/**
 * The problem codes of `contracts/http-api.md`, each with a text `error.<code>`. The backend's `ProblemCodes` holds the
 * same codes; a Host test keeps the two lists equal.
 */
export const knownCodes = [
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
] as const;

/** A code the API answers, or `network` when nothing usable came back. */
export type ProblemCode = (typeof knownCodes)[number] | 'network';

/** What the page keeps of an error response: `code` (or `network` when nothing usable came back), `retryAt`, `traceId`. */
export interface Problem {
  code: ProblemCode;
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
  if (!isKnownCode(code)) return NETWORK_PROBLEM;

  return {
    code,
    retryAt: typeof retryAt === 'string' ? retryAt : undefined,
    traceId: typeof traceId === 'string' ? traceId : undefined,
  };
}

function isKnownCode(code: unknown): code is (typeof knownCodes)[number] {
  return (knownCodes as readonly unknown[]).includes(code);
}
