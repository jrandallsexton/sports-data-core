-- Check how many rows will be affected
SELECT COUNT(*) as rows_to_update
FROM public."FranchiseSeasonLogo"
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-teambyseasonlogo-%';

-- Preview the changes that will be made
SELECT 
    "Id",
    "Uri" as old_uri,
    REPLACE("Uri", 
        'sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-teambyseasonlogo-',
        'sportdeetssa.blob.core.windows.net/team-by-season-logo-football-ncaa-'
    ) as new_uri
FROM public."FranchiseSeasonLogo"
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-teambyseasonlogo-%';

-- Update the URIs to point to production storage account
UPDATE public."FranchiseSeasonLogo"
SET "Uri" = REPLACE("Uri", 
    'sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-teambyseasonlogo-',
    'sportdeetssa.blob.core.windows.net/team-by-season-logo-football-ncaa-'
)
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-teambyseasonlogo-%';

-- Verify the update
SELECT "Id", "Uri" 
FROM public."FranchiseSeasonLogo"
LIMIT 10;