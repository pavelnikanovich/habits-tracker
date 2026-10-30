# Ubiquitous language

The only source of domain terms for habits-tracker. A term is spelled the same in specs, class names, tests, API and the ledger. Renaming a term is a refactoring everywhere. A word used in two contexts is defined in each. One term per concept, no synonyms.

Each bounded context is a package in the code (modular monolith), not a service.

## Bounded contexts

| Context | Kind | Responsibility |
|---|---|---|
| Identity | supporting | Who the user is and whether they are signed in: accounts, one-time codes, sessions, time zone, locale, account deletion. |
| Habit Tracking | core | Habits, their schedules, check-ins, and everything computed from them: day status, streaks, completion rate. |
| Live Updates | generic | Delivers changes to every open connection of the same user. Knows no domain rules and decides nothing. |

## Context map

| Upstream | Downstream | What crosses the boundary |
|---|---|---|
| Identity | Habit Tracking | User id and the user's time zone. Habit Tracking knows nothing about codes or sessions. |
| Identity | Habit Tracking | Account deleted: Habit Tracking erases all habits of that user with their check-ins, periods and tombstones. |
| Identity | Habit Tracking | Time zone changed: before the new zone applies, Habit Tracking stores the Closed-through date of every Commitment as it stands in the old zone, so the change cannot move it back. |
| Habit Tracking | Live Updates | Change notifications: some data of a user changed (a check-in recorded, a habit renamed or archived, ...). Live Updates forwards them to the user's open connections. |
| Identity | Live Updates | Session revoked or expired: the connections opened under that session are closed. |
| Identity | Live Updates | Time zone changed: Live Updates forwards the change to every open connection of the user; the page then recomputes today in the new time zone. |

## Identity

| Term (EN) | Russian word | Definition | Rules and invariants |
|---|---|---|---|
| User | Пользователь | A person signed up to the app, identified by their email. | One user per email. Created on the first successful sign-in. "Account" means the User record: creating the account creates the User, account deletion erases it. |
| Email | Email | The address a user signs in with. | Compared case-insensitively. Unique among users. |
| One-time code | Одноразовый код | A 6-digit code sent by email to prove the user owns the address. | Valid for 10 minutes. At most 5 entry attempts per code. Stored only as a hash. Used once. Requesting a new code invalidates earlier ones, once the new code is sent. At most 5 codes per email and 10 per IP address (per /64 prefix for IPv6) in any 24 hours. Never written to logs in production. |
| Sign-in | Вход | Requesting a one-time code and entering it. | A correct, unexpired, unused code opens a session. |
| Session | Сеанс | The state of being signed in on one browser. | Stored on the server; the `HttpOnly`, `Secure` cookie carries only its id. Sliding: expires after 30 days without activity, activity extends it. Activity is a request the user makes; an open Connection alone is not activity. Revocation takes effect at once. The cookie of a session that is not a Remembered session ends when the browser closes. |
| Remembered session | Запомненный сеанс | A session opened with "Remember me" ticked. | Its cookie persists across browser restarts; the session still expires after 30 days without activity. Ticking the box is the user's consent to the persistent cookie; the box is unchecked by default. |
| Sign out | Выход | Revoking the current session. | — |
| Sign out everywhere | Выход на всех устройствах | Revoking every session of the user. | Live Updates is told of every revoked session (see the context map). |
| Time zone | Часовой пояс | The user's IANA time zone. | Taken from the browser on the first sign-in; the user can change it. |
| Locale | Язык интерфейса | The language of the interface and of the one-time code email. | English, Polish and Russian. Before sign-in it follows the browser and the language switcher, falling back to English; at the first sign-in it becomes the user's locale; the user can change it. Every user-visible string lives in translation files; a new language is a new file, with no code change. |
| Account deletion | Удаление аккаунта | The user erasing their account. | Erases all the user's data at once, in every context. Not reversible. Backups keep a copy until they expire, at most 7 days. |

## Habit Tracking

