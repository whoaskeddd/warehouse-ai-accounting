# Step 4 Backend Notes

## Hardcoded admin

- Login: `admin`
- Password: `Admin123!`
- Role: `Admin`

The application must keep exactly one admin account in the system. It is seeded automatically during database initialization.

## Role model

- `Admin`
  - Creates and updates users
  - Can only create non-admin users
  - Can view audit log
  - Can run backup and restore
- `WarehouseOperator`
  - Can perform all warehouse mutations
  - Works with products, categories, suppliers, locations, stock documents, reservations, inventory
- `Manager`
  - No warehouse mutations
  - Reserved for analytics and dashboards in later steps

## Backend authorization

- Backend uses local login/password authentication.
- Successful login sets the current application user in the backend session context.
- Mutating services validate the current role before executing business logic.
- Critical operations are written to the audit log with current user id and timestamp.
- Until a dedicated login UI is implemented, application startup performs backend auto-login with the hardcoded admin account so existing WPF screens can continue to work.

## Step 4 critical audit actions

- Product created
- Product updated
- Receipt posted
- Issue posted
- Inventory completed
- Backup created
- Backup restored
- User created
- User updated
- User deleted
