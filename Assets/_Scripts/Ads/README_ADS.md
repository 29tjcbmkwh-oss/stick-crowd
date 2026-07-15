# Ads module (AdMob, mediation-ready)

Self-contained ad system. The game talks only to `AdManager`, which talks to an `IAdService`.
Today that's `AdMobService`; swapping to AppLovin MAX / LevelPlay later = add one new
`IAdService` implementation, no gameplay changes.

## Files
- `IAdService.cs` — the abstraction + `NullAdService` (no-op fallback so the game runs without any SDK).
- `AdConfig.cs` — ad unit IDs (test vs live) + frequency cap. **Edit this to go live.**
- `AdMobService.cs` — real AdMob implementation, compiled only when `ADMOB_ENABLED` is defined.
- `AdManager.cs` — the entry point. Auto-creates itself before the first scene (never null).

## One-time setup (after Unity is installed)
1. **Import the Google Mobile Ads Unity SDK** (v8.0.0 or newer — the API here uses `new AdRequest()`
   and static `InterstitialAd.Load(...)`, which are v8+). Get it from Google's GitHub releases or UPM.
2. **Set your AdMob App ID:** `Assets > Google Mobile Ads > Settings`.
   - Test App IDs (safe for development):
     - Android: `ca-app-pub-3940256099942544~3347511713`
     - iOS: `ca-app-pub-3940256099942544~1458002511`
3. **Enable the real code:** `Edit > Project Settings > Player > Other Settings >
   Scripting Define Symbols` → add `ADMOB_ENABLED` (do it for **Android** and **iOS** tabs).
4. Press Play. You should see test ads. (In `AdConfig`, `UseTestAds = true` → Google's test ads.)

## Going live (before store release)
- In `AdConfig.cs`, paste your real ad unit IDs into the `Live*` fields.
- Set `UseTestAds = false`.
- Put your real AdMob App ID in the Google Mobile Ads Settings.
- ⚠️ Never click your own live ads while testing — it can get your AdMob account banned.
  Keep `UseTestAds = true` until you actually ship.

## How gameplay uses it
- Interstitial is already wired: `GameFlowManager.GameOver()` calls
  `AdManager.Instance.NotifyGameOver()` → shows an interstitial every 2nd loss.
- Rewarded (optional, wire to a UI button when ready):
  ```csharp
  AdManager.Instance.ShowRewarded(
      onRewardEarned: () => { /* revive the player / give 2x coins */ },
      onFailedOrDismissed: () => { /* they didn't finish the ad — grant nothing */ });
  ```

## Mediation later (AppLovin MAX with AdMob inside)
Add `MaxAdService : IAdService`, guard it behind a `MAX_ENABLED` define, and switch the one
line in `AdManager.Awake()`. Gameplay code is untouched because it only knows `AdManager`.
