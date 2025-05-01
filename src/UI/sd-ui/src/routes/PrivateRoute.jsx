import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext'; // we’ll discuss next

function PrivateRoute({ children }) {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <div className="route-loading">Loading...</div>;
  }

  return user ? children : <Navigate to="/signup" replace state={{ from: location }} />;
}

export default PrivateRoute;
