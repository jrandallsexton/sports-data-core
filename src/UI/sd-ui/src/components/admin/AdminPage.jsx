import React, { useEffect, useState } from 'react';
import './AdminPage.css';
import apiWrapper from '../../api/apiWrapper';
import AdminHeader from './AdminHeader';
import CompetitionsWithoutCompetitors from './CompetitionsWithoutCompetitors';
import CompetitionsWithoutPlays from './CompetitionsWithoutPlays';
import SystemHealth from './SystemHealth';
import RecentErrors from './RecentErrors';

export default function AdminPage() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [items, setItems] = useState([]);
  const [playsLoading, setPlaysLoading] = useState(false);
  const [playsError, setPlaysError] = useState(null);
  const [playsItems, setPlaysItems] = useState([]);
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(5);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await apiWrapper.Admin.getCompetitionsWithoutCompetitors();
        if (!cancelled) {
          setItems(Array.isArray(res.data) ? res.data : res.data?.items ?? []);
        }
      } catch (err) {
        if (!cancelled) setError(err.message || 'Failed to fetch');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    load();
    return () => { cancelled = true; };
  }, []);

  // Load competitions without plays
  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setPlaysLoading(true);
      setPlaysError(null);
      try {
        const res = await apiWrapper.Admin.getCompetitionsWithoutPlays();
        if (!cancelled) setPlaysItems(Array.isArray(res.data) ? res.data : res.data?.items ?? []);
      } catch (err) {
        if (!cancelled) setPlaysError(err.message || 'Failed to fetch plays dataset');
      } finally {
        if (!cancelled) setPlaysLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  const handleChangePage = (event, newPage) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (event) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  return (
    <div className="admin-page">
      <AdminHeader />

      <div className="admin-grid">
        <div className="admin-main">
          {/* Top widgets: place System Health and Recent Errors side-by-side */}
          <div className="admin-top-widgets">
            <SystemHealth />
            <RecentErrors />
          </div>

          <CompetitionsWithoutCompetitors items={items} loading={loading} error={error} />
        </div>

        <div className="admin-plays">
          <CompetitionsWithoutPlays
            playsItems={playsItems}
            playsLoading={playsLoading}
            playsError={playsError}
            page={page}
            rowsPerPage={rowsPerPage}
            handleChangePage={handleChangePage}
            handleChangeRowsPerPage={handleChangeRowsPerPage}
          />
        </div>
      </div>
    </div>
  );
}
