import axios from "axios";
//import toast from "react-hot-toast";

const apiClient = axios.create({
  baseURL: process.env.REACT_APP_API_BASE_URL,
  timeout: 10000,
  headers: {
    "Content-Type": "application/json",
  },
});

// 🔥 Optional interceptors:
apiClient.interceptors.request.use((config) => {
  console.log("API Request URL:", config.url);
  const token = localStorage.getItem('authToken');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  response => response,
  error => {
    //toast.error("Something went wrong. Please try again.");
    return Promise.reject(error);
  }
);

export default apiClient;
