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
- `OccurredAtUtc`
- `CreatedByUserId`
- `Comment`

## UI-поля, которые нужны уже на шаге 2

- Для списка товаров: `Sku`, `Name`, `CategoryName`, `SupplierName`, `CurrentStock`, `MinimumStock`, `LocationName`
- Для карточки товара: все поля `Product` плюс справочники категорий, поставщиков и локаций
- Для документов: `DocumentNumber`, `Status`, `CreatedAtUtc`, `Items[]`, `Comment`
