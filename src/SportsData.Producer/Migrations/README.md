# EF Core Migrations - Multi-Sport Organization

## Overview

Migrations are organized by sport to support parallel development across different sports (Football, Baseball, Basketball, Golf, etc.). Each sport has its own DbContext and maintains separate migration histories.

## Folder Structure

```
Migrations/
├── Football/
│   ├── 20260202101027_02FebV1_Baseline.cs
│   ├── 20260202101027_02FebV1_Baseline.Designer.cs
│   └── FootballDataContextModelSnapshot.cs
├── Baseball/
│   └── [future baseball migrations]
├── Basketball/
│   └── [future basketball migrations]
└── Golf/
    └── [future golf migrations]
```

## Creating New Migrations

### Football Migrations

```powershell
cd C:\Projects\sports-data\src\SportsData.Producer

dotnet ef migrations add MigrationName `
    --context FootballDataContext `
    --output-dir Migrations/Football
```

### Baseball Migrations (Future)

```powershell
dotnet ef migrations add MigrationName `
    --context BaseballDataContext `
    --output-dir Migrations/Baseball
```

### Basketball Migrations (Future)

```powershell
dotnet ef migrations add MigrationName `
    --context BasketballDataContext `
    --output-dir Migrations/Basketball
```

### Golf Migrations (Future)

```powershell
dotnet ef migrations add MigrationName `
    --context GolfDataContext `
    --output-dir Migrations/Golf
```

## Applying Migrations

Migrations are automatically applied at startup via `ApplyMigrations<T>()` in `Program.cs`, where `T` is the sport-specific DbContext.

### Manual Migration Application

```powershell
# Football
dotnet ef database update --context FootballDataContext

# Baseball (future)
dotnet ef database update --context BaseballDataContext
```

## Database Naming Convention

Databases follow the pattern: `sd{Application}.{Sport}`

Examples:
- `sdProducer.FootballNcaa`
- `sdProducer.FootballNfl`
- `sdProducer.BaseballMlb` (future)
- `sdProducer.BasketballNba` (future)

## Important Notes

1. **Namespace**: All migrations in a sport folder must use the namespace `SportsData.Producer.Migrations.{Sport}`
   - Example: `namespace SportsData.Producer.Migrations.Football`

2. **Migration History**: Each sport maintains its own `__EFMigrationsHistory` table in its respective database

3. **Parallel Development**: Multiple developers can work on different sports simultaneously without migration conflicts

4. **Production Deployment**: 
   - Producer pods run in "mode" (Sport enum: FootballNcaa, BaseballMlb, etc.)
   - Only the relevant DbContext migrations are applied at startup
   - Separate pods for separate sports (producer-football-ncaa, producer-baseball-mlb, etc.)

## Migration Squashing

When migrating to a baseline (like 02FebV1_Baseline for Football):
1. Backup production database
2. Create baseline migration in appropriate sport folder
3. Test locally
4. Delete old migrations
5. Update `__EFMigrationsHistory` in production to single baseline record

See [HISTORICAL_SOURCING_READINESS_CHECKLIST.md](../../../docs/HISTORICAL_SOURCING_READINESS_CHECKLIST.md) for detailed squash procedures.

## Future Sports

When adding a new sport:
1. Create DbContext (e.g., `BaseballDataContext`)
2. Add to `DataContextFactory.Resolve()`
3. Register in `Program.cs` DI
4. Create first migration with `--output-dir Migrations/Baseball`
5. Update this README with baseball-specific commands
