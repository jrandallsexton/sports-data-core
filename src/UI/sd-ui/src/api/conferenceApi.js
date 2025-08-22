import apiClient from "./apiClient";

const ConferenceApi = {
  getConferenceNamesAndSlugs: () => apiClient.get("/ui/conference/list")
};

export default ConferenceApi;
