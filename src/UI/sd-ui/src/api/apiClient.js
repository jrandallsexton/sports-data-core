import axios from "axios";
//import toast from "react-hot-toast";

const apiClient = axios.create({
  baseURL: process.env.REACT_APP_API_BASE_URL,
  timeout: 10000,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true, // Enable sending cookies with requests
});

// 🔥 Optional interceptors:
apiClient.interceptors.request.use((config) => {
  console.log("API Request URL:", config.url);
  console.log("Request Headers:", config.headers);
  // No need to manually add token as it will be sent via cookie
  return config;
});

apiClient.interceptors.response.use(
  response => {
    console.log("API Response:", response.status, response.config.url);
    return response;
  },
  async error => {
    console.log("API Error:", error.response?.status, error.config?.url);
    if (error.response?.status === 401) {
      // Mark the error as unauthorized for components to handle
      error.isUnauthorized = true;
    }
    return Promise.reject(error);
  }
);

export default apiClient;
