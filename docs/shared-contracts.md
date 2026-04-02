# Shared Contracts

Черновик общего контракта на шаг 1. Любое изменение общих сущностей сначала фиксируется здесь, потом в коде.

## Базовые сущности

### Product
- `Id`
- `Sku`
- `Name`
- `CategoryId`
- `SupplierId`
- `LocationId`
- `Unit`
- `CurrentStock`
- `ReservedStock`
- `MinimumStock`
- `PurchasePrice`
- `SalePrice`
- `IsActive`
- `CreatedAtUtc`
- `UpdatedAtUtc`

### Category
- `Id`
- `Name`
- `ParentCategoryId`
- `Description`
- `IsActive`

### Supplier
- `Id`
- `Name`
- `ContactPerson`
- `Phone`
- `Email`
- `LeadTimeDays`
- `Notes`

### Location
- `Id`
- `Name`
- `ParentLocationId`
- `LocationType`
- `Code`
- `Capacity`
- `IsActive`

### User
- `Id`
- `Login`
- `DisplayName`
- `Role`
- `PasswordHash`
- `PasswordSalt`
- `IsActive`
- `CreatedAtUtc`
- `LastLoginAtUtc`

### StockMovement
- `Id`
- `ProductId`
- `MovementType`
- `Quantity`
- `DocumentNumber`
- `StockDocumentId`
- `ReservationId`
- `CreatedAtUtc`
- `BalanceAfter`
- `Comment`

### StockDocument
- `Id`
- `Number`
- `DocumentType`
- `Status`
- `SupplierId`
- `Comment`
- `CreatedAtUtc`
- `PostedAtUtc`
- `Items[]`

### StockDocumentItem
- `Id`
- `StockDocumentId`
- `ProductId`
- `Quantity`
- `UnitPrice`
- `Comment`

### StockReservation
- `Id`
- `ProductId`
- `Quantity`
- `Reference`
- `Comment`
- `CreatedAtUtc`
- `ReleasedAtUtc`
- `IsReleased`

### AuditLog
- `Id`
- `UserId`
- `ActionType`
- `EntityType`
- `EntityId`
- `Details`
- `CreatedAtUtc`

### InventorySession
- `Id`
- `Number`
- `Status`
- `Comment`
- `CreatedByUserId`
- `CompletedByUserId`
- `StartedAtUtc`
- `CompletedAtUtc`
- `Lines[]`

### InventorySessionLine
- `Id`
- `InventorySessionId`
- `ProductId`
- `ExpectedStock`
- `ActualStock`
- `Variance`
- `Comment`

### DiscrepancyReport
- `Id`
- `InventorySessionId`
- `Number`
- `CreatedAtUtc`
- `TotalItems`
- `TotalVariance`

### BackupEntry
- `Id`
- `FileName`
- `FullPath`
- `CreatedAtUtc`
- `CreatedByUserId`
- `RestoredAtUtc`
- `RestoredByUserId`

## UI-поля, которые нужны уже на шаге 2

- Для списка товаров: `Sku`, `Name`, `CategoryName`, `SupplierName`, `CurrentStock`, `MinimumStock`, `LocationName`
- Для карточки товара: все поля `Product` плюс справочники категорий, поставщиков и локаций
- Для документов: `DocumentNumber`, `Status`, `CreatedAtUtc`, `Items[]`, `Comment`
- Для шага 3 UI должен получать для документов еще и: `DocumentType`, `PostedAtUtc`, `TotalItems`, `TotalQuantity`
- Для журнала движений UI должен получать: `OccurredAtUtc`, `ProductName`, `Sku`, `MovementType`, `Quantity`, `BalanceAfter`, `DocumentNumber`, `Comment`
- Для проверки нехватки UI должен получать: `CurrentStock`, `ReservedStock`, `AvailableStock`
- Для шага 4 UI должен получать по пользователю: `Login`, `DisplayName`, `Role`, `IsActive`, `LastLoginAtUtc`
- Для шага 4 UI должен получать по инвентаризации: `Number`, `Status`, `StartedAtUtc`, `CompletedAtUtc`, `TotalItems`, `Lines[]`, `TotalVariance`
- Для шага 4 UI должен получать по журналу аудита: `CreatedAtUtc`, `UserDisplayName`, `ActionType`, `EntityType`, `EntityId`, `Details`
- Для шага 4 UI должен получать по backup: `FileName`, `CreatedAtUtc`, `CreatedByUser`, `RestoredAtUtc`, `RestoredByUser`

## Роли и доступ

- В системе существует ровно один пользователь с ролью `Admin`.
- `Admin` создается автоматически при инициализации БД.
- `Admin` может создавать и изменять только пользователей ролей `WarehouseOperator` и `Manager`.
- Создание второго `Admin` запрещено.
- `WarehouseOperator` выполняет все складские операции: справочники, приход, расход, резервирование, инвентаризация.
- `Manager` не выполняет складские изменения и резервируется под аналитику/дашборды.

## Авторизация шага 4

- Авторизация локальная, backend-only, по `Login` + `Password`.
- Учетные данные встроенного администратора захардкожены в коде и отдельно зафиксированы в README.
- Пароль хранится в БД не в открытом виде: сохраняются `PasswordHash` и `PasswordSalt`.
- После успешного входа backend хранит текущую сессию пользователя в приложении и использует ее для проверок прав и аудита.
