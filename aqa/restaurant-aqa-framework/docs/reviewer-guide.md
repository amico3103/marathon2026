# Перевірка AQA-домашнього завдання / AQA homework review

**Мова / Language:** [Українською](#українською) · [English](#english)

## Українською

Це **Напрям 1, рівні 1–3** домашнього завдання: автоматичні перевірки
групового попереднього замовлення у власному розгорнутому застосунку
[Green & Tasty](https://d2hntky491rgpt.cloudfront.net). Підхід:
**BDD (Reqnroll/Gherkin) + Screenplay + Playwright для .NET + TUnit**.
Тести працюють зі справжніми інтерфейсом, API та AWS даними.

### Як перевірити за кілька хвилин

1. Відкрийте [публічний репозиторій](https://github.com/amico3103/marathon2026).
2. Відкрийте [успішний повний запуск AQA Tests](https://github.com/amico3103/marathon2026/actions/runs/41659883656): **45 passed, 0 failed, 0 skipped**. Нижче на сторінці запуску завантажте артефакт **aqa-group-preorder-report**. У ньому `index.html` з результатом кожного сценарію і `summary.json` з тими самими даними. GitHub може попросити увійти; артефакт зберігається 14 днів, після цього власник може повторити запуск.
3. Відкрийте [успішний AQA Quality](https://github.com/amico3103/marathon2026/actions/runs/41664083744): збірка, форматування, Gherkin lint і пошук 45 тестів. Повний тестовий запуск виконано на коміті `c5c343d`; наступні коміти змінювали лише документацію.
4. Перегляньте чотири файли сценаріїв: [рівень 1](../Features/GroupParticipants.feature) — 14 прикладів, [рівень 2](../Features/GroupSubmission.feature) — 15, [рівень 3](../Features/GroupGuestRemoval.feature) — 15, [наскрізний сценарій](../Features/GroupPreOrderJourney.feature) — 1. [Матриця покриття](coverage-matrix.md) зіставляє їх із пунктами ТЗ.

Тести автоматично створюють власні записи й прибирають їх. Після фінального
запуску перевірено: тимчасових бронювань, груп, сесій, замовлень і AQA
акаунтів не залишилося. Деталі, зокрема виправлення початкового збою CI,
є в [звіті про GitHub Actions](ci-and-reporting.md).

### Повторний запуск

Власник репозиторію може запустити **Actions → AQA Tests → Run workflow** із
типовими параметрами — це всі 45 сценаріїв. Потрібні GitHub Variables і
Secrets уже налаштовані; перевіряючому власні AWS ключі не потрібні.
Локально, після налаштування [`.env.example`](../.env.example) за
[README фреймворку](../README.md), у папці фреймворку достатньо однієї команди:

```bash
dotnet run -- --treenode-filter '/*/*/*/*[Category=group-preorder]' --report-trx
```

CI виконує тести послідовно. Повний паралельний прогін ще не підтверджено через
мережеві збої; [результати перевірки паралельності](repeatability-and-parallelism.md)
наведені окремо. Додаткове завдання з ReportPortal не виконувалося.

## English

This is **Direction 1, levels 1–3** of the AQA homework: automated checks of
group pre-orders in the deployed [Green & Tasty](https://d2hntky491rgpt.cloudfront.net)
app. The approach is **BDD (Reqnroll/Gherkin) + Screenplay + Playwright for
.NET + TUnit**. Tests use the live UI, API, and AWS data.

### Quick review

1. Open the [public repository](https://github.com/amico3103/marathon2026).
2. Check the [successful AQA Tests run](https://github.com/amico3103/marathon2026/actions/runs/41659883656): **45 passed, 0 failed, 0 skipped**. Download **aqa-group-preorder-report** from the run page and open `index.html` or `summary.json`. GitHub may require sign-in; the artifact is retained for 14 days, after which the owner can rerun the workflow.
3. Check [AQA Quality](https://github.com/amico3103/marathon2026/actions/runs/41664083744) for the build, formatting, Gherkin lint, and discovery of 45 tests. The complete test run used commit `c5c343d`; later commits changed documentation only.
4. Read the Gherkin features: [level 1](../Features/GroupParticipants.feature) (14 examples), [level 2](../Features/GroupSubmission.feature) (15), [level 3](../Features/GroupGuestRemoval.feature) (15), and the [end-to-end journey](../Features/GroupPreOrderJourney.feature) (1). The [coverage matrix](coverage-matrix.md) maps them to the assignment.

Each scenario creates and removes its own test data. The final AWS check found
no remaining temporary reservations, groups, sessions, orders, or AQA users.
The [CI notes](ci-and-reporting.md) explain the first failed run and its fix.

The repository owner can rerun all 45 tests with **Actions → AQA Tests → Run
workflow**. A reviewer needs no AWS credentials to inspect the existing run.
For a local run, configure [`.env.example`](../.env.example) as described in
the [framework README](../README.md), then run this from the framework directory:

```bash
dotnet run -- --treenode-filter '/*/*/*/*[Category=group-preorder]' --report-trx
```

CI uses sequential execution; a complete parallel
run remains unconfirmed because of network failures. The optional ReportPortal
task was not implemented.
