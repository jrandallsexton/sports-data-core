import apiClient from "./apiClient";

const ConferenceApi = {
  getConferenceNamesAndSlugs: () => apiClient.get("/ui/conferences")
};

export default ConferenceApi;
