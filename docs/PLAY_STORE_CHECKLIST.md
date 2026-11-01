# Neon Brawl – Play Store checklist

Package: `com.mohitgames.neonbrawl` · Version 1.0.1 (code 2) · Unity 6000.5.8f1

## Already done in the project
- App icon (legacy, round, adaptive) from `Assets/_Project/Art/UI/app_icon.png`; 512 px store icon: `docs/store/icon_512.png`.
- Ads: Google AdMob (Google Mobile Ads 11.6.0) with Google's consent form (UMP).
  - App ID `ca-app-pub-8048914278660224~6107316062` (set in `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`).
  - Rewarded unit `ca-app-pub-8048914278660224/3481152725` (x2 coins, daily x2, free coins).
  - Interstitial and banner code is ready; their unit ids are empty until you create them (see below).
  - Unity Ads adapter for AdMob mediation is installed (Unity Ads game id 6216551 Android / 6216550 iOS).
  - All ids live in one place: `Assets/_Project/Scripts/Game/Core/AdMobAds.cs` → `AdIds`.
- Privacy policy: `docs/privacy-policy.html` → https://mohitorange.github.io/2dshooting/privacy-policy.html (after Pages is on).
- Friends (Unity Friends 1.3.0): friend codes, requests, online status, invites. Needs **Friends** switched on for the project in the Unity Cloud dashboard (it already works in testing).
- In-app purchases (Unity IAP): `gems_80`, `gems_500`, `gems_1200`, `gems_2500` (consumable), `remove_ads` (non-consumable).

## 1. GitHub Pages (privacy policy link)
GitHub → repo **2dshooting** → Settings → Pages → Source: *Deploy from a branch* → Branch `main`, folder `/docs` → Save.
After ~1 minute the policy is live at https://mohitorange.github.io/2dshooting/privacy-policy.html

## 2. AdMob console
1. **Create 2 more ad units** for the Neon Brawl Android app, with *partner bidding unticked*:
   - Interstitial → paste its id into `AdIds.Interstitial`
   - Banner → paste its id into `AdIds.Banner`
2. **Privacy & messaging** → create a *European regulations (GDPR)* message for the app (Google's consent form uses it).
3. **Mediation** → create a mediation group (Rewarded, Android) → *Add ad source* → **Unity Ads (bidding)** → enter game id `6216551` and the Unity placement ids (Unity dashboard → Ad units, e.g. `Rewarded_Android`). Repeat for Interstitial / Banner when you create them.
4. Privacy & messaging → add *Unity Ads* to the GDPR and US state regulations ad partner lists.
5. **app-ads.txt**: AdMob checks `https://<your developer website>/app-ads.txt`. The file is in `docs/app-ads.txt`. For a github.io site it must be at the domain root, so create a repo named `MohitOrange.github.io` with that file, and use `https://mohitorange.github.io` as the developer website in Play Console.
6. Don't tap your own real ads. Install `NeonBrawl-test-ads.apk` on your phone for testing.

## 3. Upload key (needed for the .aab)
Unity → Edit → Project Settings → Player → Android → Publishing Settings → Keystore Manager → *Create new* → save it as `Keystore/neonbrawl-upload.keystore` (this folder is git-ignored). Then create `Keystore/keystore.properties`:
```
storeFile=Keystore/neonbrawl-upload.keystore
storePassword=YOUR_PASSWORD
keyAlias=YOUR_ALIAS
keyPassword=YOUR_KEY_PASSWORD
```
Back up both files somewhere safe (Google Drive etc.). Then run **Neon Brawl ▸ Build ▸ Final** to get `Builds/Android/NeonBrawl.aab`.

## 4. Play Console
- Create app → upload `NeonBrawl.aab` to *Internal testing* first.
- Store listing: icon `docs/store/icon_512.png`, a 1024×500 feature graphic, 2–8 phone screenshots (landscape).
- App content:
  - Privacy policy URL: https://mohitorange.github.io/2dshooting/privacy-policy.html
  - Ads: **Yes, contains ads**
  - Target audience: 13+ (not designed for children)
  - Content rating questionnaire: cartoon violence (shooter), user chat/voice between players, in-app purchases
  - Data safety (summary): collects *Device or other IDs* (advertising ID) and *App interactions / diagnostics* for advertising, analytics and fraud prevention, shared with ad partners; *Audio* only for live voice chat (not stored); purchase history handled by Google Play; data encrypted in transit; users can request deletion via the contact in the policy.
- In-app products: create `gems_80`, `gems_500`, `gems_1200`, `gems_2500`, `remove_ads` with the same ids.
