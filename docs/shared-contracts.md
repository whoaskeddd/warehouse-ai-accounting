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
- `IsActive`

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

## UI-поля, которые нужны уже на шаге 2

- Для списка товаров: `Sku`, `Name`, `CategoryName`, `SupplierName`, `CurrentStock`, `MinimumStock`, `LocationName`
- Для карточки товара: все поля `Product` плюс справочники категорий, поставщиков и локаций
- Для документов: `DocumentNumber`, `Status`, `CreatedAtUtc`, `Items[]`, `Comment`
- Для шага 3 UI должен получать для документов еще и: `DocumentType`, `PostedAtUtc`, `TotalItems`, `TotalQuantity`
- Для журнала движений UI должен получать: `OccurredAtUtc`, `ProductName`, `Sku`, `MovementType`, `Quantity`, `BalanceAfter`, `DocumentNumber`, `Comment`
- Для проверки нехватки UI должен получать: `CurrentStock`, `ReservedStock`, `AvailableStock`
