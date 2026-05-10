import React from 'react';
import './AdminPage.css';
import AdminHeader from './AdminHeader';
import FootballDebugCard from './signalr-debug/FootballDebugCard';

/**
 * Football SignalR debug harness page. Hosts the football-specific debug
 * card on its own route (`/admin/football`) so the parent /admin page
 * stays focused on system-health/data-quality widgets.
 */
export default function AdminFootballPage() {
  return (
    <div className="admin-page">
      <AdminHeader />
      <section className="admin-signalr-debug">
        <FootballDebugCard />
      </section>
    </div>
  );
}
