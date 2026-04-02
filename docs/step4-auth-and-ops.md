# Step 4 Backend Notes

## Hardcoded admin

- Login: `admin`
- Password: `Admin123!`
- Role: `Admin`

The application must keep exactly one admin account in the system. It is seeded automatically during database initialization.

## Default non-admin users

- Warehouse operator: `operator` / `Operator123!`
- Manager: `manager` / `Manager123!`

These users are seeded automatically if they do not exist yet and are intended for normal application startup.

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

## Startup login flow

- Application startup shows a login window instead of auto-login.
- Normal users sign in as `manager` or `operator`.
- Admin sign-in is blocked by default in the UI.
- Developers can temporarily allow admin sign-in by setting environment variable `SMARTSTOCKAI_ENABLE_ADMIN_LOGIN=1` before launching the app.
- User management remains admin-only.

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
