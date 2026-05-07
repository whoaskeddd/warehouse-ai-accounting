# CRUD Services

Этот документ фиксирует, где в проекте находятся CRUD-операции для шага 2 и как ими пользоваться дальше.

## Где должны лежать CRUD-операции

В текущей структуре проекта используем такой принцип:

- `Core/Entities` -> сущности БД
- `Core/Contracts/<Entity>` -> DTO, request-модели и интерфейс сервиса
- `Data/Services` -> реализация CRUD через `AppDbContext`
- `App` -> только вызов сервисов из UI, без прямой работы с `DbContext`

## Реализованные CRUD-сервисы

### User

Контракт:

- `src/SmartStockAI.Core/Contracts/Users/IUserService.cs`

Реализация:

- `src/SmartStockAI.Data/Services/UserService.cs`

Методы:

- `GetAllAsync`
- `GetByIdAsync`
- `CreateAsync`
- `UpdateAsync`
- `DeleteAsync`

### Product

Контракт:

- `src/SmartStockAI.Core/Contracts/Products/IProductService.cs`

Реализация:

- `src/SmartStockAI.Data/Services/ProductService.cs`

Особенности:

- проверка уникальности `Sku`
- проверка существования `Category`, `Supplier`, `Location`
- удаление запрещено, если у товара уже есть `StockMovement`

### Category

Контракт:

- `src/SmartStockAI.Core/Contracts/Categories/ICategoryService.cs`

Реализация:

- `src/SmartStockAI.Data/Services/CategoryService.cs`

Особенности:

- поддержка `ParentCategoryId`
- запрет self-reference
- удаление запрещено, если есть дочерние категории
- удаление запрещено, если категория используется в товарах

### Supplier

Контракт:

- `src/SmartStockAI.Core/Contracts/Suppliers/ISupplierService.cs`

Реализация:

- `src/SmartStockAI.Data/Services/SupplierService.cs`

Особенности:

- удаление запрещено, если поставщик используется в товарах

### Location

Контракт:

- `src/SmartStockAI.Core/Contracts/Locations/ILocationService.cs`

Реализация:

- `src/SmartStockAI.Data/Services/LocationService.cs`

Особенности:

- поддержка `ParentLocationId`
- запрет self-reference
- удаление запрещено, если есть дочерние локации
- удаление запрещено, если локация используется в товарах

## Что должен делать UI-разработчик

UI не должен работать с `DbContext` напрямую.

UI должен вызывать сервисы:

- `IUserService`
- `IProductService`
- `ICategoryService`
- `ISupplierService`
- `ILocationService`

## Что делать дальше для шага 2

Следующий логичный этап:

1. подключить сервисы через DI
2. сделать ViewModel/Pages для списков
3. сделать формы create/edit
4. повесить delete с подтверждением и обработкой ошибок

## Минимальный шаблон для новых сущностей

Если добавляется новая сущность, повторять тот же шаблон:

1. `Entity` в `Core/Entities`
2. `Dto` в `Core/Contracts/<Entity>`
3. `CreateRequest` в `Core/Contracts/<Entity>`
4. `UpdateRequest` в `Core/Contracts/<Entity>`
5. `I<Entity>Service` в `Core/Contracts/<Entity>`
6. `<Entity>Service` в `Data/Services`
