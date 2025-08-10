import * as React from "react";
import { Link } from "react-router-dom";
import { formatToEasternTime } from "../../utils/timeUtils";
import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  Chip,
} from "@mui/material";

export default function TeamScheduleMUI({ schedule = [], seasonYear }) {
  const hasRows = Array.isArray(schedule) && schedule.length > 0;

  return (
    <TableContainer component={Paper} elevation={3} sx={{ mt: 2 }}>
      <Typography
        variant="h6"
        sx={{
          px: 2,
          pt: 2,
          pb: 1,
          color: "warning.main", // similar to your highlight color
          borderBottom: (theme) => `1px solid ${theme.palette.warning.main}`,
        }}
      >
        Schedule ({seasonYear})
      </Typography>

      <Table size="small" aria-label="team schedule">
        <TableHead>
          <TableRow sx={{ bgcolor: "grey.900" }}>
            <TableCell sx={{ color: "grey.100", fontWeight: 600 }}>
              Kickoff (ET)
            </TableCell>
            <TableCell sx={{ color: "grey.100", fontWeight: 600 }}>
              Opponent
            </TableCell>
            <TableCell sx={{ color: "grey.100", fontWeight: 600 }}>
              Location
            </TableCell>
            <TableCell sx={{ color: "grey.100", fontWeight: 600 }}>
              Result
            </TableCell>
          </TableRow>
        </TableHead>

        <TableBody>
          {hasRows ? (
            schedule.map((game, idx) => {
              const resultText = game.result ?? "";
              const isWin = resultText.trim().toUpperCase().startsWith("W");
              return (
                <TableRow
                  key={game.id ?? `${game.opponentSlug ?? "opp"}-${idx}`}
                  hover
                  sx={{
                    "&:nth-of-type(even)": { bgcolor: "action.hover" },
                  }}
                >
                  <TableCell sx={{ whiteSpace: "nowrap" }}>
                    {formatToEasternTime(game.date)}
                  </TableCell>

                  <TableCell>
                    <Link
                      to={`/app/sport/football/ncaa/team/${game.opponentSlug}/${seasonYear}`}
                      style={{
                        textDecoration: "underline",
                        fontWeight: 500,
                        color: "inherit",
                      }}
                    >
                      {game.opponent}
                    </Link>
                  </TableCell>

                  <TableCell>{game.location}</TableCell>

                  <TableCell sx={{ whiteSpace: "nowrap" }}>
                    {resultText ? (
                      <Chip
                        size="small"
                        label={resultText}
                        color={isWin ? "success" : "error"}
                        variant="outlined"
                        sx={{ fontWeight: 600 }}
                      />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                </TableRow>
              );
            })
          ) : (
            <TableRow>
              <TableCell colSpan={4} sx={{ textAlign: "center", py: 3 }}>
                No games scheduled.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
