import { randomUUID } from 'node:crypto';

/** An email no other test uses, so each test gets its own user and its own code request limit. */
export function uniqueEmail(): string {
  return `e2e-${randomUUID()}@example.com`;
}
