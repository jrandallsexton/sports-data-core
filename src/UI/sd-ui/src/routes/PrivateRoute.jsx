import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

function PrivateRoute({ children }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <div className="route-loading">Loading...</div>;
  }

  // If we don't have a user, redirect to signup
  if (!user) {
    return <Navigate to="/signup" replace state={{ from: location }} />;
  }

  return children;
}

export default PrivateRoute;
