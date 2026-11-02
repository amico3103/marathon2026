---
sessionId: session-261029-102550-u4rg
---

# Plan in plain words

### Що я прочитав
- `frontend/homework-design/README.md` і три макети: `01-owner-review.png` (наш екран), `02-sent-to-restaurant.png` та `03-remove-guest.png` (для 2-го та 3-го рівнів, зараз не чіпаю).
- `restaurant-frontend-app/CLAUDE.md` (конвенції), кольори `shared/styles/abstracts/_colors.scss`, маршрути `app.routes.ts`, `package.json`, модель відповіді бекенду (`ApiRoutes.md`, `GetGroupReservationResponse.cs`).

### Що я виявив — важливо
У репозиторії **немає** того, на що посилається завдання як на «практичне»:
- немає константи `GROUP_ORDER_PREVIEW`;
- немає сторінки/компонента Owner Review і маршруту `/reservations/:id/group-order`;
- немає скрипта `npm run test:hw` (є лише `ng test`) і файлу `homework-frontend.md`.

Тому я створю це з нуля, у стилі проєкту. Якщо у тебе є «практичне» в іншій гілці — скажи, і я підлаштуюсь.

### Що зроблю (простими словами)
1. **Статичні дані.** Файл з типами та константою `GROUP_ORDER_PREVIEW`: `invite` (`LocationAddress`, `StartTime`, `EndTime`, `MaxGuestCount`, статус-бейдж) і `sessions` (список учасників: «My Pre-Order», Maria, Alex, Julia; у кожного страви з назвою, кількістю, нотаткою, ціною, `total`, `isSubmitted`; загальний `total` = 188). Суми беру з макета: 68.50 + 50 + 37.50 + 32 = 188.
2. **Сторінка.** Standalone-компонент у `pages/group-order-page/` (за правилом «route-level екран → pages/»), `inject()`/`signal()`/`computed()`, без `Subject`/HTTP. Маршрут `/reservations/:id/group-order` в `app.routes.ts`.
3. **Шапка.** `<h1>Group Pre-Order</h1>` + бейдж, адреса, дата (пайп `date`, напр. `MMM d, y`), час (`HH:mm – HH:mm`), «4 of 5 guests» — усе з `invite`, з іконками.
4. **Жовта плашка** «N participants haven't submitted their cart yet…» — N рахується з `isSubmitted === false`.
5. **Картки учасників.** `<main>` → `<ul>` → `<li>` → `<article>` на кожного. У картці: заголовок («My Pre-Order» / «Guest: Alex»), рядки страв (зелене коло з першою літерою, назва, нотатка, `× 2`, ціна), Subtotal. Кнопок «−/+», кошика та «Edit my Pre-Order» немає, бо їх виключено з домашки. «Remove Guest» — як неактивна/декоративна на цьому рівні.
6. **Статус кошика не лише кольором:** текст «Submitted» / «Still choosing» + іконка (✓ / …).
7. **Grand Total** — `GROUP_ORDER_PREVIEW.sessions.total` через пайп `currency`, зеленим; нижче широка зелена кнопка «Submit Pre-Order» (без HTTP).
8. **Стилі.** `.scss` поруч із компонентом, `styleUrl`, кольори лише через `@use 'app/shared/styles/abstracts/colors' as color;` (без `@import`). Адаптив: мобільний-first, на 375 px ряд страви переноситься, нема горизонтального скролу.
9. **Тести й скрипт `test:hw`.** Додаю spec для сторінки (одна `<h1>`, `<main>`, 4 `<li>`/`<article>`, текст статусу, Grand Total, шапка з форматованою датою) і скрипт `test:hw` у `package.json`, що запускає лише тести домашки (`ng test --watch=false --include=<hw spec>`).
10. **Перевірки перед здачею:** `npx tsc -p tsconfig.app.json --noEmit`, `npx tsc -p tsconfig.spec.json --noEmit`, `BACKEND_URL=https://example.com npm run build` (має закінчитись `Bundle check passed`), `npm run test:hw`. Скріни на 1280 px і 375 px зроблю, якщо в середовищі є браузер.
11. **Опис для здачі** (що зроблено, стани екрана, AI так/ні, як перевірив) — запропоную текстом у чаті, у репозиторій не комітжу.

### Припущення (скажи, якщо не так)
- Фото страв: у макеті є фото для частини страв; використаю наявні картинки з `assets/`, а якщо їх немає — літера в зеленому колі для всіх.
- Ціни форматую `currency:'USD'` з `1.2-2` ($34.00), бо пайп не вміє «без копійок лише для цілих».
- Бейдж у шапці — «Created New Pre-Order», як на макеті.
- Нічого не комічу і не пушу (за CLAUDE.md), лише змінюю файли.

# Technical Design

### Current Implementation
- Angular 22, standalone, SCSS, Karma/Jasmine; `restaurant-frontend-app/src/app`.
- Route-level сторінки: `pages/*` (напр. `reservation-dashboard`), реєстрація в `app.routes.ts` (є `AuthGuard` з `data: { authorized: true }`).
- Кольори: `shared/styles/abstracts/_colors.scss` (`$green-100/300/400/600/800`, `$green-gradient`, `$neutral-*`, `$bg-yellow`, `$orange-100/400/600`). Типографіка: Poppins.
- Бекенд-контракт сесій: `{ participants: [{ id, type, name, items, total, isSubmitted }], total }`; invite: `locationAddress`, `startTime`, `endTime`, `maxGuestCount`.
- `GROUP_ORDER_PREVIEW`, `test:hw`, сторінка group-order — відсутні.

