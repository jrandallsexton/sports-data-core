select * from public."SeasonPoll" where "SeasonYear" = 2026;

-- Id	Name	ShortName	Slug	SeasonYear	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- a82a679f-2f15-98bf-026c-6c7fe15d290e	FCS Coaches Poll	FCS Coaches Poll	fcs	2026	2026-08-17 21:42:04.519495+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL
-- ac65cf00-057e-0c6f-e6fa-74661fb5b0ed	AFCA Coaches Poll	AFCA Coaches Poll	usa	2026	2026-08-17 21:42:04.819644+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL
-- c07a929b-491c-4f11-b7bc-84c12316e5ad	AP Top 25	AP Poll	ap	2026	2026-08-17 21:42:04.890471+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL
-- 909da9ca-5d7a-63e2-04c3-c4e87dcaf7dc	AFCA Division II Coaches Poll	AFCA Div II	afca	2026	2026-08-30 22:04:01.023451+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL
-- fce2f909-f522-6310-0d98-a40ad523ac15	AFCA Division III Coaches Poll	AFCA Div III	afca	2026	2026-08-30 22:04:01.078878+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL

select * from public."SeasonPollWeek" where "SeasonPollId" = 'c07a929b-491c-4f11-b7bc-84c12316e5ad'; -- AP Poll 2026

select sw."Number" as "SeasonWeek", spw.*
from public."SeasonPollWeek" spw
inner join public."SeasonWeek" sw on sw."Id" = spw."SeasonWeekId"
where spw."SeasonPollId" = 'c07a929b-491c-4f11-b7bc-84c12316e5ad' -- AP Poll 2026
order by "SeasonWeek";

-- SeasonWeek	Id	SeasonPollId	SeasonWeekId	OccurrenceNumber	OccurrenceType	OccurrenceIsLast	OccurrenceValue	OccurrenceDisplay	DateUtc	LastUpdatedUtc	Name	ShortName	Type	Headline	ShortHeadline	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- 3	9b01efef-921a-0d95-27fa-0cfda65e0917	c07a929b-491c-4f11-b7bc-84c12316e5ad	7348ffda-85eb-beb7-bc3c-a0872be907f2	2	week	False	2	Week 2	2026-09-08 07:00:00+00	2026-09-08 21:18:00+00	AP Top 25	AP Poll	ap	2026 NCAA Football Rankings - AP Poll Week 2	2026 AP Poll: Week 2	2026-09-09 11:36:27.000814+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL
-- 2	df0aba6b-dbe9-58c0-f930-5e70296fb2ad	c07a929b-491c-4f11-b7bc-84c12316e5ad	c63171ea-ee40-3212-508c-f0f9ffcde955	1	week	False	1	Preseason	2026-08-17 07:00:00+00	2026-08-17 19:26:00+00	AP Top 25	AP Poll	ap	2026 NCAA Football Rankings - AP Poll Preseason	2026 AP Poll: Preseason	2026-08-17 21:42:18.692848+00	NULL	ab980339-9958-4238-8db1-7459c556b6c7	NULL

select * from public."SeasonWeek" where "SeasonPhaseId" = 'fd6830cd-2220-5f34-01cd-69eda5bf3c9f' order by "StartDate"; -- NCAAFB Regular Season 2026
select * from public."SeasonWeek" where "Id" = '07ead313-9561-4e41-3507-d87c03efc935'

