# Expo Deployment Model

## Why Expo?

The choice comes down to **React Native without the native toolchain pain**. The alternatives are:

- **Bare React Native** — requires Xcode + Android Studio configured correctly on every dev machine, manual linking of native modules, managing CocoaPods, etc.
- **Expo** — abstracts all of that. You write TypeScript/JSX and Expo handles the native layer.

The specific wins here:
- **Expo Router** gives file-based routing (like Next.js) out of the box — no React Navigation boilerplate
- **Expo SDK** pre-bundles the most common native modules (camera, notifications, SecureStore, etc.) without needing Xcode to compile them
- **Single codebase** targets iOS, Android, and Web — which matters for this project

---

## How iOS Deployment Works with Expo

There are two distinct layers:

### 1. Building the Native Binary — EAS Build

Expo Application Services (EAS) is a cloud build service. Instead of needing a Mac with Xcode, you push your code and EAS compiles the `.ipa` (iOS) or `.apk`/`.aab` (Android) in the cloud. This matters a lot on Windows — you can't build an iOS binary locally without a Mac.

```bash
eas build --platform ios --profile preview
```

EAS handles code signing, provisioning profiles, and certificates for you.

### 2. Getting Builds to Testers

**TestFlight** is Apple's official beta distribution platform. The workflow is:

1. EAS Build produces a signed `.ipa`
2. EAS can automatically submit it to TestFlight (`eas submit`)
3. Testers install the TestFlight app from the App Store, then get invited to your beta

TestFlight is Apple-native — it's not Expo-specific. It's the standard channel for iOS betas before App Store release.

### 3. The "Shell App" Concept — Expo Go vs Development Builds

Expo offers two things here:

| | Expo Go | Development Build |
|---|---|---|
| What it is | Apple/Google app from the store | Your own app with Expo's dev runtime baked in |
| How it works | Scan a QR code, your JS loads inside it | Install a custom `.ipa` via TestFlight or USB |
| Limitation | **Only works with Expo SDK modules** — no custom native code | Supports any native module you add |
| Use case | Quick iteration during early dev | Testing the actual app experience |

**Expo Go** is the shell-app experience — testers install it once, then you push JS updates and they reload. However, once you add any native dependency outside the Expo SDK (a custom camera library, a payment SDK, etc.), Expo Go can't run it.

At that point you create a **development build** with `eas build --profile development`, distribute that `.ipa` via TestFlight, and from then on JS changes still hot-reload without rebuilding — only native code changes require a new build.

### 4. Over-the-Air JS Updates — EAS Update

Once you have a production app in the store, you can push JavaScript-only changes (bug fixes, UI tweaks) without going through App Store review:

```bash
eas update --branch production --message "Fix pick button bug"
```

Apple permits this for JS-only changes. Native code changes still require a full store submission.

---

## The Practical Workflow for This App

```
Dev machine (Windows)
      │
      ├─ Local:      Expo Go app on phone + QR code → instant reload
      │
      ├─ Beta:       eas build → TestFlight → testers install .ipa
      │
      └─ Production: eas submit → App Store review → release
                          ↑
                     EAS Update for JS-only hotfixes
```

## EAS Pricing (as of early 2026)

The main cost consideration is EAS Build:
- **Free tier** — limited monthly builds, sufficient during development for a solo project
- **Paid tier** — ~$99/mo for teams, priority build queue, more concurrent builds

EAS Update (OTA) has a generous free tier based on monthly active users.

---

## EAS Configuration

The build profiles live in `eas.json` at the project root. A typical setup:

```json
{
  "cli": {
    "version": ">= 10.0.0",
    "appVersionSource": "local"
  },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal",
      "ios": {
        "resourceClass": "m-medium"
      }
    },
    "preview": {
      "distribution": "internal",
      "env": {
        "EXPO_PUBLIC_API_BASE_URL": "https://api.sportdeets.com"
      },
      "ios": {
        "resourceClass": "m-medium"
      }
    },
    "production": {
      "autoIncrement": true,
      "env": {
        "EXPO_PUBLIC_API_BASE_URL": "https://api.sportdeets.com"
      },
      "ios": {
        "resourceClass": "m-medium"
      }
    }
  }
}
```

