import apiClient from "./apiClient";

const MapsApi = {
  getMap: () =>
    apiClient.get("/ui/map")
};

export default MapsApi;
