import React from 'react';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';
import TablePagination from '@mui/material/TablePagination';
import { FiExternalLink, FiRefreshCw } from 'react-icons/fi';
import IconButton from '@mui/material/IconButton';
import CircularProgress from '@mui/material/CircularProgress';
import { contestLink, resolveSportLeague } from '../../utils/sportLinks';
import './AdminPage.css';

export default function CompetitionsWithoutCompetitors({ items = [], loading, error, page, rowsPerPage, handleChangePage, handleChangeRowsPerPage, refresh }) {
  return (
    <section className="admin-card large">
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h3 style={{ margin: 0, flex: '1 1 auto', minWidth: 0 }}>
          Competitions Without Competitors
          {!loading && !error ? <span> ({items.length})</span> : null}
        </h3>
        <div style={{ flex: '0 0 auto' }}>
          {loading ? (
            <CircularProgress size={18} thickness={6} color="inherit" />
          ) : (
            <IconButton aria-label="Refresh competitors" size="small" onClick={() => { if (typeof refresh === 'function') refresh(); }} sx={{ color: '#61dafb', ml: 1 }}>
              <FiRefreshCw />
            </IconButton>
          )}
        </div>
      </div>
      {loading ? (
        <div className="placeholder">Loading</div>
      ) : error ? (
        <div className="placeholder">Error: {String(error)}</div>
      ) : items.length === 0 ? (
        <div className="placeholder">No competitions found.</div>
      ) : (
        <>
          <TableContainer component={Paper} sx={{ background: '#23272f', color: '#f8f9fa' }}>
            <Table aria-label="competitions-without-competitors" sx={{ minWidth: 1030, tableLayout: 'fixed' }}>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 110 }}>Sport</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 180 }}>CompetitionId</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 180 }}>ContestId</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 340 }}>CompetitionName</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 180 }}>StartDateUtc</TableCell>
                  <TableCell align="right" sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 110 }}>CompetitorCount</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage).map((it) => {
                  const start = it.startDateUtc;
                  const isSentinel = typeof start === 'string' && start.startsWith('0001-01-01');
                  let startDisplay = '-';
                  if (!isSentinel) {
                    try {
                      const d = new Date(start);
                      if (!isNaN(d.getTime())) startDisplay = d.toLocaleString();
                    } catch (e) {
                      startDisplay = '-';
                    }
                  }

                  const sportLeague = resolveSportLeague(it.sport);

                  return (
                    <TableRow key={it.competitionId ?? it.contestId} hover sx={{ '& td': { color: '#f8f9fa', borderBottom: '1px solid rgba(255,255,255,0.04)' } }}>
                      <TableCell sx={{ width: 110 }}>{it.sport ?? '-'}</TableCell>
                      <TableCell sx={{ width: 180, whiteSpace: 'normal', wordBreak: 'break-all', fontFamily: 'monospace' }}>{it.competitionId}</TableCell>
                      <TableCell sx={{ width: 180, whiteSpace: 'normal', wordBreak: 'break-all', fontFamily: 'monospace' }}>{it.contestId}</TableCell>
                      <TableCell sx={{ width: 340, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {it.contestId && it.competitionName && sportLeague ? (
                          <a
                            href={contestLink(it.contestId, sportLeague.sport, sportLeague.league)}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{ color: '#61dafb', textDecoration: 'underline', display: 'inline-flex', alignItems: 'center', gap: 6, overflow: 'hidden' }}
                            aria-label={`Open contest ${it.competitionName} in new tab`}
                          >
                            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'inline-block', maxWidth: '100%' }}>{it.competitionName}</span>
                            <FiExternalLink style={{ fontSize: '0.9em' }} />
                          </a>
                        ) : (
                          it.competitionName ?? '-'
                        )}
                      </TableCell>
                      <TableCell sx={{ width: 180, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{startDisplay}</TableCell>
                      <TableCell align="right" sx={{ width: 110 }}>{typeof it.competitorCount === 'number' ? it.competitorCount : '-'}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
          <TablePagination
            component="div"
            count={items.length}
            page={page}
            onPageChange={handleChangePage}
            rowsPerPage={rowsPerPage}
            onRowsPerPageChange={handleChangeRowsPerPage}
            rowsPerPageOptions={[5,10,25]}
            sx={{ background: 'transparent', color: '#f8f9fa', mt: 1 }}
          />
        </>
      )}
    </section>
  );
}
