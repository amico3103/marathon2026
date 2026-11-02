---
sessionId: session-261029-102550-u4rg
---

# Plan in plain words

### Відповіді на ваші пункти
1. **Дані з DynamoDB.** Беремо їх з вашого справжнього бекенду. Мок-дані з `green-and-tasty` (`GROUP_ORDER_PREVIEW`, мок-інтерсептор, `?demo`-логін) **не переношу взагалі**.
2. **Основний проєкт** — `frontend/restaurant-frontend-app`. Усе пишу тільки там. Папка `green-and-tasty` лишається лише як довідка (структура тестів, текстів).
3. **Production і звичайна авторизація.** Так, усе працюватиме. Сторінка ходить на реальні ендпоінти з токеном, який уже видає ваш логін. Мок-інтерсептора і демо-логіну в `app.config.ts` не буде, тож реєстрацію та логін я не чіпаю. Єдина зміна поруч з авторизацією — новий метод сервісу, що явно додає токен власника (пояснення нижче).
4. **«Remove Guest»** (3-й рівень) — не робимо, поки план лише цей.

### Що вже є на бекенді (я перевірив у коді)
- `GET /reservations/{reservationId}/invite-token` -> `{ token }`.
- `GET /guest/invites/{token}` -> `GroupReservationId`, `RestaurantName`, `LocationAddress`, `StartTime`, `EndTime`, `MaxGuestCount`, `InviterName` (PascalCase).
- `GET /guest/groups/{groupReservationId}/sessions` (тільки власник) -> `{ participants: [{ id, type: Owner|Guest, name, items: [{ dish: {id,name,price}, quantity }], total, isSubmitted }], total }` (camelCase).
- `POST /group-reservations/{groupReservationId}/submit` — «Submit Pre-Order».

### Як сторінка отримує дані (простими словами)
1. Маршрут `/reservations/:reservationId/group-order`. Беремо `reservationId` з URL.
2. Запитуємо invite-токен, потім invite за токеном. З нього беремо адресу, дату, час, `MaxGuestCount` і `GroupReservationId`.
3. За `GroupReservationId` запитуємо список учасників (сесії).
4. Показуємо шапку, плашку, картки і Grand Total.

### Важливі нюанси
- **Токен власника.** `token.interceptor.ts` навмисно не змінює жодні запити з `/guest/`, а список учасників вимагає токен власника. Інтерсептор не чіпаю (щоб не зламати гостьовий потік). У новому методі `GuestSessionsService.getGroupSessions(groupId)` додаю заголовок `Authorization: Bearer <idToken>` з `AuthenticationService.getTokens()`.
- **Фото й нотаток страв немає в API.** У відповіді тільки назва, ціна, кількість. Тому в кожного рядка зелене коло з першою літерою, без фото й нотаток.
- **«4 of 5 guests»** = кількість учасників у відповіді сесій з `MaxGuestCount` з invite. Власник теж рахується учасником.
- **Grand Total** — `total` з відповіді сесій через пайп `currency`.
- **Пайп `date`** для дати й часу з `StartTime`/`EndTime` (пайп сам переводить у локальний час користувача).
- **Заголовки карток:** «Owner: Ім'я» / «Guest: Ім'я».

### Що саме на екрані (як у макеті)
- `<main>`, один `<h1>` «Group Pre-Order», бейдж, адреса, дата, час, «N of M guests».
- Жовта плашка «N participants haven't submitted their cart yet…» (N з `isSubmitted === false`), зникає, коли всі готові.
- `<ul>/<li>`, у кожному `<article>` картки: статус текстом «Submitted» / «Still choosing» + іконка, рядки страв (літера, назва, `× кількість`, ціна), «No dishes yet», Subtotal.
- Grand Total і зелена кнопка «Submit Pre-Order» (викликає `POST /group-reservations/{id}/submit`; після успіху тост і оновлення, під час запиту кнопка заблокована).
- Стани: завантаження, помилка з кнопкою «Retry», порожній список.
- `.scss` поруч, `styleUrl`, кольори лише через `@use … colors as color`, 375 px без горизонтального скролу.

### Що з архіву беремо, а що ні
- Беремо як довідку: тексти, структуру картки, ідеї тестів (`homework-tests/practice`, `level-1`).
- Готові тести з архіву написані під мок `GROUP_ORDER_PREVIEW` (Yevhenii, $74.50). Для реальних даних адаптую їх: ті самі перевірки (семантика, статус текстом, Subtotal, Grand Total), але з підставленими через мок HTTP-сервісів даними всередині тесту. Мок-файли в production-код не потрапляють.
- Скрипт `npm run test:hw` додаю в `package.json`, щоб він запускав лише ці spec-файли.

### Питання, які лишаю відкритими (є значення за замовчуванням)
- **Вхід на сторінку:** за замовчуванням додаю невелике посилання «Review group pre-order» в існуючу картку групової броні (де вже є invite-модалка), інакше сторінка досяжна тільки за URL. Скажіть, якщо це не потрібно.
- **Доступ:** маршрут під `AuthGuard` (`data: { authorized: true }`), бо сторінка тільки для власника.

