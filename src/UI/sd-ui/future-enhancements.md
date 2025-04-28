# sportDeets – Future Enhancements

## PicksPage Upgrades
- ✅ Add grid view option for quick pick entry (done)
- Highlight picks clearly in grid view
- Color pick buttons using team's primary/secondary colors (future polish)
- Enhance "Submit Picks" flow with confirmation / animation

## Consensus Improvements
- Break out Consensus percentages by:
  - Spread picks
  - Over/Under picks
  - Straight-Up picks
- Display these separately on MatchupCard and MatchupGrid

## Group Management
- Create, edit, and delete groups
- Invite users to groups
- Set league options: (spread, straight-up, confidence points)
- Allow private/public group selection

## User Profiles
- Allow users to update display name, favorite team, bio
- Upload a profile picture
- Theme settings stored per user

## Authentication
- Add login/signup/authentication
- Google sign-in integration
- Password recovery flow

## Messaging
- Message Board to support:
  - Threads
  - Quick replies
  - Attach emojis/GIFs
- Moderation options for group admins

## Admin Tools
- Manage user bans/timeouts
- View leaderboard audit history
- API request monitoring for performance tracking

## API Reliability
- Implement retry logic (exponential backoff) using axios-retry
- Graceful handling of 401 Unauthorized responses
- Loading states, spinners, and error fallbacks everywhere

## Deployment
- Separate dev/staging/prod environments
- Health checks for API server
- CDN optimization for static assets

---
