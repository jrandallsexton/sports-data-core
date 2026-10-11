-- Task 01 (data spike) — which participant roles ESPN attaches to each play type.
-- Database: sdProducer.FootballNcaa (local). Plays with participants (stored since 2026-07-22).
-- Per play type: the role sequence (Order 0..n) and how often it occurs.
WITH roles AS (
    SELECT p."Id", p."TypeId",
           string_agg(pp."Type", ', ' ORDER BY pp."Order") AS role_sequence
    FROM "CompetitionPlay" p
    JOIN "CompetitionPlayParticipant" pp ON pp."CompetitionPlayId" = p."Id"
    WHERE p."TypeId" IN ('26','63','36','29','9','39','7','20','24','67','68','5','59','60')
    GROUP BY p."Id", p."TypeId"
)
SELECT "TypeId", role_sequence, count(*) AS plays
FROM roles
GROUP BY "TypeId", role_sequence
HAVING count(*) >= 20
ORDER BY "TypeId"::int, plays DESC;
