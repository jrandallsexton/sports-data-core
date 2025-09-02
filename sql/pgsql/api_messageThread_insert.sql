select * from public."PickemGroup"
select * from public."User"
-- Starter thread for group 'edf84c4b-04d0-488f-b18e-1fed96fb93c7'

INSERT INTO "MessageThread" (
    "Id",
    "GroupId",
    "Title",
    "CreatedBy",
    "CreatedUtc",
    "LastActivityAt",
    "PostCount",
    "IsLocked",
    "IsPinned"
) VALUES (
    '00000000-0000-0000-0000-000000000002', -- fixed GUID for starter
    'aa7a482f-2204-429a-bb7c-75bc2dfef92b',
    'Welcome to ''AP25_SEC_ATS'' here at sportDeets!',
    '11111111-1111-1111-1111-111111111111', -- system user or replace with real
    NOW(),
    NOW(),
    1,     -- thread has one post (the root)
    false,
    false
);

INSERT INTO "MessagePost" (
    "Id",
    "ThreadId",
    "ParentId",
    "Depth",
    "Path",
    "Content",
    "CreatedBy",
    "CreatedUtc",
    "ReplyCount",
    "LikeCount",
    "DislikeCount",
	"IsDeleted"
) VALUES (
    gen_random_uuid(), -- requires pgcrypto extension
    '00000000-0000-0000-0000-000000000002',
    NULL,        -- root post has no parent
    0,           -- depth 0
    '0001',      -- first segment of path
    'Welcome to your group AP25_SEC_ATS here at sportDeets. This is the place to discuss picks, matchups, and everything NCAA football!',
    '11111111-1111-1111-1111-111111111111', -- system user
    NOW(),
    0,
    0,
    0,
	false
);
