select * from public."MatchupPreviewPrompt" order by "CreatedUtc" desc;

select mo."Name" as "Model", mpp.*
from public."MatchupPreviewPrompt" mpp
left join public."Model" mo on mo."Id" = mpp."ModelId"
order by mpp."CreatedUtc" desc;

select * from public."MatchupPreviewPrompt" where "ModelId" is null;
delete from public."MatchupPreviewPrompt" where "ModelId" is null;

--update public."MatchupPreviewPrompt" set "ModelId" = 'b0000000-0000-0000-0000-000000000007' where "Model" = 'deepseek-chat' and "ModelId" is null;

select * from public."Model";

select * from public."Prompt" where "Id" = 'a48a0855-894a-4d8d-b5a8-421a7bb72a53';