| Term (EN) | Russian word | Definition | Rules and invariants |
|---|---|---|---|
| User | Пользователь | The owner of habits, known by the user id received from Identity. | Habit Tracking knows nothing about email, codes or sessions. Erased with all their habits when the account is deleted. |
| Time zone | Часовой пояс | The user's time zone, received from Identity. | Defines where a Tracking day starts and ends. A change never moves a check-in and never reopens a Closed day. |
| Habit | Привычка | The general term for something the user tracks regularly: every habit is either a Routine or a Commitment. Has a name and a schedule. | Belongs to exactly one user; no other user can see or change it. Is active or archived. A deleted habit leaves only a tombstone. "Habit" always means both Routines and Commitments; it is never used to mean a Routine only. |
| Routine | Рутина | A habit kept on trust: the user is their own judge. | Any past day can be checked in or cleared, except a Closed day (left from a time the habit was a Commitment). An archive period that begins outside a Commitment period is skipped by the streak. |
| Commitment | Обязательство | A habit kept strictly: a day the user did not record in time can no longer be changed. | Only today and yesterday can be checked in or cleared (the Edit window). An archive period that begins inside a Commitment period resets the streak. Leaving it is a Release, which takes 7 days. |
| Edit window | Окно правки | The days of a Commitment that can still be checked in or cleared: today and yesterday, except Closed days. | Yesterday closes at the end of today, in the user's time zone. A time zone change can shrink the window, never widen it. |
| Closed-through date | Дата закрытия | The last Closed day of a habit. | When the habit becomes a Commitment, by Commit or by being created as one, set to the day before yesterday, or kept if it is already later. While the habit is a Commitment, the start of each tracking day moves it forward to the day before yesterday, if that is later; so does the start of the day a Release takes effect. It never moves back: neither a Release nor a time zone change moves it. Stored with the habit, so that a time zone change cannot move it back (see the context map). A habit that was never a Commitment has none. |
| Closed day | Закрытый день | A day on or before the habit's Closed-through date. | Stays closed forever. Takes no check-in, and its check-in cannot be cleared. |
| Commit | Взять обязательство | Turning a Routine into a Commitment. | Takes effect at once, after a warning that days before yesterday close and that leaving takes 7 days. Sets the Closed-through date and starts a Commitment period. Allowed on an active habit only. |
| Release | Освобождение | Turning a Commitment back into a Routine. | Starts as a Release request, made after a warning. Takes effect at the start of a tracking day 7 days after the request; that day is fixed when the request is made. Until then the habit is still a Commitment. Taking effect is not an edit: it changes no Habit version and no device has to be told, because every device derives from the date whether the habit is a Routine or a Commitment. Not retroactive: the Closed-through date makes its last move at the start of that day and then stays where it is. |
| Release request | Запрос на освобождение | A Release that is asked for and has not taken effect yet. | Made or cancelled on an active habit only; both change the Habit version. A request made before archiving still takes effect on its day. |
| Commitment period | Период обязательства | A span of days during which a habit was a Commitment: from the day of Commit, or of creating the habit as a Commitment, to the day before a Release takes effect. | Decides whether an archive period resets the streak: it does if its first day lies in a Commitment period. |
| Habit name | Название привычки | The name the user gives a habit. | Not empty. |
| Habit version | Версия привычки | A number that changes with every change to a habit's settings (name, schedule, archive state, Commit, making or cancelling a Release request). | An edit made against an older version is rejected; the user sees the current state. Check-ins and a Release taking effect do not change the version. |
| Schedule | Расписание | When a habit is meant to be done. Either a Weekday schedule or a Weekly target. | A habit has exactly one schedule at any date. The schedule kind (Weekday schedule or Weekly target) is fixed when the habit is created and is independent of Routine or Commitment. |
| Weekday schedule | Расписание по дням недели | A schedule naming the days of the week, from one day to all seven ("daily"). | Judged per scheduled day. A scheduled day without a check-in becomes Missed once it is over. |
| Weekly target | Цель на неделю | A schedule of N check-ins per week on any days. | 1 ≤ N ≤ 7. Judged per week, not per day: no single day is Missed. |
| Schedule period | Период расписания | The span of dates during which one schedule applied. | A new Weekday schedule starts from tomorrow, or from the day after the habit's latest check-in if that is later: today is always judged by the schedule it started with, and no check-in falls on an Unscheduled day. A new N of a Weekly target starts from the next Monday. The first schedule period starts on the day the habit is created. Earlier dates keep the schedule of their period. History is never recomputed with a later schedule. |
| Week | Неделя | Monday to Sunday in the user's time zone. | Always starts on Monday. |
| Tracking day | Учётный день | A calendar date in the user's time zone. | Today, past and future are decided in the user's time zone. |
| Check-in | Отметка | The record that the user did the habit on a tracking day. "Not done" is the absence of this record. | At most one per habit per tracking day. Allowed for today and past days; not for future days, Unscheduled days, Not tracked days or Closed days. Allowed on active habits only. Set as an explicit state (done / not done), never as a toggle: two devices sending the same state agree; of different states the last one to reach the server wins. Stores the tracking day; a time zone change never moves it. A check-in that a time zone change put on a future date stays Done, can be cleared (for a Commitment, only once its date is in the Edit window), and counts in the streak and the completion rate once its date is no longer in the future. |
| Day status | Статус дня | What a calendar cell of a habit shows for one tracking day. | Weekday schedule: Done, Missed, Unscheduled, Planned. Weekly target: Done, Empty. Either kind: Not tracked. |
| Done | Выполнено | Day status: there is a check-in on that day. | — |
| Missed | Пропущено | Day status: the day was scheduled by a Weekday schedule, is over, and has no check-in. | Never used for a Weekly target. |
| Unscheduled | Не по расписанию | Day status: the Weekday schedule did not include that day. | Skipped by the streak. Cannot be checked in. |
| Planned | Запланировано | Day status: a day a Weekday schedule includes that is not over yet and has no check-in: today or a future day. | Today can be checked in; a future day cannot. |
| Empty | Пусто | Day status of a Weekly target day without a check-in: past, today or future. | Not a miss: a Weekly target is judged per week. |
| Not tracked | Не отслеживается | Day status of a day before the habit's first schedule period or inside an archive period. | Takes no check-in; not Missed; left out of the completion rate. |
| Week status | Статус недели | Whether a Weekly target was met in a week: Met, Not met, In progress or Not judged. | Met: the week has N check-ins, as soon as it has them. In progress: the current week, not Met yet. Not met: a week that is over without N check-ins; marked on the whole week column. Not judged: a Partial week that is over without N check-ins; never marked. |
| Partial week | Неполная неделя | A week only partly tracked: the week the habit was created in, or a week that overlaps an archive period. | Met once it has N check-ins; otherwise Not judged, never Not met. |
| Streak | Серия | How long the user has kept their word without a break: the number of consecutive kept promises up to now. | The unit is what the schedule judges: a scheduled day for a Weekday schedule, a week for a Weekly target; it is shown with the unit ("5 days", "3 weeks"). A unit counts the moment the promise is kept (today once Done, the current week once it has N check-ins) and never before. A unit still in progress neither counts nor resets the streak. A unit that ended unkept resets the streak to 0. Unscheduled days are skipped. A Partial week counts once met and is skipped otherwise. An archive period whose first day lies in a Commitment period resets the streak; any other archive period is skipped. Check-ins above N add nothing. Computed from check-ins, never stored. |
| Best streak | Лучшая серия | The highest value the streak has reached in the habit's history. | Same units and rules as Streak. Computed, never stored. |
| Completion rate | Процент выполнения | The share of scheduled work done over a recent window. | Weekday schedule: done days ÷ scheduled days over the last 30 tracking days ending today; today counts only once Done; Not tracked days are left out. Weekly target: check-ins counted up to N per week ÷ sum of N, over the last 4 complete weeks plus the current week once its target is met; weeks before the first schedule period are left out, and a Partial week is counted only once met. Weeks left out are not replaced by earlier ones. Either kind: with nothing to count, the rate is shown as "—". Computed, never stored. |
| Archive | Архивировать | Moving a habit off the main screen while keeping its history. | An archived habit can be restored. No check-ins on an archived habit. An archived habit accepts only a rename, Restore and Delete; Commit, a Release request or its cancellation, and a schedule change need it restored first. Starts an archive period. |
| Restore | Восстановить | Returning an archived habit to the main screen. | Ends the archive period: the day of restoring is tracked again. |
| Archive period | Период в архиве | The days from archiving a habit to the day before restoring it. | Starts on the day of archiving, or on the day after the latest check-in if the day of archiving or a later day has one, so no check-in is ever hidden. Its days are Not tracked. Resets the streak if its first day lies in a Commitment period; otherwise skipped by the streak. A period with no days (archived and restored on the same day, or restored before the day after the latest check-in) has no effect. |
| Delete | Удалить | Erasing a habit with all its check-ins and periods. | Not reversible; asks for confirmation. Wins over any concurrent change: a check-in into a deleted habit is rejected. Leaves a tombstone. |
| Tombstone | Надгробие | What remains of a deleted habit: its id, owner and deletion time, no user content. | Lets a late request be answered "habit was deleted" instead of "not found". Erased with the account. |

## Live Updates

| Term (EN) | Russian word | Definition | Rules and invariants |
|---|---|---|---|
| Session | Сеанс | The Identity session a Connection was opened under. | Live Updates only learns that a session was revoked or expired; what a session is and when it expires is decided by Identity. |
| Connection | Подключение | One open browser tab of a signed-in user that receives changes. | Belongs to one session. Closed when that session is revoked or expires. |
| Change notification | Уведомление об изменении | A message telling a connection that some of its user's data changed. | Sent only to connections of the user who owns the data. Every open connection of that user ends in the same state. |
