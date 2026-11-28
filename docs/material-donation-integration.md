# Material Donation Integration

Member 05 provides confirmed material donations through:

GET /api/material-donations/intake

Member 03 must use the material item ID as the unique
inventory intake reference.

Inventory quantities must increase only after logistics
intake approval.

The inventory update and material item linkage must be
saved in the same database transaction.

After successful inventory intake, update:

- InventoryResourceId
- InventoryReceivedAt

The same material item must not be received twice.