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
    "version": ">= 10.0.0"
  },
  "build": {
    "development": {
      "developmentClient": true,
      "distribution": "internal"
    },
    "preview": {
      "distribution": "internal",
      "ios": { "simulator": false }
    },
    "production": {
      "autoIncrement": true
    }
  },
  "submit": {
    "production": {
      "ios": {
        "appleId": "your@apple.id",
        "ascAppId": "1234567890"
      }
    }
  }
}
```

- **development** — builds the dev client (supports hot reload + custom native modules)
- **preview** — internal TestFlight distribution for QA/stakeholder testing
- **production** — App Store submission build