- **development** — builds the dev client (supports hot reload + custom native modules)
- **preview** — internal TestFlight distribution for QA/stakeholder testing
- **production** — App Store submission build

---

## One-Shot Preview Build for iPhone (Ad-Hoc Install)

Use this when you want to sideload a preview build onto your own iPhone without TestFlight review — useful for validating the app against prod APIs while off your dev network.

The `preview` profile in `eas.json` is set up for this: `distribution: internal` produces an ad-hoc-signed IPA that installs from an EAS-hosted URL. SignalR falls back to `EXPO_PUBLIC_API_BASE_URL` (`useSignalRClient.ts`), so the same env var that points the REST client at prod also routes the hub.

### Prerequisites (one-time)

- Active Apple Developer membership ($99/yr) on the Apple ID you'll use
- `eas-cli` installed (`npm i -g eas-cli`)
- Your iPhone's UDID — or skip and let `eas device:create` walk you through registering it

### Steps

Run from `src/UI/sd-mobile/`:

1. **Log in to EAS** (one-time):
   ```bash
   eas login
   ```
   Use the Expo account that owns the project (`app.json` `owner: "sportdeets"`).

2. **Register your iPhone for ad-hoc distribution** (one-time per device):
   ```bash
   eas device:create
   ```
   EAS prints a URL/QR. Open it on the iPhone, install the config profile from Settings → General → VPN & Device Management. EAS now knows the device and can include it in the ad-hoc provisioning profile.

3. **Build the preview IPA**:
   ```bash
   eas build --platform ios --profile preview
   ```
   First-run prompts:
   - "Generate a new Apple Distribution Certificate?" → **Yes** (let EAS manage credentials)
   - "Generate a new Ad Hoc Provisioning Profile?" → **Yes**
   - Apple ID login → the one tied to your Developer membership

   Build runs in EAS cloud (~10–20 min on `m-medium`).

4. **Install on iPhone**: When the build finishes EAS prints a URL of the form `https://expo.dev/accounts/sportdeets/projects/sportdeets/builds/<id>`. Open it in Safari on the iPhone → tap **Install**. The IPA installs directly via the ad-hoc profile — no TestFlight, no App Store review.

### Gotchas

- **Prod hub auth**: this is the first time the mobile client negotiates SignalR against `https://api.sportdeets.com/hubs/notifications`. Confirm in Seq that the hub returns 200 for the new connection after you log in on the device.
- **Device cap**: Apple's ad-hoc program allows 100 registered iPhones per year per account. Fine for personal testing; if you grow QA testers past that, move to TestFlight via `eas submit`.
- **Profile expiry**: ad-hoc provisioning profiles expire after 12 months. A rebuild after expiry just regenerates the profile.

---

## TestFlight Distribution to External Testers

This is the model for shipping builds to **people who aren't on your network and whose devices you don't want to register individually** — friends helping with beta testing, stakeholders, etc. They install Apple's TestFlight app from the App Store, tap your invite link, and the build appears on their phone. No UDIDs, no config profiles.

TestFlight has two tiers:

| | Internal | External |
|---|---|---|
| Tester cap | 100 | 10,000 |
| How they're added | By Apple ID in App Store Connect | By email **or** public invite link |
| Apple review required | No | **Yes, first time per group** (~24h, lighter than App Store review) |
| Subsequent builds in same group | Instant | Instant (no re-review) |
| Build lifetime | 90 days | 90 days |

For friends-testing the right answer is usually **External + public link** — you paste a URL in iMessage and they're in.

### Prerequisites (one-time)

