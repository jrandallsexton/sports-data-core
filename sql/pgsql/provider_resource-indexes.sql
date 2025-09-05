select "LastPageIndex", "TotalPageCount", * from public."ResourceIndex" where "IsRecurring" is false order by "Ordinal"
select * from public."ResourceIndex" where "IsRecurring" is false and "LastCompletedUtc" is null order by "Ordinal"

--update public."ResourceIndex" set "ProcessingInstanceId" = null, "ProcessingStartedUtc" = null, "IsQueued" = false, "LastCompletedUtc" = null where "Id" = '8683b877-ca76-4c17-97ab-0fe3e8eec350'

select * from public."ResourceIndex" order by "Ordinal"
select * from public."ResourceIndexItem" where "ResourceIndexId" = '9e49039a-6844-4025-8429-dcb2888c12f3'
--delete from "ResourceIndex" where "Id" = 'e301f674-d3c6-4f74-b6f8-8f266b043810'
--update public."ResourceIndex" set "LastAccessedUtc" = null, "LastCompletedUtc" = null, "LastPageIndex" = null, "TotalPageCount" = 0 where "Id" = '735041ed-60f7-4baf-a7e9-2496276dcc4d'

--update public."ResourceIndex" set "Id" = '00000000-0000-0000-0000-000000000000' where "Id" = '99d91c6b-b58b-45fa-bcdd-75bc91784d1f'

--insert into public."ResourceIndex" ("Id", "Ordinal", "Name", "IsRecurring", "IsEnabled", "IsQueued",
--"Provider", "DocumentType", "SportId", "CreatedUtc", "CreatedBy", "Uri", "SourceUrlHash", "IsSeasonSpecific")
--values ('00000000-0000-0000-0000-000000000000', -1, 'Default', false, false, false,
--0, 0, 0, '2025-07-22 15:07:53.960671-04', '00000000-0000-0000-0000-000000000000', 'http://domain.none', '', false)

SHOW max_connections
SHOW config_file