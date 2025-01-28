declare @franchise int, @franchiseExternalId int, @franchiseLogo int
declare @franchiseSeason int, @franchiseSeasonLogo int

select @franchise = count(*) from dbo.Franchise
select @franchiseExternalId = count(*) from dbo.FranchiseExternalId
select @franchiseLogo = count(*) from dbo.FranchiseLogo
select @franchiseSeason = count(*) from dbo.franchiseSeason
select @franchiseSeasonLogo = count(*) from dbo.franchiseSeasonLogo

select @franchise AS Franchise, @franchiseExternalId as franchiseExternalId, @franchiseLogo as franchiseLogo, @franchiseSeason as franchiseSeason, @franchiseSeasonLogo as franchiseSeasonLogo

declare @group int, @groupExtId int, @groupLogo int, @groupSeason int, @groupSeasonLogo int

select @group = count(*) from dbo.[Group]
select @groupExtId = count(*) from dbo.GroupExternalId
select @groupLogo = count(*) from dbo.GroupLogo
select @groupSeason = count(*) from dbo.GroupSeason
select @groupSeasonLogo = count(*) from dbo.GroupSeasonLogo

select @group as [Group], @groupExtId as GroupExt, @groupLogo as GroupLogo, @groupSeason as GroupSeason, @groupSeasonLogo as GroupSeasonLogo

declare @venue int, @venueExtId int,  @venueImage int
select @venue = count(*) from dbo.Venue
select @venueExtId = count(*) from dbo.VenueExternalId
select @venueImage = count(*) from dbo.VenueImage

select @venue as [Venue], @venueExtId as VenueExtId, @venueImage as VenueImage

--DELETE FROM [dbo].[FranchiseSeasonLogo]
--DELETE FROM [dbo].[FranchiseSeason]
--DELETE FROM [dbo].[FranchiseLogo]
--DELETE FROM [dbo].[FranchiseExternalId]
--DELETE FROM [dbo].[Franchise]
--DELETE FROM [dbo].[GroupSeasonLogo]
--DELETE FROM [dbo].[GroupSeason]
--DELETE FROM [dbo].[GroupExternalId]
--DELETE FROM [dbo].[GroupLogo]
--DELETE FROM [dbo].[Group]
--DELETE FROM [dbo].[Venue]
--DELETE FROM [dbo].[VenueExternalId]
--DELETE FROM [dbo].[VenueImage]

--DELETE FROM dbo.OutboxMessage
--DELETE FROM dbo.OutboxState
--DELETE FROM dbo.InboxState

--DROP TABLE Hangfire.[State]
--DROP TABLE Hangfire.[Set]
--DROP TABLE Hangfire.[Server]
--DROP TABLE Hangfire.[Schema]
--DROP TABLE Hangfire.[List]
--DROP TABLE Hangfire.[JobQueue]
--DROP TABLE Hangfire.[JobParameter]
--DROP TABLE Hangfire.[Job]
--DROP TABLE Hangfire.[Hash]
--DROP TABLE Hangfire.[Counter]
--DROP TABLE Hangfire.[AggregatedCounter]


