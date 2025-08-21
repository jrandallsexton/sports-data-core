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
    '00000000-0000-0000-0000-000000000000', -- fixed GUID for starter
    'edf84c4b-04d0-488f-b18e-1fed96fb93c7',
    'Welcome to sportDeets!',
    '00000000-0000-0000-0000-000000000000', -- system user or replace with real
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
    '00000000-0000-0000-0000-000000000000',
    NULL,        -- root post has no parent
    0,           -- depth 0
    '0001',      -- first segment of path
    'Welcome to your group here at sportDeets. This is the place to discuss picks, matchups, and everything NCAA football!',
    '00000000-0000-0000-0000-000000000000', -- system user
    NOW(),
    0,
    0,
    0,
	false
);
