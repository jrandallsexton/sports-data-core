# sportDeets Feature Roadmap

This document tracks planned feature work, technical debt cleanup, and future enhancements for the sportDeets application.

---

## 🏈 Current Application Summary
- Pick'em style NCAA football app
- Supports multiple groups (private leagues, work pools, public contests)
- Pick against the spread (ATS) format
- Dual views: Card View and Grid View
- AI Insights available to subscribed users

---

## 🚀 Upcoming Features

### Picks and Matchups
- [ ] Allow users to select **Straight Up** or **Against the Spread** when picking
- [ ] Add **Over-Under Picks** support
- [ ] Display **game time** and **location** consistently in both views
- [ ] Separate "Consensus" into:
  - [ ] Spread consensus
  - [ ] Over-Under consensus
  - [ ] Straight Up consensus
- [ ] Add color coding for confidence levels or consensus percentages

### Submissions
- [ ] Save user picks to backend API (currently simulated with `console.log`)
- [ ] Validate pick completeness before submission (e.g., no missing picks)

### API Integrations
- [ ] Real API endpoints for:
  - [ ] Matchups by Group/Week
  - [ ] Leaderboard by Group/Week
  - [ ] User Picks Submit/Fetch
- [ ] Graceful API error handling
- [ ] Automatic retries/backoff (using Axios interceptors)

### User Profile
- [ ] Editable display name
- [ ] Update notification settings (email alerts, push notifications)
- [ ] Light/Dark theme toggle saved to user profile

---

## 🛠️ Technical Debt / Cleanup

- [ ] Move inline styles inside components into proper `.css` modules
- [ ] Normalize flexbox and grid layouts across all pages
- [ ] Extract constants (like Group names, Week numbers) into `/src/constants/`
- [ ] Improve accessibility (ARIA labels for buttons, etc.)

---

## 🎯 Future Nice-to-Haves

- [ ] **Public Leaderboards** with top overall users
- [ ] **Confidence Points** system for ranked picks
- [ ] **Live Score Updates** via external API
- [ ] Mobile app using React Native (stretch goal)
- [ ] Admin panel for managing groups, matchups, etc.

---

# sportDeets – Current Application Modules

## Core Components
- LandingPage (public entry point)
- HomePage (dashboard after login)
- PicksPage (make weekly picks)
- LeaderboardPage (see rankings)
- MessageBoardPage (user threads)
- SettingsPage (theme + notifications)

## PicksPage Child Components
- GroupWeekSelector
- MatchupList (for Card view)
- MatchupGrid (for Grid view)
- MatchupCard
- SubmitButton
- InsightDialog

## Infrastructure
- `apiWrapper.js` – centralized Axios API client
- JSON Server mock backend (port 3001)
- ThemeContext (for light/dark mode switching)

## Layout
- MainApp.js
- Sidebar navigation
- Responsive layout support

## Dev Tools
- `concurrently` to run React + JSON Server
- `json-server` for mock API


_Last Updated: April 28, 2025_
