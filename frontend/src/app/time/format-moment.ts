const minute = 60_000;

/**
 * A moment such as `retryAt` for the person (contracts/ui.md, "Times"), in the page's language and the browser's time
 * zone: the time alone when the moment is on `now`'s day, else the date and the time. It is rounded up to the next
 * minute (a moment already on the minute stays), so the time shown is never earlier than the moment.
 */
export function formatMoment(moment: Date, now: Date, language: string, timeZone: string): string {
  const rounded = new Date(Math.ceil(moment.getTime() / minute) * minute);
  const time = new Intl.DateTimeFormat(language, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone }).format(rounded);
  if (dayOf(rounded, timeZone) === dayOf(now, timeZone)) return time;

  const date = new Intl.DateTimeFormat(language, { day: 'numeric', month: 'long', timeZone }).format(rounded);
  return `${date}, ${time}`;
}

/** The browser's time zone, the one the page shows moments in; UTC when the browser reports none. */
export function browserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}

// The calendar day in the time zone, in a form that compares: the same locale whatever the page's language.
function dayOf(moment: Date, timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone }).format(moment);
}