### Key Decisions
- Нова сторінка в `pages/group-order-page/`, а не в `components/`, за decision table з CLAUDE.md.
- Один компонент сторінки + маленький презентаційний `participant-card` (`input()` для учасника) — картка = `<article>`; це відповідає вимозі `input()`/`output()`.
- Дані — `readonly` константа, у компоненті `signal`/`computed` (`pendingCount`, `guestsLabel`); жодного `HttpClient`, жодних нових `Subject`.
- Інтерфейси в окремому `models.ts` поруч з компонентом (правило «один інтерфейс-файл», не в `.component.ts`).

### Data Models / Contracts
```ts
export interface GroupOrderItem { id: string; name: string; quantity: number; price: number; note?: string; imageUrl?: string; }
export interface GroupOrderParticipant { id: string; type: 'Owner' | 'Guest'; name: string; items: GroupOrderItem[]; total: number; isSubmitted: boolean; }
export interface GroupOrderInvite { LocationAddress: string; StartTime: string; EndTime: string; MaxGuestCount: number; Status: string; }
export interface GroupOrderPreview { invite: GroupOrderInvite; sessions: { participants: GroupOrderParticipant[]; total: number } }
```
Імена полів `invite` — як у завданні (PascalCase).

### File Structure
- `src/app/pages/group-order-page/group-order-page.component.{ts,html,scss,spec.ts}` — новий
- `src/app/pages/group-order-page/participant-card/participant-card.component.{ts,html,scss}` — новий
- `src/app/pages/group-order-page/models.ts`, `group-order-preview.mock.ts` — нові
- `src/app/app.routes.ts` — додати маршрут `reservations/:id/group-order`
- `package.json` — скрипт `test:hw`

### Components
- `GroupOrderPageComponent`: `<main>`, `<h1>`, шапка, плашка, `<ul>` з `<li><app-participant-card/></li>`, Grand Total (`| currency:'USD'`), кнопка Submit.
- `ParticipantCardComponent`: `participant = input.required<GroupOrderParticipant>()`; `<article>`, `<h2>`, `<ul>` страв, статус-текст, Subtotal.
- Дата: `{{ invite.StartTime | date:'MMM d, y' }}`, час: `{{ invite.StartTime | date:'HH:mm' }} – {{ invite.EndTime | date:'HH:mm' }}`.

### Risks
- Бюджет стилів 4 kB/компонент (warning) і бандл 1 MB — SCSS тримаю компактним.
- Часовий пояс у `date`-пайпі: у тестах не залежати від TZ (задавати час через локальний `new Date`/UTC-узгоджено).
- Неіснуючі `test:hw` і мок — створюю самостійно; можливе розходження з очікуваннями перевіряльника, тому питання винесено в «Припущення».

# Testing

### Validation Approach
Чотири перевірки з завдання + ручна перевірка семантики й ширини 375 px.

### Key Scenarios
- Рівно один `h1`, один `main`, `ul > li` на 4 учасників, `article` у кожному `li`.
- Шапка: адреса, відформатована дата й час, «4 of 5 guests».
- Grand Total = «$188.00»; Subtotal кожної картки.
- Статус кошика має текст «Submitted»/«Still choosing».
- Плашка показує правильне N.

### Edge Cases
- Учасник без страв, усі `isSubmitted` (плашка зникає).
- Довга назва страви/адреса на 375 px — без горизонтального скролу.

### Test Changes
- Новий `group-order-page.component.spec.ts` (+ можливо spec картки); скрипт `test:hw` запускає лише їх.

# Delivery Steps

###   Step 1: Create static data, models and the group-order page skeleton with route
The `/reservations/:id/group-order` route opens a page that renders semantic markup from static `GROUP_ORDER_PREVIEW`.

- Add `models.ts` and `group-order-preview.mock.ts` in `pages/group-order-page/` (invite + sessions with Owner, Maria, Alex, Julia; total 188).
- Create standalone `GroupOrderPageComponent` with `signal`/`computed` state, `<main>`, a single `<h1>`, `<ul>`/`<li>` participant list.
- Register the route in `app.routes.ts`.
- No HTTP and no new Subject/BehaviorSubject.

###   Step 2: Build the header, notification banner and participant cards
The page shows the header, the yellow banner and participant cards with dish rows and Subtotals, all driven by static data.

- Header: «Group Pre-Order», status badge, address, date via `date` pipe, time range, «4 of 5 guests» from `invite` (with icons).
- Banner: «N participants haven't submitted their cart yet…» computed from `isSubmitted`.
- `ParticipantCardComponent` (`<article>`, `input.required`) with dish rows: letter/photo circle, name, note, `× qty`, price, Subtotal.
- Basket status as text + icon: «Submitted» / «Still choosing».

###   Step 3: Add Grand Total, Submit button and SCSS styling with 375 px responsiveness
The screen visually matches `01-owner-review.png` and does not break at 375 px.

- Grand Total from `GROUP_ORDER_PREVIEW.sessions.total` with the `currency` pipe, green; wide green Submit Pre-Order button.
- `.scss` next to each component with `styleUrl`; colors only via `@use 'app/shared/styles/abstracts/colors' as color;`.
- Mobile-first layout, wrapping rows, no horizontal scroll at 375 px; keep stylesheet under the 4 kB budget.

###   Step 4: Add homework tests, test:hw script and run all four checks
All four checks pass and the result is ready to hand in.

- Write `group-order-page.component.spec.ts` covering semantics, header, Grand Total, status text, banner count.
- Add `test:hw` script to `package.json` that runs only the homework specs with `--watch=false`.
- Run `npx tsc -p tsconfig.app.json --noEmit`, `npx tsc -p tsconfig.spec.json --noEmit`, `BACKEND_URL=https://example.com npm run build`, `npm run test:hw`; fix issues.
- Prepare a short submission description (what was done, screen states, AI usage, how verified) in chat.