- Apple Developer membership ($99/yr)
- App Store Connect record for the app — `Bundle ID` must match `app.json` (`com.sportdeets.mobile`)
- App Store Connect API key for non-interactive `eas submit` (optional but strongly recommended; otherwise every submit prompts for Apple ID + 2FA)

### One-Time Setup

1. **Create the app in App Store Connect** (https://appstoreconnect.apple.com → My Apps → `+`):
   - Bundle ID: `com.sportdeets.mobile` (must be registered in the Developer portal first; EAS does this on the first production build)
   - SKU: any unique string, e.g. `sportdeets-mobile`
   - Primary language: English

2. **Generate an ASC API key** (Users and Access → Keys → App Store Connect API → `+`):
   - Role: **App Manager** (or Admin)
   - Download the `.p8` file — Apple shows it once, never again
   - Record the **Key ID** and **Issuer ID** shown on that page

3. **Add a `submit` block to `eas.json`**:
   ```json
   "submit": {
     "production": {
       "ios": {
         "ascApiKeyPath": "./secrets/AuthKey_<KEY_ID>.p8",
         "ascApiKeyId": "<KEY_ID>",
         "ascApiIssuerId": "<ISSUER_ID>",
         "appleId": "you@example.com",
         "ascAppId": "<APP_ID_FROM_APP_STORE_CONNECT>"
       }
     }
   }
   ```
   Add `secrets/` to `.gitignore` — the `.p8` is a credential. Alternative: upload the key to EAS once with `eas credentials` and skip the local file.

4. **Create an external TestFlight group** in App Store Connect → TestFlight → External Testing → `+`:
   - Name it (e.g., `Friends`)
   - Toggle "Enable Public Link" → copy the URL — this is what you share

### Per-Build Workflow

From `src/UI/sd-mobile/`:

1. **Build the production IPA**:
   ```bash
   eas build --platform ios --profile production
   ```
   The `production` profile in `eas.json` already has `autoIncrement: true`, so the build number bumps automatically — Apple requires every TestFlight upload to have a unique, monotonically increasing build number.

2. **Submit to TestFlight**:
   ```bash
   eas submit --platform ios --latest
   ```
   `--latest` grabs the most recent successful build. Apple processes the upload (~5–15 min); it appears in App Store Connect → TestFlight under "iOS Builds."

3. **Attach the build to the external group**:
   - First time: select the build, add it to the `Friends` group, fill in "What to Test" notes → **Submit for Beta App Review**. Wait ~24h.
   - Subsequent builds: just attach to the group — no review needed because the group is already approved.

4. **Share the public link**. Friends:
   - Install TestFlight from the App Store
   - Open the link → tap "Accept" → tap "Install"
   - Build is on their phone, no network sharing, no UDID

### Hotfixes Between Builds

For JS/asset-only changes (most UI tweaks, copy fixes, bug fixes that don't touch native modules), skip the rebuild:

```bash
eas update --branch production --message "Fix pick button overflow"
```

Existing TestFlight installs pick up the update on next app launch. Native code or SDK changes still require a fresh `eas build` + `eas submit`.

### Gotchas

- **First external review delay**: budget 24h before the first invite. Once the group is approved, every subsequent build skips review. Plan the first submission ahead of the day you want friends testing.
- **90-day build expiry**: TestFlight builds stop launching after 90 days. If a build sits unused that long, ship a new one before re-engaging testers.
- **Export compliance**: `ITSAppUsesNonExemptEncryption: false` is already in `app.json` — keeps Apple from prompting on every upload. If the app ever adds custom crypto (beyond HTTPS), this has to change.
- **Privacy nutrition labels**: TestFlight upload will warn if these are missing but won't block. The actual App Store submission will block — easier to fill them in once when creating the app record.
- **Build numbers can't go backward**: if `autoIncrement` ever desynchronizes (e.g., you build outside EAS), App Store Connect rejects the upload. Fix is `eas build:version:set` to a number above the highest one already uploaded.
- **Tester invite emails go to spam**: if a friend says they didn't get the email, point them at the public link instead — it always works.
