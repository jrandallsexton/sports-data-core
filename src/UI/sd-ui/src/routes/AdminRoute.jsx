import { Navigate, useLocation } from 'react-router-dom';
import { useUserDto } from '../contexts/UserContext';

function AdminRoute({ children }) {
  const { userDto, loading } = useUserDto();
  const location = useLocation();

  if (loading) {
    return <div className="route-loading">Loading...</div>;
  }

  // If not an admin, redirect back to the main app landing
  if (!userDto || !userDto.isAdmin) {
    return <Navigate to="/app" replace state={{ from: location }} />;
  }

  return children;
}

export default AdminRoute;
