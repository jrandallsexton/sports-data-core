select "LastPageIndex", "TotalPageCount", * from public."ResourceIndex" where "IsRecurring" is false order by "Ordinal"
select * from public."ResourceIndex" where "IsRecurring" is false and "LastCompletedUtc" is null order by "Ordinal"
--update public."ResourceIndex" set "LastCompletedUtc" = null where "Id" = '8683b877-ca76-4c17-97ab-0fe3e8eec350'
select * from public."ResourceIndex" where "LastCompletedUtc" is null order by "Ordinal"
select * from public."ResourceIndexItem" where "SourceUrlHash" = '4619f7f84e26d8324bdd49fb011961a0b1a9a2bfc29b6da412166d3d4350bc3e'

--update public."ResourceIndex" set "LastAccessedUtc" = null, "LastCompletedUtc" = null, "LastPageIndex" = null, "TotalPageCount" = 0 where "Id" = '735041ed-60f7-4baf-a7e9-2496276dcc4d'

--update public."ResourceIndex" set "Id" = '00000000-0000-0000-0000-000000000000' where "Id" = '99d91c6b-b58b-45fa-bcdd-75bc91784d1f'

--insert into public."ResourceIndex" ("Id", "Ordinal", "Name", "IsRecurring", "IsEnabled", "IsQueued",
--"Provider", "DocumentType", "SportId", "CreatedUtc", "CreatedBy", "Uri", "SourceUrlHash", "IsSeasonSpecific")
--values ('00000000-0000-0000-0000-000000000000', -1, 'Default', false, false, false,
--0, 0, 0, '2025-07-22 15:07:53.960671-04', '00000000-0000-0000-0000-000000000000', 'http://domain.none', '', false)

SHOW max_connections
SHOW config_file