import apiClient from "./apiClient";

export async function submitPicks(groupId, week, picks) {
  const response = await apiClient.post(`/picks`, {
    groupId,
    week,
    picks,
  });
  return response.data;
}
