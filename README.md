# Big Local Hub — .NET MAUI

A .NET MAUI rendition of the Big Local hub app. The canonical implementation is
the TypeScript monorepo (`northstarapp`, pushed as
[biglocalhub](https://github.com/unifiedbap/biglocalhub)) — React web app, Expo
mobile app, and a shared `packages/core`.

This project talks to **the same Firebase project** (`big-local-ideas`) and the
same Firestore documents. It is not a fork of the data, just a second client.

**Status.** Auth, company/module gating, Dashboard, Leads, Jobs, Time,
Calendar, and Agenda all work end to end on the shared token system described
below. Apple Calendar sync works today; Google Calendar needs a one-time OAuth
client id (see below). Six modules remain unported.

---

## What's here

| Area | Ported from | Notes |
|---|---|---|
| `Models/Enums.cs` | `packages/core/types.ts` | Modules, lead/job statuses, lead sources |
| `Models/Documents.cs` | `packages/core/types.ts` | Firestore document models |
| `Services/StageLabels.cs` | `packages/core/stageLabels.ts` | Per-company pipeline wording |
| `Services/SessionService.cs` | `packages/core/auth.tsx` | Auth state machine |
| `Services/FirestoreRepository.cs` | `packages/core/hooks.ts` | `useCollection` equivalent |
| `Views/DashboardPage.xaml` | `apps/web/src/pages/Dashboard.tsx` | Needs-action list, Quick Actions, counts |
| `Views/LeadsPage.xaml` | `apps/web/src/pages/Leads.tsx` | List, filters, add/edit/delete |
| `Views/JobsPage.xaml` | `apps/web/src/pages/Jobs.tsx` | List + status filters (read-only) |
| `Views/TimePage.xaml` | `apps/web/src/pages/Time.tsx` | Clock in/out + month calendar of hours |
| `Views/TeamPage.xaml` | `apps/web/src/pages/Time.tsx` (Manage Team) | Manager-only: link logins to crew |
| `Views/AgendaPage.xaml` | `apps/web/src/pages/Agenda.tsx` | Tasks + inline done/reopen |
| `Views/CalendarPage.xaml` | `apps/web/src/pages/Calendar.tsx` | Upcoming agenda + external sync |
| `Services/ModuleRegistry.cs` | — | Label/icon/route for every module, in one place |

### Not ported yet

Clients, GC & Contractors, Marketing, Cold Call CRM, Money, Bids, Expenses,
Customer Portal — plus, within Leads, the spreadsheet import flow, the
cold-call cadence engine, and lead→client→job conversion. Jobs is read-only for
now, and Manage Team can link and unlink crew but not add or rename them. The **More** tab lists the company's enabled modules and
marks which ones this build actually implements.

## Quick Actions

The Dashboard's four shortcut tiles. Defaults are Jobs / Time / Calendar /
Agenda; tapping **Edit** lets each user pick their own four from whatever their
company has enabled and this client implements.

The choice is stored in device-local `Preferences`, **not** on `/users/{uid}` —
that document's Firestore rule only permits a teammate to write `notifyPrefs`,
`phone`, `fcmTokens`, and `updatedAt`, so a `quickActions` field would be
rejected with permission-denied. To make the preference follow a user between
devices: add `quickActions` to `touchesOnlyNotifyFields()` in `firestore.rules`,
deploy, then swap the two methods in `Services/UserPreferences.cs` for a
Firestore read/write. Nothing else has to change.

## Roles, employees, and clocking in

**companyRole is separate from role.** `UserDoc.role` stays platform-level
(`admin` has no companyId at all); `companyRole` is `manager` or `staff` within
a company. Absent reads as staff, so no existing user gains manager rights when
this ships. Collapsing them into one field would make a platform admin
accidentally a manager everywhere, or force a crew manager to be given platform
powers.

**Employees link to logins, they don't replace them.** `employees` keeps its
free-text records (the web Time page and the QuickBooks export depend on
`name`/`qbName`) and gains an optional `uid` pointing at `/users/{uid}`. Only a
*linked* employee can clock in, because that link is the only thing proving
whose shift it is. Unlinked crew still get hours logged for them from the web
app.

**Managers link; they don't create logins.** A client cannot mint a Firebase
Auth user, and relaxing `createCompanyUser` so managers could would mean
managers setting other people's passwords. A platform admin creates the account
and assigns it to the company; the manager's **More → Manage Team** screen then
picks from users already on that company.

**A shift is a TimeEntry with a start and no end.** No new field: clocking in
writes `startTime` with `endTime` empty and `hours` 0; clocking out fills both.
An in-progress shift therefore contributes 0 to every total, including the
web app's payroll export — you can't bill hours nobody has finished. A shift
crossing midnight adds a day rather than recording negative hours.

### Firestore rules — must be deployed

`firestore.rules` in the **northstarapp** repo gains an `isManager(companyId)`
helper and an `employees` carve-out: everyone on the company reads the roster
(Time needs it to resolve names and find "me"), only managers write it. The
generic subcollection rule now excludes `employees`, because overlapping
matches UNION their allows — without the exclusion any member would still get
writes.

Until that is deployed, the manager gate is **client-side only and bypassable**.
The in-app checks (`SessionService.IsManager`) are convenience, not a security
boundary.

```bash
firebase deploy --only firestore:rules
```

The Manage Team picker also reads `/users` filtered by `companyId`, which needs
the teammate-read rule. If that isn't live the screen says so explicitly rather
than showing an empty picker that looks like "no users exist".

## Calendar sync

Events are derived from Jobs exactly as the web Calendar does — a job's
`quoteDate` becomes a quote appointment, its `startDate` a job start. There is
no separate events collection. Only **future** events are pushed; back-filling
years of finished work into someone's personal calendar isn't what "sync"
means to them and is hard to undo.

Re-syncing is idempotent. Google gets an app-defined event id derived from the
source doc, so a repeat sync is a PUT rather than a duplicate. EventKit has no
custom-field search, so the source id is written into the event notes and
matched over its own date window.

### Apple Calendar — works now

Uses EventKit against the device's own calendar store (including any iCloud or
Exchange accounts the user has added). No OAuth, no network, works offline.
`NSCalendarsUsageDescription` and `NSCalendarsFullAccessUsageDescription` are
already in `Platforms/iOS/Info.plist` — without them iOS terminates the app on
the first EventKit call rather than returning an error.

### Google Calendar — needs a one-time setup

The full OAuth 2.0 + PKCE flow and Calendar API v3 calls are implemented in
`Services/Calendar/GoogleCalendarBridge.cs`. What's missing is a credential
that can only be created in your Google Cloud console:

1. In the Google Cloud console for the `big-local-ideas` project, enable the
   **Google Calendar API**.
2. Create an **OAuth 2.0 Client ID** of type **iOS**, with bundle id
   `com.biglocalideas.biglocal`.
3. Put the client id in `GoogleCalendarConfig.ClientId`.
4. Add the **reversed** client id (e.g.
   `com.googleusercontent.apps.1234567890-abcdefg`) as a `CFBundleURLScheme`
   in `Platforms/iOS/Info.plist`, so the OAuth redirect can return to the app.

Until step 3 is done the bridge reports `NotConfigured` and the Calendar screen
says "Setup needed" rather than dropping the user into an auth screen that
can't succeed. The client id is not a secret — installed apps get no client
secret, and security comes from PKCE plus the registered redirect URI — so it
is fine to commit.

---

## Design system

Light theme, token-driven. **Screens must consume tokens — never inline a hex
value or a magic number.** Dashboard is the reference implementation; copy its
patterns when porting a module.

| File | Holds |
|---|---|
| `Resources/Styles/Tokens/Colors.xaml` | Palette + status pairs, with measured WCAG ratios |
| `Resources/Styles/Tokens/Typography.xaml` | Type scale + base text styles |
| `Resources/Styles/Tokens/Spacing.xaml` | 4pt spacing scale, radii, touch floor |
| `Resources/Styles/Components.xaml` | Cards, badges, buttons, chips, form controls |
| `Services/DesignTokens.cs` | C# mirror for the code-built Splash/Notice pages |

**Accessibility rules baked into the tokens**

- Body text floor is 16pt. 14pt exists only for badges and section eyebrows,
  which are short bold labels, not content.
- `FontAutoScalingEnabled` is set explicitly on every text style, so the OS
  accessibility text-size setting is respected. Nothing pins a height around
  text — rows use `MinimumHeightRequest` so they grow instead of clipping.
- 44pt minimum touch target, inherited from the component styles rather than
  remembered per screen.
- Every text/background pair is checked against WCAG AA and the measured ratio
  is recorded in `Colors.xaml`. The accent was darkened from `#0F77E6` to
  `#0B63C4` because the original measured 4.37:1 on white and failed AA for
  body text.
- Status badges are dark ink on a light tint, never white on a saturated fill —
  saturated fills at badge size rarely clear 4.5:1.

**Status colors** mean the same thing everywhere, resolved through
`StatusTones`: green = active/done, amber = pending, red = urgent/overdue.
Lost is neutral, not red — a lost lead isn't an error.

**Navigation.** Three permanent slots: **Dashboard · [active module] · More**,
always icon + label. Dashboard and More never move; the middle slot is the
"where am I" tab — it starts on Leads and becomes whatever module you open from
Quick Actions or the More launcher, so the highlighted tab always matches the
screen.

That middle slot is one persistent `ModuleHostPage` whose content is swapped
(`AppShell.ShowModuleAsync`). Modules are deliberately *not* pushed onto a
tab's navigation stack: doing that left the bar highlighting "Dashboard" while
the page title said "Calendar", with no way back. Swapping also lets the bar
stay at three however many modules a company enables — the launcher absorbs the
growth, so every target stays well past the 44pt touch floor.

Each swap disposes the outgoing view model. They hold live Firestore snapshot
listeners, and switching without disposing would accumulate one listener per
visit for the whole session.

**Pipeline wording is never hardcoded.** Dashboard action rows title themselves
from `StageLabels`, so an agency sees "Meeting scheduled" where a contractor
sees "Quote scheduled".

## Architecture notes

**Statuses are strings, not enums.** Values like `"Quote scheduled"` are the
stored contract, shared with the web app, the Expo app, and the Cloud
Functions. They're kept as string constants so nothing can drift. What the user
*sees* is resolved separately through `StageLabels` (company override →
business-type preset → canonical value), exactly as in `stageLabels.ts`.

**Writes are field-level, never whole-document.** The models here map only the
fields this app uses. A lead document also carries cadence state, portal links,
and `importBatchId`; a full-document `SetData` would silently drop them, so
`FirestoreRepository.UpdateAsync` writes named fields only.

**Module gating is real, not cosmetic.** `AppShell` builds its tabs from
`company.enabledModules`, so a module the company doesn't have has no route.

**`importBatchId` matters.** The `notifyOnNewLead` Cloud Function skips leads
carrying it. Leads created from this app deliberately don't set it, so adding
one *will* notify the team — which is correct for a hand-entered lead.

---

## Running it

### iOS — works today

```bash
dotnet build -f net10.0-ios -r iossimulator-arm64
```

Install and launch on a booted simulator:

```bash
xcrun simctl install booted bin/Debug/net10.0-ios/iossimulator-arm64/BigLocalHub.app
```

```bash
xcrun simctl launch booted com.biglocalideas.biglocal
```

### Android — blocked

The Android target is configured but cannot build on this machine yet:

1. **No Android SDK installed.** `dotnet build -f net10.0-android` fails with
   `XA5300`. Install it, or point `AndroidSdkDirectory` at an existing SDK.
2. **No `google-services.json`.** No Android app is registered in the
   `big-local-ideas` Firebase console project (the Expo app was iOS-only).
   Register one for `com.biglocalideas.biglocal`, download the file to
   `Platforms/Android/google-services.json`, and the existing csproj item will
   pick it up.

---

## Gotchas worth knowing

**Bundle id is shared with the Expo app** (`com.biglocalideas.biglocal`), so
this project can reuse that app's `GoogleService-Info.plist` without
registering a second iOS app. The trade-off: the two builds can't be installed
side by side on one device. To run both, change `ApplicationId`, register that
bundle id in Firebase, and drop in the new plist.

**`FirebaseInstallations` must be referenced explicitly.** `FirebaseSessions`
(pulled in by Auth/Firestore) links against it, but no `Plugin.Firebase`
package depends on the Installations binding, so its framework never lands in
the bundle and the app dies at launch with
`dyld: Library not loaded: @rpath/FirebaseInstallations.framework`. Hence the
explicit `AdamE.Firebase.iOS.Installations` reference, version-pinned to match
the other native bindings.

**Use forward slashes in MSBuild `Exists()`.** MSBuild normalizes backslashes
inside `Include`, but *not* inside `Exists()` on macOS — so a Windows-style
path silently evaluates false and quietly omits the file. That cost one
debugging cycle here: the plist was left out of the bundle and only surfaced at
runtime as `FirebaseApp.configure() could not find a valid GoogleService-Info.plist`.

**`GoogleService-Info.plist` is committed.** It's consistent with the Expo app,
which commits the same file, and Firebase iOS config is designed to ship inside
the app binary — the security boundary is Firestore rules, not this file. Move
it out of source control if you'd rather not have it here.
