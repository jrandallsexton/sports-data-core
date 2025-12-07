select DISTINCT "ContestId" from public."UserPick" where "Week" = 15 order by "ContestId"

-- Done: BYU/TT, Kennesaw @ Jax State, NoTx @ Tulane, Troy @ JMU, UGA @ Bama, Indiana @ OhSt
-- Others were correct already

select * from public."ContestPrediction" where "ContestId" = 'e3f3dada-71a9-8a04-2d00-2e774d66c685'

-- Indiana @ OhSt
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = '8bbb44a7-3cf9-2e22-1b91-65ac51a8bbea', "WinProbability" = 0.3740 where "Id" = 'c39de052-c834-4110-9d25-13a2b1f7e761'
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = '8bbb44a7-3cf9-2e22-1b91-65ac51a8bbea', "WinProbability" = 0.2227 where "Id" = 'c7106020-1bcf-4a80-b572-9c665fc27be0'

-- UGA @ Bama
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = '0fe5f457-3ab7-4d9b-47c5-7015eaec1c4f', "WinProbability" = 0.4649 where "Id" = '54c8575e-a5f3-4ba3-ad3e-cb03872dd021'

-- Troy @ JMU
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = '0fccafcd-1f26-e830-5757-8e7db4bf8a51', "WinProbability" = 0.293 where "Id" = '780b56ae-9bea-4f99-88f0-625200a4bfdc'

-- Kennesaw @ Jax State
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = 'da185aa7-ace8-599b-25fc-6d06eeb429b4', "WinProbability" = 0.4554 where "Id" = 'f8af8be0-2c84-462c-ac08-ae10426596f2'

-- NoTx @ Tulane
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = '08c93f44-bb6d-638b-765e-c422ac40e1ce', "WinProbability" = 0.2322 where "Id" = '0240b9b6-662e-41c0-9768-dc63b32066eb'
--update public."ContestPrediction" set "WinnerFranchiseSeasonId" = '08c93f44-bb6d-638b-765e-c422ac40e1ce', "WinProbability" = 0.1756 where "Id" = '367d9fe1-37e0-43c9-b0c3-06ba6841d4d1'

select * from public."User" where "IsSynthetic" = true

--select * from public."UserPick" where "UserId" = '82942ecd-7b8d-420f-a13c-7e90d0ecd048' and "Week" = 15

delete from public."UserPick" where "UserId" = '5fa4c116-1993-4f2b-9729-c50c62150813' and "Week" = 15
delete from public."UserPick" where "UserId" = 'b210d677-19c3-4f26-ac4b-b2cc7ad58c44' and "Week" = 15
delete from public."UserPick" where "UserId" = 'fab4d468-5899-4324-b0fd-7d2a01d76504' and "Week" = 15
delete from public."UserPick" where "UserId" = '7f9c6a8e-8b93-4623-a0f0-2741df86b679' and "Week" = 15
delete from public."UserPick" where "UserId" = '82942ecd-7b8d-420f-a13c-7e90d0ecd048' and "Week" = 15