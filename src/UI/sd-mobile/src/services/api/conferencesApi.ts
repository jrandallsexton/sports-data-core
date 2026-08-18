import { apiClient } from './client';

// Matches SportsData.Core.Dtos.Canonical.ConferenceDivisionNameAndSlugDto.
// Division is the parent classification name ("FBS", "FCS",
// "NCAA Division II", "NCAA Division III").
export interface ConferenceOption {
  division: string;
  shortName: string;
  slug: string;
}

export const conferencesKeys = {
  all: ['conferences'] as const,
};

export const conferencesApi = {
  // GET /ui/conferences — every conference with its classification (web twin:
  // ConferenceApi.getConferenceNamesAndSlugs). Mobile callers filter to FBS;
  // the full list is a web-only surface (operator ruling 2026-08-18).
  getConferenceNamesAndSlugs: () =>
    apiClient.get<ConferenceOption[]>('/ui/conferences'),
};
