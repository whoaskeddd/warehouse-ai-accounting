# AGENTS.md

## Основное правило

В репозитории используется только один файл решения:

- `SmartStockAI.sln`

Файл `*.slnx` не использовать, не создавать и не коммитить.

## Единый стек проекта

Для всей команды зафиксированы такие версии:

- SDK: `.NET 10`
- WPF приложение: `net10.0-windows`
- Class Library проекты: `net10.0`
- Тесты: `net10.0`

Текущее соглашение по проектам:

- `src/SmartStockAI.App` -> `net10.0-windows`
- `src/SmartStockAI.Core` -> `net10.0`
- `src/SmartStockAI.Data` -> `net10.0`
- `tests/SmartStockAI.Tests` -> `net10.0`

Нельзя локально переводить отдельный проект на `net8.0`, `net9.0` или другую версию без синхронного изменения всей команды.

## Какой файл открывать

Открывать в Visual Studio нужно:

- `SmartStockAI.sln`

Не открывать:

- отдельный `.csproj` как основной рабочий вход;
- `*.slnx`.

## Версии основных пакетов

Используем согласованные major/minor версии:

- `Microsoft.EntityFrameworkCore` -> `10.0.5`
- `Microsoft.EntityFrameworkCore.Sqlite` -> `10.0.5`
- `Microsoft.EntityFrameworkCore.Design` -> `10.0.5`
- `Microsoft.Extensions.Configuration.Json` -> `10.0.5`
- `Microsoft.Extensions.DependencyInjection` -> `10.0.5`
- `Microsoft.Extensions.Hosting` -> `10.0.5`
- `CommunityToolkit.Mvvm` -> `8.4.2`
- `Microsoft.NET.Test.Sdk` -> `17.14.1`
- `xunit` -> `2.9.3`
- `xunit.runner.visualstudio` -> `3.1.4`
- `FluentAssertions` -> `8.9.0`

Если обновляется один пакет из семейства `Microsoft.Extensions.*` или `EntityFrameworkCore`, нужно обновлять остальные согласованно, а не выборочно.

## Структура решения

В решении должны быть всегда:

- `SmartStockAI.App`
- `SmartStockAI.Core`
- `SmartStockAI.Data`
- `SmartStockAI.Tests`

Если какой-то проект пропал из solution, его нужно сразу вернуть в `SmartStockAI.sln`.

## Ссылки между проектами

Должны оставаться такими:

- `SmartStockAI.App` -> `SmartStockAI.Core`
- `SmartStockAI.App` -> `SmartStockAI.Data`
- `SmartStockAI.Data` -> `SmartStockAI.Core`
- `SmartStockAI.Tests` -> `SmartStockAI.Core`
- `SmartStockAI.Tests` -> `SmartStockAI.Data`

Нельзя удалять эти ссылки без явной договоренности.

## Правила для коммитов, влияющих на инфраструктуру

Если меняется что-то из списка ниже, это нужно отдельно написать друг другу:

- `TargetFramework`
- `csproj`
- `NuGet` пакеты
- solution-файл
- `EF Core` миграции
- структура папок `src/` и `tests/`

Такие изменения не должны попадать в коммит “между делом”.

## Что делать перед началом работы

1. Открыть `SmartStockAI.sln`
2. Убедиться, что все 4 проекта есть в Solution Explorer
3. Проверить target framework в `.csproj`
4. Убедиться, что работа идет не в `slnx`

## Что делать перед коммитом

1. Проверить, что не создан новый `*.slnx`
2. Проверить, что не изменены версии `.NET` случайно
3. Проверить, что solution все еще содержит все проекты
4. Если менялись пакеты или framework, написать это в сообщении коммита

## Запрещено

- создавать новый `slnx`
- переводить один проект на другую версию `.NET`, оставляя остальные на старой
- удалять проекты из `SmartStockAI.sln`
- обновлять пакеты хаотично только в одном проекте

## Кратко

Единая точка входа:

- `SmartStockAI.sln`

Единая версия платформы:

- `.NET 10`

Единое правило:

- любые изменения solution, framework и пакетов сначала синхронизируются между разработчиками, потом коммитятся
