select * from public."Franchise" where "Slug" = 'lsu-tigers';
select * from public."FranchiseSeason" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7'; -- 8f90c51b-d906-2c02-e8e8-2ac0ac6340ae 2024

select * from public."FranchiseSeasonRecord" where "FranchiseSeasonId" = '8f90c51b-d906-2c02-e8e8-2ac0ac6340ae'; -- LSU 2024
select * from public."FranchiseSeasonRecord" where "FranchiseId" = 'd2ca25ce-337e-1913-b405-69a16329efe7';

select * from public."Contest" where "SeasonYear" = 2024 and "HomeTeamFranchiseSeasonId" = '8f90c51b-d906-2c02-e8e8-2ac0ac6340ae';

select * from public."Competition" where "ContestId" = '5272e3d1-cb53-a569-9435-79c3cf1581b2'; -- BAMA @ LSU 2024

select * from public."CompetitionCompetitor" where "CompetitionId" = '55505ccc-c57b-222e-9d6b-120deced9bc9'; -- BAMA @ LSU 2024

-- Id	CompetitionId	FranchiseSeasonId	Type	Order	HomeAway	Winner	CuratedRankCurrent	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy	Points	Discriminator
-- dbdbf477-edae-dc06-996c-edbcb264efc8	55505ccc-c57b-222e-9d6b-120deced9bc9	8f90c51b-d906-2c02-e8e8-2ac0ac6340ae	team	0	home	False	15	2026-02-26 02:21:39.150639-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL	NULL	FootballCompetitionCompetitor
-- 80e3500e-63a8-701a-8471-0109bf78a6db	55505ccc-c57b-222e-9d6b-120deced9bc9	bb43b2d9-68de-5a60-6f7d-9389962e0eaf	team	1	away	True	11	2026-02-26 02:21:39.278214-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL	NULL	FootballCompetitionCompetitor

select * from public."CompetitionCompetitorRecord" where "CompetitionCompetitorId" = '80e3500e-63a8-701a-8471-0109bf78a6db'; -- BAMA 2024

-- Id	CompetitionCompetitorId	Type	Name	Summary	DisplayValue	Value	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- 31809a70-1449-4ddf-be60-2d5b5b4f8dfe	80e3500e-63a8-701a-8471-0109bf78a6db	road	Road	2-2	2-2	0.5	2026-02-27 00:19:31.502895-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- f389e483-6e3a-4a75-b8ae-94b2565e1396	80e3500e-63a8-701a-8471-0109bf78a6db	home	Home	5-0	5-0	1	2026-02-27 00:19:31.46071-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- 6b056419-83f4-40f7-9a12-64dcc5bf066f	80e3500e-63a8-701a-8471-0109bf78a6db	total	overall	7-2	7-2	0.7777777777777778	2026-02-27 00:19:31.455239-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- e893f7eb-e2e4-49e8-a70c-a0c64bd7dabc	80e3500e-63a8-701a-8471-0109bf78a6db	vsconf	vs. Conf.	4-2	4-2	0.6666666666666666	2026-02-27 00:19:31.495909-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL

select * from public."CompetitionCompetitorRecord" where "CompetitionCompetitorId" = 'dbdbf477-edae-dc06-996c-edbcb264efc8'; -- LSU 2024

-- Id	CompetitionCompetitorId	Type	Name	Summary	DisplayValue	Value	CreatedUtc	ModifiedUtc	CreatedBy	ModifiedBy
-- ffdd007a-b972-4cdb-bb0b-e74b0486ccfd	dbdbf477-edae-dc06-996c-edbcb264efc8	road	Road	2-1	2-1	0.6666666666666666	2026-02-27 00:19:31.339797-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- 6e0836c6-b72b-454d-bd4d-c3cf54d5263a	dbdbf477-edae-dc06-996c-edbcb264efc8	home	Home	4-1	4-1	0.8	2026-02-27 00:19:31.322044-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- 030e841a-b70b-401d-95ca-92a87cf3c4cb	dbdbf477-edae-dc06-996c-edbcb264efc8	total	overall	6-3	6-3	0.6666666666666666	2026-02-27 00:19:31.248249-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL
-- e0c33385-dc58-41f0-ad3a-42abf46a7238	dbdbf477-edae-dc06-996c-edbcb264efc8	vsconf	vs. Conf.	3-2	3-2	0.6	2026-02-27 00:19:31.382692-05	NULL	c5b1fb0b-1350-429c-851a-4e8d30f603de	NULL