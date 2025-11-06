# CFP Bracket Data Structure

The CFP poll should include a `bracket` property with the following structure:

```javascript
{
  pollName: "cfp",
  hasPoints: false,
  hasFirstPlaceVotes: false,
  seasonYear: 2025,
  week: 11,
  pollDateUtc: "2025-11-04T08:00:00Z",
  entries: [
    // ... regular rankings entries (1-12)
  ],
  bracket: {
    firstRound: [
      {
        seed1: 12,
        team1: {
          franchiseSeasonId: "...",
          franchiseSlug: "memphis-tigers",
          franchiseName: "Memphis",
          franchiseLogoUrl: "https://...",
          wins: 9,
          losses: 2
        },
        seed2: 5,
        team2: {
          franchiseSeasonId: "...",
          franchiseSlug: "georgia-bulldogs",
          franchiseName: "Georgia",
          franchiseLogoUrl: "https://...",
          wins: 9,
          losses: 2
        },
        winner: null  // null if not played, or franchiseSeasonId of winner
      },
      // 3 more first round games (9v8, 11v6, 10v7)
    ],
    quarterfinals: [
      {
        seed1: 4,
        team1: {
          // Alabama (bye, #4 seed)
        },
        seed2: null,  // TBD from first round
        team2: null,
        winner: null
      },
      // 3 more quarterfinal games (1 vs winner, 2 vs winner, 3 vs winner)
    ],
    semifinals: [
      {
        seed1: null,  // TBD from quarterfinals
        team1: null,
        seed2: null,
        team2: null,
        winner: null
      },
      {
        seed1: null,
        team1: null,
        seed2: null,
        team2: null,
        winner: null
      }
    ],
    championship: {
      seed1: null,
      team1: null,
      seed2: null,
      team2: null,
      winner: null,
      location: "Miami Gardens, Florida",
      date: "Jan 19"
    }
  }
}
```

## Notes:

- **First Round**: 4 games (seeds 12v5, 9v8, 11v6, 10v7)
- **Quarterfinals**: 4 games (top 4 seeds get byes, play first round winners)
- **Semifinals**: 2 games
- **Championship**: 1 game

- `winner` should be `null` before the game is played
- `winner` should be the `franchiseSeasonId` of the winning team after the game
- Teams that haven't been determined yet should be `null`
- Seeds for TBD teams can be `null`

## API Endpoint:

The bracket data should be included in the response from:
```
GET /api/rankings/current?seasonYear=2025&week=11
```

For the CFP poll object only (other polls like AP, Coaches won't have a `bracket` property).
