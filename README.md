# Електронні таблиці 🐺

Лабораторна робота №1 (та №1*) — програма для роботи з електронними таблицями: аналіз і обчислення виразів у клітинках, збереження локально та на Google Drive. Платформа — **.NET MAUI** (Windows), інтерфейс україномовний.

## Можливості

- Таблиця з довільною кількістю рядків і стовпців (додавання / видалення).
- Клітинки містять вирази: цілі числа довільної довжини (`BigInteger`), посилання на клітинки (`A1`, `AB12`), дужки, операції та функції.
- Два режими відображення: **Вирази** та **Значення**.
- Синтаксичний аналіз через **ANTLR 4** (граматика [`CellFormula.g4`](SpreadsheetApp.Core/Grammar/CellFormula.g4)) з локалізацією помилки (позиція в тексті).
- Граф залежностей: після зміни клітинки перераховуються всі залежні; циклічні посилання виявляються.
- Збереження у JSON: локально (діалоги Провідника) або на **Google Drive** (лабораторна №1*).
- Довідка в програмі, підтвердження перед втратою незбережених змін.

### Операції та функції (всі варіанти)

| Пріоритет | Операції |
|---|---|
| найвищий | `( )`, числа, клітинки, функції |
| | `x ^ y` (правоасоціативне) |
| | унарні `+x`, `-x` |
| | `*`, `/`, `div`, `mod` |
| | `+`, `-` |
| | `=`, `<>`, `<`, `<=`, `>`, `>=` |
| | `not` |
| | `and` |
| | `or` |
| найнижчий | `eqv` |

Функції: `inc(x)`, `dec(x)`, `max(x, y)`, `min(x, y)`, `mmax(x1, …, xN)`, `mmin(x1, …, xN)`.
Логічні значення: `TRUE` / `FALSE`.

### Коди помилок

| Код | Значення |
|---|---|
| `#SYNTAX!` | синтаксична помилка |
| `#VALUE!` | невідповідність типів |
| `#DIV/0!` | ділення на нуль |
| `#CYCLE!` | циклічне посилання |
| `#REF!` | клітинка поза таблицею або з помилкою |
| `#NUM!` | недопустимий показник степеня |

## Структура рішення

```
SpreadsheetApp.sln
├── run.cmd, publish.cmd        запуск і збірка без Visual Studio
├── SpreadsheetApp.Core/        бібліотека: логіка без UI
│   ├── Grammar/CellFormula.g4  граматика ANTLR
│   ├── Parsing/                FormulaParser, AstBuilder, Expr (AST), Evaluator, DependencyCollector, CalcException
│   ├── Model/                  Spreadsheet, Cell, CellAddress, Value, DependencyGraph
│   └── Storage/                IFileStorage, LocalFileStorage, GoogleDriveStorage, SpreadsheetSerializer
├── SpreadsheetApp/             застосунок .NET MAUI (MVVM)
│   └── UI/                     MainPage, HelpPage, SpreadsheetViewModel, DisplayMode, діалоги
└── SpreadsheetApp.Tests/       юніт-тести xUnit
```

## Запуск

### Без Visual Studio

Потрібен лише [.NET 10 SDK](https://dotnet.microsoft.com/download) і робоче навантаження MAUI для Windows:

```
dotnet workload install maui-windows
```

- **`run.cmd`** — зібрати й одразу запустити програму.
- **`publish.cmd`** — зібрати самодостатню теку `publish\` з `SpreadsheetApp.exe`. Її можна скопіювати на будь-який ПК з Windows 10/11 x64 і запускати без .NET, Visual Studio чи інсталяції.

Те саме з командного рядка:

```
dotnet run --project SpreadsheetApp -c Release
dotnet publish SpreadsheetApp -c Release -r win-x64 --self-contained -o publish
```

### У Visual Studio

Відкрити `SpreadsheetApp.sln`, вибрати ціль **Windows Machine** і натиснути F5.

Під час першої збірки `Antlr4BuildTasks` сам завантажить Java та ANTLR-генератор (потрібен інтернет).

Тести: `dotnet test SpreadsheetApp.Tests`.

## Google Drive

1. У [Google Cloud Console](https://console.cloud.google.com) створити проєкт і увімкнути **Google Drive API**.
2. Налаштувати OAuth consent screen (тип External) і додати свою пошту в **Test users**.
3. Створити OAuth client ID типу **Desktop app** і завантажити JSON.
4. У програмі увімкнути перемикач «Google Drive» → «Вибрати файл» → вибрати завантажений JSON.

Файл `client_secret.json` зберігається локально і **не потрапляє в репозиторій** (див. `.gitignore`).
