import apiClient from "./apiClient";

const LogoAdminApi = {
  getFranchiseLogos: (sport, league, slug, seasonYear) =>
    apiClient.get(`/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/logos`),

  updateLogoDarkBg: (sport, league, slug, seasonYear, logoId, isForDarkBg, logoType) =>
    apiClient.patch(`/ui/teamcard/sport/${sport}/league/${league}/team/${slug}/${seasonYear}/logos/${logoId}/dark-bg`, {
      isForDarkBg,
      logoType,
    }),
};

export default LogoAdminApi;