-- Id	SeasonId	SeasonPhaseId	Number	StartDate	EndDate	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy	IsNonStandardWeek	Text
-- f78d8d4e-635b-148d-f13e-d661d8b1ab4b	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	1	2026-08-22 07:00:00+00	2026-09-08 06:59:00+00	2026-04-14 09:50:41.783657+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- c63171ea-ee40-3212-508c-f0f9ffcde955	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	2	2026-09-08 07:00:00+00	2026-09-13 06:59:00+00	2026-04-14 09:50:24.687383+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- 7348ffda-85eb-beb7-bc3c-a0872be907f2	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	3	2026-09-13 07:00:00+00	2026-09-20 06:59:00+00	2026-04-14 09:50:09.47264+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- 07ead313-9561-4e41-3507-d87c03efc935	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	4	2026-09-20 07:00:00+00	2026-09-27 06:59:00+00	2026-04-14 09:50:25.031526+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- fe57b4a9-4bb8-21b8-16de-269f1e032de1	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	5	2026-09-27 07:00:00+00	2026-10-04 06:59:00+00	2026-04-14 09:50:24.833205+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- 3f4484bd-4827-67fe-268a-0ed517abaec4	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	6	2026-10-04 07:00:00+00	2026-10-11 06:59:00+00	2026-04-14 09:50:24.504067+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- de05aae9-cd50-cd3b-6581-88bfe8da70aa	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	7	2026-10-11 07:00:00+00	2026-10-18 06:59:00+00	2026-04-14 09:50:58.932284+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- fe0cfd55-72b7-a733-2fa6-d73027037dc3	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	8	2026-10-18 07:00:00+00	2026-10-25 06:59:00+00	2026-04-14 09:50:10.886688+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- 18792653-9169-6ab7-2204-1d93d36a9dbc	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	9	2026-10-25 07:00:00+00	2026-11-01 06:59:00+00	2026-04-14 09:50:24.677986+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- ffba1831-7bbb-e67d-6948-c8905375a654	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	10	2026-11-01 07:00:00+00	2026-11-08 07:59:00+00	2026-04-14 09:52:25.637651+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- b58ffcde-b09d-5dde-4e6b-e38bf4020795	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	11	2026-11-08 08:00:00+00	2026-11-15 07:59:00+00	2026-04-14 09:50:26.796044+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- 5ec36e0d-4670-b9fc-25d5-a1c03a85acec	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	12	2026-11-15 08:00:00+00	2026-11-22 07:59:00+00	2026-04-14 09:50:26.445565+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- dac92c44-fbf7-b765-882b-bb66373dcdf8	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	13	2026-11-22 08:00:00+00	2026-11-29 07:59:00+00	2026-04-14 09:50:41.786253+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- 16e78336-de4c-d4b7-428f-a6c130565f1b	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	14	2026-11-29 08:00:00+00	2026-12-06 07:59:00+00	2026-04-14 13:06:12.596981+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL
-- c4e173a5-4230-1f7a-fdd2-cc4d5de4bb94	ce446b31-6bdb-fd32-d8cf-e4410277698c	fd6830cd-2220-5f34-01cd-69eda5bf3c9f	15	2026-12-06 08:00:00+00	2026-12-13 07:59:00+00	2026-04-14 10:12:29.685931+00	NULL	f01a5891-e812-49d6-88e2-54c7ec049795	NULL	False	NULL

