SELECT *
FROM public."Athlete"
WHERE "DoB" > '2000-01-01'
  AND "WeightLb" > 350
  AND "LastName" ~ '^[A-Za-z]+$'
  AND "FirstName" ~ '^[A-Za-z]+$';
