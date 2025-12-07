-- Check how many rows will be affected
SELECT COUNT(*) as rows_to_update
FROM public."AthleteImage"
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-athleteimage/%';

-- Preview the changes that will be made
SELECT 
    "Id",
    "Uri" as old_uri,
    REPLACE("Uri", 
        'sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-athleteimage/',
        'sportdeetssa.blob.core.windows.net/athlete-image-football-ncaa/'
    ) as new_uri
FROM public."AthleteImage"
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-athleteimage/%';

-- Update the URIs to point to production storage account
UPDATE public."AthleteImage"
SET "Uri" = REPLACE("Uri", 
    'sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-athleteimage/',
    'sportdeetssa.blob.core.windows.net/athlete-image-football-ncaa/'
)
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-athleteimage/%';

-- Verify the update
SELECT "Id", "Uri" 
FROM public."AthleteImage"
LIMIT 10;