Нічого не комічу й не пушу.

# Technical Design

### Current Implementation
- `restaurant-frontend-app` (Angular, standalone, SCSS). Route-level сторінки — у `pages/*`, наприклад `pages/guest-pre-order-page`; маршрути в `app.routes.ts` (є `join/:token`).
- `services/reservation/guest-invite.service.ts` вже має `getInviteToken$(reservationId)` і `getInvite$(token)`. Модель `models/reservations/guest-invite.model.ts` (`GuestInvite`, PascalCase) вже є.
- `services/guest-sessions/guest-sessions.service.ts`: методи для гостьової сесії (`createSession`, `getMenu`, `addDish`, `removeDish`, `submit`) з `sessionToken`; методів групи немає.
- `auth/token.interceptor.ts`: для URL з `/guest/` запит не змінюється (немає Authorization), тільки тости помилок. Для решти додається `idToken`.
- `AuthenticationService.getTokens()` повертає токени з `localStorage`.
- Бекенд: `RouteRegistry.cs` (`/guest/groups/{id}/sessions`, `/group-reservations/{id}/submit`), `ResponseBuilder.CreateJsonResponse` серіалізує в camelCase.

### Key Decisions
- **Реальні дані, жодних моків** у production-коді; `shared/mocks`, `?demo`-логін і `groupOrdersMockInterceptor` не переносяться.
- **Токен власника** додається явним заголовком у новому методі сервісу, `token.interceptor.ts` не змінюється.
- **Стан сторінки** — `signal`/`computed`, завантаження через `GuestInviteService` та `GuestSessionsService` у `switchMap`/`forkJoin` з `takeUntilDestroyed`; без нових `Subject`/`BehaviorSubject`.
- **Окремі презентаційні компоненти:** `ParticipantCardComponent` (`input.required`) і сторінка.
- **Модель відповіді сесій** у `models/group-orders/` (camelCase, як віддає бекенд).

### Data Models / Contracts
```ts
// models/group-orders/group-sessions-response.model.ts
export type ParticipantType = 'Owner' | 'Guest';
export interface ParticipantDish { id: string; name: string; price: number; }
export interface ParticipantItem { dish: ParticipantDish; quantity: number; }
export interface Participant { id: string | null; type: ParticipantType; name: string; items: ParticipantItem[]; total: number; isSubmitted: boolean; }
export interface GroupSessionsResponse { participants: Participant[]; total: number; }

// GuestSessionsService (новий метод)
getGroupSessions(groupReservationId: string) // GET /guest/groups/{id}/sessions, Authorization: Bearer idToken
// SubmitGroup (новий метод)
submitGroup(groupReservationId: string)      // POST /group-reservations/{id}/submit
```

### Proposed Changes
- Page: читає `reservationId` через `input()`/`ActivatedRoute` (за прийнятим у проєкті способом), завантажує invite і сесії, виставляє сигнали `invite`, `participants`, `total`, `status` (`loading|error|ready`), `submitting`.
- `pendingCount = computed(() => participants().filter(p => !p.isSubmitted).length)`; `guestsLabel = computed(...)` => `"{participants.length} of {MaxGuestCount} guests"`.
- Шаблон: `<main>` -> `<h1>` -> шапка -> плашка -> `<ul>` з `<li><app-participant-card [participant]="p" /></li>` -> Grand Total (`{{ total() | currency:'USD':'symbol':'1.2-2' }}`) -> кнопка Submit.
- Картка: `title = computed(...)` («Owner: …»/«Guest: …»), `<article>`, `<h2>`, статус «Submitted» / «Still choosing» + іконка (не лише колір), рядки страв з літерою, `× quantity`, ціна `dish.price * quantity` через `currency`, «No dishes yet», Subtotal з `participant.total`.
- Дата/час: `StartTime | date:'MMM d, y'`, `StartTime | date:'HH:mm'` – `EndTime | date:'HH:mm'`.
- Обробка помилок: 401/403/404 показуються тостами інтерсептора для `/guest/`; сторінка додатково показує стан помилки з «Retry».

### File Structure
- Нові: `src/app/pages/group-order-page/group-order-page.component.{ts,html,scss,spec.ts}`; `src/app/components/group-orders/participant-card/participant-card.component.{ts,html,scss,spec.ts}`; `src/app/models/group-orders/group-sessions-response.model.ts`.
- Змінені: `src/app/app.routes.ts` (маршрут `reservations/:reservationId/group-order`, `AuthGuard`, `loadComponent`), `src/app/services/guest-sessions/guest-sessions.service.ts` (+`getGroupSessions`, `submitGroup`), `package.json` (+`test:hw`), опційно картка групової броні (посилання).
- Не змінюються: `app.config.ts`, `token.interceptor.ts`, `environment*.ts`, auth-сервіси.

