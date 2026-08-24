select * from public."AthleteSeason" limit 10;

select * from public."AthletePosition" order by "Name";

select * from public."AthleteStatus";

-- Active FBS QBs in 2026
select athS.*, fs."Slug" 
from public."AthleteSeason" athS
inner join public."AthleteStatus" athStatus on athStatus."Id" = athS."StatusId"
inner join public."FranchiseSeason" fs on fs."Id" = athS."FranchiseSeasonId"
inner join public."AthletePosition" ap on ap."Id" = athS."PositionId"
where
    athStatus."Name" = 'Active' and
    fs."SeasonYear" = 2026 and
    ap."Abbreviation" = 'WR' and
    athS."IsActive" = true and
    fs."GroupSeasonMap" like '%fbs%'
order by athS."LastName", athS."FirstName"; -- QB 852 down to 718 after AthleteStatus join and filter

-- 4afaf7e4-f027-c0ea-a5a1-358f3730f057 Sam Leavitt AthleteId
-- ea6ec41d-31a1-623e-1a68-d21910f17bb8 Arch Manning AthleteId

select fs."SeasonYear", athS.*
from public."AthleteSeason" athS
inner join public."FranchiseSeason" fs on fs."Id" = athS."FranchiseSeasonId"
where athS."AthleteId" = 'ea6ec41d-31a1-623e-1a68-d21910f17bb8'
order by fs."SeasonYear" desc;

-- SeasonYear	Id	AthleteId	FranchiseSeasonId	PositionId	FirstName	LastName	DisplayName	ShortName	Slug	WeightLb	WeightDisplay	HeightIn	HeightDisplay	Jersey	ExperienceAbbreviation	ExperienceDisplayValue	ExperienceYears	IsActive	StatusId	Discriminator	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- 2026	2cd9b32e-5e73-2fa8-0f23-d16e00dfb099	ea6ec41d-31a1-623e-1a68-d21910f17bb8	f669a484-38aa-a805-8fd8-938288bd5e22	bf91cc7e-912a-7072-6268-1477efe15790	Arch	Manning	Arch Manning	A. Manning	arch-manning	222.00	222 lbs	76.00	6' 4"	16	JR	Junior	3	True	553a5909-a05c-40e1-be41-f38bacc04d7a	FootballAthleteSeason	2026-04-14 05:17:34.435491-04	2026-08-17 17:45:53.106896-04	f01a5891-e812-49d6-88e2-54c7ec049795	ab980339-9958-4238-8db1-7459c556b6c7
-- 2025	d8e67eb6-afc9-ccf1-94b4-593813f5ca4d	ea6ec41d-31a1-623e-1a68-d21910f17bb8	2bb15a4f-652a-1a5f-3060-dcc3355bac93	bf91cc7e-912a-7072-6268-1477efe15790	Arch	Manning	Arch Manning	A. Manning	arch-manning	222.00	222 lbs	76.00	6' 4"	16	JR	Junior	3	True	553a5909-a05c-40e1-be41-f38bacc04d7a	FootballAthleteSeason	2025-08-25 16:08:39.635707-04	2026-08-16 04:35:35.337794-04	a070e023-aebf-48cf-9835-ce30ee96ac4e	adec14a8-30d5-4461-b1e3-e17eda25cd29
-- 2024	84d32bb6-4ce9-51e1-de75-bc25af02ebb4	ea6ec41d-31a1-623e-1a68-d21910f17bb8	2baa6f9b-451d-81f7-fd48-ef91a5376721	bf91cc7e-912a-7072-6268-1477efe15790	Arch	Manning	Arch Manning	A. Manning	arch-manning	219.00	219 lbs	76.00	6' 4"	16	SO	Sophomore	2	True	553a5909-a05c-40e1-be41-f38bacc04d7a	FootballAthleteSeason	2026-02-25 19:08:36.923916-05	2026-08-16 04:46:47.464936-04	c5b1fb0b-1350-429c-851a-4e8d30f603de	c0ec43a2-42df-4f6d-94f6-f945093713d2
-- 2023	22e69b27-88a0-781d-df04-bdc7b294d6d1	ea6ec41d-31a1-623e-1a68-d21910f17bb8	6f392e59-5945-edb2-92b7-e0ccabd615b8	bf91cc7e-912a-7072-6268-1477efe15790	Arch	Manning	Arch Manning	A. Manning	arch-manning	219.00	219 lbs	76.00	6' 4"	16	SO	Sophomore	2	True	553a5909-a05c-40e1-be41-f38bacc04d7a	FootballAthleteSeason	2026-02-08 21:19:15.73956-05	2026-08-16 05:10:41.726542-04	e15add7f-557e-4a7e-b6a3-07e320f2a5ee	993f0e63-c8f2-4e9a-a7f4-9fec42c6df7d

select assc."DisplayName" as "Category", asss."DisplayName" as "Stat", asss."DisplayValue", asss."PerGameValue", fs."SeasonYear"
from public."AthleteSeasonStatistic" ass
inner join public."AthleteSeason" athS on athS."Id" = ass."AthleteSeasonId"
inner join public."FranchiseSeason" fs on fs."Id" = athS."FranchiseSeasonId"
inner join public."AthleteSeasonStatisticCategory" assc on assc."AthleteSeasonStatisticId" = ass."Id"
inner join public."AthleteSeasonStatisticStat" asss on asss."AthleteSeasonStatisticCategoryId" = assc."Id"
where ass."AthleteSeasonId" = 'd8e67eb6-afc9-ccf1-94b4-593813f5ca4d'; -- 2025 Arch Manning

select * from public."AthleteSeasonStatistic" limit 10;

select * from public."AthleteSeasonStatisticCategory" limit 10;
select * from public."AthleteSeasonStatisticStat" limit 10;

select * from public."FranchiseSeason" where "GroupSeasonMap" like '%fbs%' limit 10;
