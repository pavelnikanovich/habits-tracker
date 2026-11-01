// Vitest runs the specs in Node, so they can read the translation files and the sources. These are the only
// two functions they use, declared here rather than adding @types/node as a dependency (research.md,
// "Dependencies and their cases"). Paths are relative to the frontend folder, where `ng test` runs.
declare module 'node:fs' {
  export function readdirSync(path: string, options?: { recursive: true }): string[];
  export function readFileSync(path: string, encoding: 'utf8'): string;
}
