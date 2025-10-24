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
import './AdminPage.css';

export default function CompetitionsWithoutPlays({ playsItems = [], playsLoading, playsError, page, rowsPerPage, handleChangePage, handleChangeRowsPerPage, refreshPlays }) {
  return (
    <section className="admin-card">
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <h3 style={{ margin: 0, flex: '1 1 auto', minWidth: 0 }}>
          Competitions Without Plays
          {!playsLoading && !playsError ? <span> ({playsItems.length})</span> : null}
        </h3>
        <div style={{ flex: '0 0 auto' }}>
          {playsLoading ? (
            <CircularProgress size={18} thickness={6} color="inherit" />
          ) : (
            <IconButton aria-label="Refresh plays" size="small" onClick={() => { if (typeof refreshPlays === 'function') refreshPlays(); }} sx={{ color: '#61dafb', ml: 1 }}>
              <FiRefreshCw />
            </IconButton>
          )}
        </div>
      </div>
      {playsLoading ? (
        <div className="placeholder">Loading</div>
      ) : playsError ? (
        <div className="placeholder">Error: {String(playsError)}</div>
      ) : playsItems.length === 0 ? (
        <div className="placeholder">No items found.</div>
      ) : (
        <>
          <TableContainer component={Paper} sx={{ background: '#23272f', color: '#f8f9fa' }}>
            <Table aria-label="competitions-without-plays" sx={{ minWidth: 920, tableLayout: 'fixed' }}>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 180 }}>CompetitionId</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 180 }}>ContestId</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 340 }}>ContestName</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 180 }}>StartDateUtc</TableCell>
                  <TableCell align="right" sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 80 }}>PlayCount</TableCell>
                  <TableCell sx={{ color: '#61dafb', backgroundColor: '#23272f', borderBottom: '2px solid rgba(97,218,251,0.12)', width: 200 }}>LastPlayText</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {playsItems.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage).map((it) => {
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

                  const playCount = typeof it.playCount === 'number' ? it.playCount : Number(it.playCount || 0);

                  return (
                    <TableRow key={it.competitionId ?? it.contestId} hover sx={{ '& td': { color: '#f8f9fa', borderBottom: '1px solid rgba(255,255,255,0.04)' } }}>
                      <TableCell sx={{ width: 180, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{it.competitionId}</TableCell>
                      <TableCell sx={{ width: 180, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{it.contestId}</TableCell>
                      <TableCell sx={{ width: 340, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {it.contestId && it.contestName ? (
                          <a
                            href={`/app/sport/football/ncaa/contest/${it.contestId}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{ color: '#61dafb', textDecoration: 'underline', display: 'inline-flex', alignItems: 'center', gap: 6, overflow: 'hidden' }}
                            aria-label={`Open contest ${it.contestName} in new tab`}
                          >
                            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'inline-block', maxWidth: '100%' }}>{it.contestName}</span>
                            <FiExternalLink style={{ fontSize: '0.9em' }} />
                          </a>
                        ) : (
                          it.contestName ?? '-'
                        )}
                      </TableCell>
                      <TableCell sx={{ width: 180, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{startDisplay}</TableCell>
                      <TableCell align="right" sx={{ width: 80 }}>{playCount}</TableCell>
                      <TableCell sx={{ width: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{it.lastPlayText ?? '-'}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
          <TablePagination
            component="div"
            count={playsItems.length}
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