-- Poll-week collision. RESOLVED 2026-09-14 - cause confirmed from Seq.
--
-- CORRECTION to the earlier note here: it named ONE doc and attributed it to
-- the AP poll. Wrong on both counts. FOUR docs are crash-looping, on four
-- different polls, and the AP one is a different URL than the one named:
--
--   URL                                 Poll (= doc ParentId)   Colliding key
--   types/2/weeks/3/rankings/1          c07a929b  AP Top 25     (c07a929b, 7348ffda)
--   types/2/weeks/3/rankings/2          ac65cf00  AFCA Coaches  (ac65cf00, 7348ffda)
--   types/2/weeks/2/rankings/11         909da9ca  AFCA Div II   (909da9ca, 7348ffda)
--   types/2/weeks/1/rankings/20         a82a679f  FCS Coaches   (a82a679f, 7348ffda)
--
-- THE PROOF - the Week 3 AP document itself (test/unit/.../EspnFootballNcaa/
-- EspnFootballNcaaSeasonPollWeek.2026.Wk3.json):
--   $ref              .../types/2/weeks/3/rankings/1
--   headline          AP Poll Week 3
--   occurrence        { number: 3, displayValue: "Week 3" }
--   season.type.week  { number: 2, 2026-09-08T07:00Z .. 2026-09-14T06:59Z }
--
-- The processor reads the LAST one:  x.Number == dto.Season.Type.Week.Number + 1
-- season.type.week is ESPN's "what week is it right now" pointer at FETCH
-- time, not the week the poll is for. So the slot depends on when the doc
-- was fetched, and two polls fetched inside the same ESPN week collide:
--   Preseason  fetched Aug 17  pointer 1  -> slot 2
--   Week 2     fetched Sep 9   pointer 2  -> slot 3
--   Week 3     fetched Sep 13  pointer 2  -> slot 3   (ESPN rolls Mon 07:00Z)
-- Last week "just worked" because each poll happened to be fetched in a
-- different ESPN week. AP's two rows are complete: AP numbers occurrences
-- 1 = Preseason, 2 = Week 2; there was never a separate Week 1 poll.
--
-- Not idempotency: the stack shows ProcessNewEntity every time. These are
-- genuinely new occurrences; the slot is occupied by the wrong occupant.
--
-- The read side does not care which SeasonWeekId a row carries: both
-- GetRankingsByPollBySeasonByWeek.sql (API) and GetRankingsByPollByWeek.sql
-- (Producer) resolve the week's poll by DateUtc and say so in a comment.
--
-- Fix direction: resolve the week from dto.Occurrence.Number (== the URL
-- week), which is the poll's own identity. Keep the `Season.Type.Week is not
-- null` gate so preseason polls still land NULL.
--
-- SELF-HEAL WINDOW: the four Hangfire jobs are at attempt 10 of 10 with 8h
-- backoff, due ~12:00Z-15:30Z on 09-14. By then ESPN's pointer is 3, so each
-- computes slot 4 (four different polls, no mutual collision) and succeeds -
-- misfiled one week late but visible in the UI because reads go by date.
-- Documents are already in Mongo either way; no ESPN re-source needed.

-- 1) What is actually in the colliding slot, and does it have entries?
SELECT
    spw."Id",
    spw."SeasonPollId",
    spw."SeasonWeekId",
    spw."LastUpdatedUtc",
    spw."CreatedUtc",
    sw."Number"        AS "SeasonWeekNumber",
    sp2."Name"         AS "SeasonPhaseName",
    sp2."TypeCode"     AS "SeasonPhaseTypeCode",
    (SELECT count(*) FROM public."SeasonPollWeekEntry" e
      WHERE e."SeasonPollWeekId" = spw."Id") AS "EntryCount"
FROM public."SeasonPollWeek" spw
JOIN public."SeasonWeek"  sw  ON sw."Id"  = spw."SeasonWeekId"
JOIN public."SeasonPhase" sp2 ON sp2."Id" = sw."SeasonPhaseId"
WHERE spw."SeasonPollId" = '909da9ca-5d7a-63e2-04c3-c4e87dcaf7dc'
ORDER BY sw."Number";
-- Read: is there a row for EVERY week so far, or are weeks missing/doubled?
-- EntryCount = 0 on the colliding row means the poll never populated.

-- Id	SeasonPollId	SeasonWeekId	LastUpdatedUtc	CreatedUtc	SeasonWeekNumber	SeasonPhaseName	SeasonPhaseTypeCode	EntryCount
-- d135b2e7-dbd6-21b8-6115-591cd530ecf8	909da9ca-5d7a-63e2-04c3-c4e87dcaf7dc	7348ffda-85eb-beb7-bc3c-a0872be907f2	2026-09-08 19:40:00+00	2026-09-09 11:36:27.62943+00	3	Regular Season	2	50

-- 2) THE decisive one: is SeasonWeek.Number ambiguous within the season?
--    If any Number returns > 1 row, the processor's lookup is picking
--    arbitrarily between phases and hypothesis B is live.
SELECT
    sw."Number",
    count(*)                         AS "RowsWithThisNumber",
    string_agg(sp2."Name", ' | ' ORDER BY sp2."TypeCode") AS "Phases",
    string_agg(sw."Id"::text, ' | ')                      AS "SeasonWeekIds"
FROM public."SeasonWeek" sw
JOIN public."Season"      s   ON s."Id"   = sw."SeasonId"
JOIN public."SeasonPhase" sp2 ON sp2."Id" = sw."SeasonPhaseId"
WHERE s."Year" = 2026
GROUP BY sw."Number"
HAVING count(*) > 1
ORDER BY sw."Number";

-- Number	RowsWithThisNumber	Phases	SeasonWeekIds
-- 1	4	Preseason | Regular Season | Postseason | Off Season	d8a0a7c3-ad08-87c1-dbe8-3407b02a55f3 | f78d8d4e-635b-148d-f13e-d661d8b1ab4b | 6392887f-7eb7-7e55-f023-c0a1c9cdc158 | 127252fb-93da-4c87-2353-387244fae98a

-- 3) Which SeasonWeek is 7348ffda, really? (the one the poll collided on)
SELECT
    sw."Id", sw."Number", sw."StartDate", sw."EndDate",
    sp2."Name" AS "Phase", sp2."TypeCode"
FROM public."SeasonWeek" sw
JOIN public."SeasonPhase" sp2 ON sp2."Id" = sw."SeasonPhaseId"
WHERE sw."Id" = '7348ffda-85eb-beb7-bc3c-a0872be907f2';
-- ESPN week 2 + the processor's "publish at end of week, use for N+1" rule
-- means this SHOULD be regular-season week 3. If it says preseason, or says
-- week 2, the week resolution is the bug and not just idempotency.

-- Id	Number	StartDate	EndDate	Phase	TypeCode
-- 7348ffda-85eb-beb7-bc3c-a0872be907f2	3	2026-09-13 07:00:00+00	2026-09-20 06:59:00+00	Regular Season	2

-- 4) Every poll-week row for 2026, to see the shape of what landed.
SELECT
    p."Name" AS "Poll", sw."Number" AS "Week", sp2."Name" AS "Phase",
    spw."LastUpdatedUtc",
    (SELECT count(*) FROM public."SeasonPollWeekEntry" e
      WHERE e."SeasonPollWeekId" = spw."Id") AS "Entries"
FROM public."SeasonPollWeek" spw
JOIN public."SeasonPoll"  p   ON p."Id"   = spw."SeasonPollId"
LEFT JOIN public."SeasonWeek"  sw  ON sw."Id"  = spw."SeasonWeekId"
LEFT JOIN public."SeasonPhase" sp2 ON sp2."Id" = sw."SeasonPhaseId"
-- SeasonPoll carries SeasonYear directly; it has no SeasonId FK.
WHERE p."SeasonYear" = 2026
ORDER BY p."Name", sw."Number" NULLS FIRST;

-- Poll	Week	Phase	LastUpdatedUtc	Entries
-- AFCA Coaches Poll	2	Regular Season	2026-08-04 19:21:00+00	55
-- AFCA Coaches Poll	3	Regular Season	2026-09-08 21:54:00+00	52
-- AFCA Division II Coaches Poll	NULL	NULL	2026-08-18 18:50:00+00	55
-- AFCA Division II Coaches Poll	3	Regular Season	2026-09-08 19:40:00+00	50
-- AFCA Division III Coaches Poll	NULL	NULL	2026-08-24 19:00:00+00	55
-- AFCA Division III Coaches Poll	2	Regular Season	2026-08-24 19:00:00+00	55
-- AFCA Division III Coaches Poll	3	Regular Season	2026-09-08 22:08:00+00	47
-- AP Top 25	2	Regular Season	2026-08-17 19:26:00+00	50
-- AP Top 25	3	Regular Season	2026-09-08 21:18:00+00	44
-- FCS Coaches Poll	2	Regular Season	2026-08-17 19:17:00+00	53
-- FCS Coaches Poll	3	Regular Season	2026-09-08 18:24:00+00	54