### Risks
- Бекенд може віддати 403, якщо користувач не власник броні: показуємо зрозумілий стан помилки.
- Група з однією людиною може не мати invite-токена/групи: показуємо стан помилки/порожній стан, не падаємо.
- Бюджет стилів 4 kB/компонент і ліміт бандла: SCSS компактний; перевірка в `npm run build`.
- Часовий пояс у `date`-пайпі: у тестах використовувати узгоджені `Date`/ISO-значення.

# Testing

### Validation Approach
Чотири перевірки з завдання: `npx tsc -p tsconfig.app.json --noEmit`, `npx tsc -p tsconfig.spec.json --noEmit`, `BACKEND_URL=https://example.com npm run build`, `npm run test:hw`. Додатково перевіряю семантику й ширину 375 px вручну, якщо в середовищі є браузер.

### Key Scenarios
- Сторінка викликає invite-token -> invite -> group sessions (HTTP-сервіси підмінені в тесті) і показує адресу, дату, час, «N of M guests».
- Рівно один `h1`, один `main`, `ul > li` на кожного учасника, `article` у кожному `li`.
- Статус тексту «Submitted» / «Still choosing».
- Grand Total береться з `total` відповіді через `currency`; Subtotal карток правильні.
- Плашка з правильним N і зникає, коли всі `isSubmitted`.
- `GuestSessionsService.getGroupSessions` додає `Authorization` з `idToken` і викликає правильний URL (spec з `HttpTestingController`).
- Submit викликає `POST /group-reservations/{id}/submit` і блокує кнопку під час запиту.

### Edge Cases
- Учасник без страв («No dishes yet»).
- Помилка завантаження (стан Retry), порожній список.
- Довга назва страви/адреса на 375 px без горизонтального скролу.

### Test Changes
- Нові spec: сторінка, картка, сервіс. Скрипт `test:hw` запускає лише їх. Тести з `green-and-tasty` використовуються як довідка й адаптуються під реальні дані.

# Delivery Steps

### ✓ Step 1: Add backend-facing models and service methods for group sessions
The frontend can load group participants and submit the group pre-order from the real API using the owner's token.

- Add `models/group-orders/group-sessions-response.model.ts` (`Participant`, `ParticipantItem`, `ParticipantDish`, `GroupSessionsResponse`) matching the camelCase backend response.
- Extend `GuestSessionsService` with `getGroupSessions(groupReservationId)` (explicit `Authorization: Bearer idToken` from `AuthenticationService.getTokens()`) and `submitGroup(groupReservationId)`.
- Do not touch `token.interceptor.ts`, `app.config.ts` or environments; no mocks.
- Add a spec for the service with `HttpTestingController` (URL, method, Authorization header).

### ✓ Step 2: Build ParticipantCardComponent
A reusable participant card renders one participant with accessible status, dish rows and Subtotal.

- Create `components/group-orders/participant-card/` (standalone, `input.required<Participant>()`, `computed` title «Owner: …» / «Guest: …»).
- Markup: `<article>`, `<h2>`, status as text + icon («Submitted» / «Still choosing»), dish rows with letter circle, name, `× quantity`, price via `currency`, «No dishes yet», Subtotal.
- SCSS next to it with `styleUrl`, colors via `@use … colors as color`, wrapping rows for 375 px.
- Add a card spec (title, dishes, subtotal, empty state, status text).

### ✓ Step 3: Build GroupOrderPageComponent with real data loading and route
`/reservations/:reservationId/group-order` shows the Owner Review screen from live backend data.

- Create `pages/group-order-page/` (standalone, `inject()`, signals/computed): load invite token -> invite -> group sessions via `GuestInviteService` and `GuestSessionsService` with `takeUntilDestroyed`; states loading/error (Retry)/ready.
- Markup: `<main>`, single `<h1>`, header (address, `date` pipe for date/time, «N of M guests»), yellow banner from `pendingCount`, `<ul>/<li>` with `app-participant-card`, Grand Total with `currency`, Submit Pre-Order button calling `submitGroup`.
- Register the route in `app.routes.ts` under the main layout with `AuthGuard` and `data: { authorized: true }`, `loadComponent`.
- Optionally add a link to the page from the existing group reservation card.
- SCSS for page matching `01-owner-review.png`, mobile-first, no horizontal scroll at 375 px.

### ✓ Step 4: Add page tests, test:hw script and run all four checks
All four checks pass and the work is ready to hand in.

- Write `group-order-page.component.spec.ts` with mocked services: semantics (one `h1`, `main`, `ul/li/article`), header, banner count, Grand Total, status text, error/Retry, submit.
- Add `test:hw` script to `package.json` that runs only the new specs with `--watch=false`.
- Run `npx tsc -p tsconfig.app.json --noEmit`, `npx tsc -p tsconfig.spec.json --noEmit`, `BACKEND_URL=https://example.com npm run build`, `npm run test:hw`; fix issues.
- Prepare a short submission description (what was done, screen states, AI usage, how verified) in chat.