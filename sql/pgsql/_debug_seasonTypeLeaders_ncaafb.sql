SELECT COUNT(*) FROM public."SeasonTypeLeader"
  WHERE "SeasonYear" = 2025 AND "SeasonTypeCode" = 3;

  SELECT stl."Rank", stl."DisplayValue", a."DisplayName"
  FROM public."SeasonTypeLeader" stl
  JOIN public."AthleteSeason" ats ON ats."Id" = stl."AthleteSeasonId"
  JOIN public."Athlete" a ON a."Id" = ats."AthleteId"
  WHERE stl."SeasonYear" = 2025 AND stl."SeasonTypeCode" = 3
    AND stl."CategoryName" = 'passingYards'
  ORDER BY stl."Rank" LIMIT 5;