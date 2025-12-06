-- Check how many rows will be affected
SELECT COUNT(*) as rows_to_update
FROM public."VenueImage"
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-venueimage%';

-- Preview the changes that will be made
SELECT 
    "Id",
    "Uri" as old_uri,
    REPLACE("Uri", 
        'sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-venueimage',
        'sportdeetssa.blob.core.windows.net/venue-image-football-ncaa'
    ) as new_uri
FROM public."VenueImage"
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-venueimage%';

-- Update the URIs to point to production storage account
UPDATE public."VenueImage"
SET "Uri" = REPLACE("Uri", 
    'sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-venueimage',
    'sportdeetssa.blob.core.windows.net/venue-image-football-ncaa'
)
WHERE "Uri" LIKE '%sportdeetssadev.blob.core.windows.net/dev-provider-footballncaa-venueimage%';

-- Verify the update
SELECT "Id", "Uri" 
FROM public."VenueImage"
LIMIT 10;
