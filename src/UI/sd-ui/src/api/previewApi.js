// src/api/previewApi.js
import apiClient from "./apiClient";

const PreviewApi = {
  approvePreviewByContestId: (previewId) =>
    apiClient.post(`/preview/${encodeURIComponent(previewId)}/approve`),
  rejectPreviewByContestId: (previewId, command) =>
    apiClient.post(`/preview/${encodeURIComponent(previewId)}/reject`, command),
};

export default PreviewApi;
