// src/api/previewApi.js
import apiClient from "./apiClient";

const PreviewApi = {
  approvePreviewByContestId: (previewId) =>
    apiClient.post(`/api/previews/${encodeURIComponent(previewId)}/approve`),
  rejectPreviewByContestId: (previewId, command) =>
    apiClient.post(`/api/previews/${encodeURIComponent(previewId)}/reject`, command),
};

export default PreviewApi;
