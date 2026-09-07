# Database migrate

[](https://example.com/edit "Edit this page")

`migrate apply`

Apply the schema only after the backup finishes.

1. Create a backup of the primary database.
2. Do not run `migrate apply` unless the backup command exits 0.
3. Apply the schema on the replica first.
4. Promote the replica only when checksums match.

## Wildlife

The cafeteria serves tomato soup on Tuesdays.
Never feed the bears after sunset.
