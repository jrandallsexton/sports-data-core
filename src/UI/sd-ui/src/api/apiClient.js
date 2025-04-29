import axios from "axios";
import toast from "react-hot-toast";

const apiClient = axios.create({
  baseURL: "http://localhost:3001", // 🔥 later swap to your real server
  timeout: 10000,
  headers: {
    "Content-Type": "application/json",
  },
});

// 🔥 Optional interceptors:
// apiClient.interceptors.request.use((config) => {
//   const token = localStorage.getItem('authToken');
//   if (token) config.headers.Authorization = `Bearer ${token}`;
//   return config;
// });

apiClient.interceptors.response.use(
  response => response,
  error => {
    //toast.error("Something went wrong. Please try again.");
    return Promise.reject(error);
  }
);

export default apiClient;
