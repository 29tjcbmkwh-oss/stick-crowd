# Project Vision — "Blue vs Orange" Stickman Runner (working title)

> AI-readable master brief. A fresh session should be able to read this file alone and
> understand the entire project without re-asking the owner. Last updated: 2026-07-15.

## 1. One-line summary
A 3D "hyper-casual" endless runner for mobile: a simple stickman auto-runs forward down
a track, the player steers left/right to dodge obstacles and grab collectibles, aiming
for a high score. Built by **forking and completely reskinning** an existing MIT-licensed
Unity game (AwesomeRunner). Monetized with ads.

## 2. The strategy (most important context)
The owner is a **solo indie developer** whose proven, money-making method is:
**fork an MIT-licensed game → rebrand → reskin the visuals → modify lightly → ship.**
He is NOT building games from scratch.

This project is deliberately designed as a **reusable reskin template**, not a one-off:
- Build ONE runner now (this "Blue vs Orange" stickman theme = Game #1).
- Then produce MANY more games that are *mechanically identical* but **visually distinct**
  ("very similar to all the next ones but with obvious visual differences").
- Therefore: keep the mechanic as SIMPLE and REPEATABLE as possible. Every extra
  mechanic is code that must be re-touched for every future reskin. The whole point is
  that future games change **art + naming only**, not code.

## 3. The base we are forking (UPDATED — base changed on owner's decision)
- **Repo:** https://github.com/ferend/crowd-runner-clone  (cloned to `./base`)
- **License:** MIT (© 2022 F. Eren Dalçık). Commercial use/modify/redistribute permitted.
  Keep the MIT `LICENSE` + attribution.
- **Engine:** Unity **2021.3.7f1**.
- **Why this base:** it's a Crowd Runner / Count Masters clone — the exact stickman-crowd
  genre the owner wants (matches his TikTok reference better than a plain runner).
  Clean architecture (ArmyController, ArmyMovementController, BattleController,
  CorridorController, gates/formations, Boss/MiniBoss, Score/UI/Audio managers), plus
  DOTween tweening and NiceVibrations haptics built in.
- **Key fact:** the base ships with **NO character art** ("codebase only") — crowd unit is a
  placeholder "Cat". This is ideal: we drop the Sketchfab stickman in as the crowd/boss.
- **NOTE — mechanic updated:** earlier the plan was a SOLO forward-runner. Owner switched
  to this crowd-runner base, so the mechanic is now: lead a CROWD of stickmen down a
  corridor, through gates (grow/shrink), mini-battles, and an end-boss fight. This is the
  new locked mechanic.

### Legal combine (all mix-safe)
- crowd-runner-clone (MIT) = game code (base)
- Sketchfab "Hyper Casual Character" (CC-BY) = stickman crowd + boss art  [see 4b]
- CountMasters_Prototype (MIT, cloned to ./reference/countmasters) = donor for stickman
  run/idle/jump animations if needed
- Everything with NO license (SnakeRunner, Color-Bump, Hole vs Colors, Sort It, etc.) = NOT used.

## 4. Core gameplay (LOCKED to the crowd-runner base — reskin, don't rebuild)
- **Mechanic:** lead a CROWD of stickmen down a corridor. Steer left/right; pass through
  GATES that grow/shrink the crowd; survive obstacles; mini-battles; finish at an END BOSS
  fight (your crowd vs the boss). All of this ALREADY EXISTS in the base (ArmyController,
  gates/formations, BattleController, Boss/MiniBoss).
- **Controls:** touch drag left/right (base has touch controls + haptics).
- **"Enemy + help" framing** maps naturally onto this base:
  - Enemy = the Boss / MiniBoss and obstacles (already in base).
  - Help  = the gates that grow your crowd (already in base).
- **Reskin scope:** replace the placeholder "Cat" crowd unit with the Sketchfab stickman,
  reskin the corridor/gates/boss to the Blue-vs-Orange flat-color look. Code stays; art + naming change.

## 4b. Player character asset (CONFIRMED)
- **Model:** "Hyper Casual Character" by **Marco Zakaria** on Sketchfab
  (https://sketchfab.com/3d-models/hyper-casual-charcter-9990bda2c5a240c28ef0687d94d22c5d).
- **License:** **CC-BY (Creative Commons Attribution)** — commercial use ALLOWED; author
  MUST be credited. Verified via Sketchfab API 2026-07-15.
- **Specs:** rigged + animated, 1,233 verts / 2,459 faces (low-poly, mobile-ideal), downloadable.
- **Why:** gives the "3D stickman feel" the owner wants out of the box; Mixamo-compatible rig
  slots into AwesomeRunner (which already uses Mixamo animations). Solves the "AwesomeRunner
  looks too basic" concern by swapping the character rather than changing code.

## ATTRIBUTION OBLIGATIONS (must ship in an in-game credits screen + repo README)
1. **Vladimir Pirozhenko** — game code (AwesomeRunner, MIT).
2. **Marco Zakaria** — stickman character (Sketchfab, CC-BY).

## 5. Visual theme — Game #1 ("Blue vs Orange" stickman)
Match this TikTok reference the owner provided (@jaybigamer1 "Blue vs Orange stickman troll race"):
- Simple **stickman** character (capsule/stick figure — simpler to model than the base's Jammo).
- **Dark track** narrowing to the horizon over a **bright flat void background** (the clip uses vivid green).
- **Red/blue bars & crosses** across the track as obstacles.
- **Rows of colored dots + coins** as collectibles.
- **Dust/smoke trail** behind the runner (base likely has a particle trail to reuse).
- Flat, saturated, minimal art style. No realistic textures.
- NOTE: we take the LOOK of the clip, not its race mechanic. Ours is solo.

## 6. Platform & distribution
- **Target:** Mobile — **Android AND iOS**.
  - Android = path of least resistance (repo already configured; $25 one-time Play account).
  - iOS = requires a Mac for builds + $99/yr Apple Developer account + stricter review.
- Unity exports to both from one project.

## 7. Monetization
- **Rewarded + interstitial ads** (the hyper-casual standard).
  - Interstitial: short ad shown between runs (e.g. every N game-overs).
  - Rewarded: optional "watch to revive" / "watch for 2x coins".
- **Ad SDK decision:** owner currently has **Google AdMob** and is interested in more later.
  Plan: build the ad code behind a small `IAdService` interface; implement **AdMob first**
  (ship game #1 with the existing account). Architect so we can later drop in **AppLovin MAX
  mediation (with AdMob as one mediated source)** without changing gameplay code. No ads
  exist in the base — this is the main piece of NEW code, built as a REUSABLE module so
  every future reskin inherits it.
- No backend/accounts/login required for v1.

## 8. Explicitly OUT of scope for v1
- Multiplayer / real opponents / racing.
- Combat, boss fights, free-roam movement.
- Online backend, user accounts, cloud saves, leaderboards (local high score only).
- Cosmetic IAP shop (possible later; not v1).

## 8b. BUILD STATUS (updated 2026-07-15)
- ✅ Base cloned to `./base` (crowd-runner-clone). Asset donor in `./reference/countmasters`.
- ✅ Rebranded: productName "Stick Crowd", id `com.yourstudio.stickcrowd` (PLACEHOLDERS — owner
  to supply real studio + game name).
- ✅ `base/CREDITS.md` added (Dalçık MIT + Zakaria CC-BY).
- ✅ **Ad module built** (headless): `base/Assets/_Scripts/Ads/` — IAdService + NullAdService,
  AdConfig (test ids, live placeholders), AdMobService (guarded by `ADMOB_ENABLED`), AdManager
  (auto-bootstrapping singleton). Interstitial wired into `GameFlowManager.GameOver()`.
  See `base/Assets/_Scripts/Ads/README_ADS.md` for the import/define/live-id steps.
- ✅ Unity 2021.3.7f1 installed + licensed (Personal via Hub; access token cached to 2027).
  Editor registered in Unity Hub; project added to Hub. NOTE: editor is Intel/Rosetta.
- ✅ **Stickman swap DONE + verified in Play mode** (blue stickman idles on track; boss orange 2.2x).
  Character = CountMasters (MIT) "Asset Unlock" rig, NOT the Sketchfab model (owner never
  downloaded it; MIT fallback used instead — update CREDITS if Sketchfab added later).
  StickmanSetup.cs auto-scales rig to collider height and snaps feet to unit origin plane.
- ✅ Android toolchain assembled headlessly: AndroidPlayer module installed into
  /Applications/Unity/PlaybackEngines; NDK r21d (21.3.6528147) + platform-33 in
  ~/Library/Android/sdk; Temurin JDK8 at /Users/a/blue-vs-orange-runner/tools/jdk8.
- ✅ **APK BUILT 2026-07-16** — `base/Builds/StickCrowd.apk` (34 MB, IL2CPP, arm64-v8a +
  armeabi-v7a, debug-signed, target SDK 33). Copy on Desktop. Build time ~7.5 min.
- **How to rebuild (any agent/teammate):** open project in Unity 2021.3.7f1 →
  `Tools > Stickman > Build Debug APK`; or create a file named `autobuild-request` in the
  project root and open/focus the editor (AutoBuildTrigger builds on load). Batch mode does
  NOT work (Hub entitlement license unavailable to CLI-spawned editors — verified repeatedly).
- **Working toolchain matrix (do not "upgrade" any of these):** JDK8 (../tools/jdk8),
  cmdline-tools 2.1 (Java-8 compatible — newer versions crash under Unity's JDK8 with JNI
  errors), build-tools 30.0.3 (Unity 2021's stated max), platform-33, NDK r21d
  (sdk/ndk/21.3.6528147). Paths applied in code via AndroidExternalToolsSettings
  (EditorPrefs writes do NOT register).
- ⚠️ **16 KB page-size flag (for production later):** Unity 2021 emits 4KB-aligned native
  libs. New Android 16KB-page devices/images refuse to load them ("Unable to initialize the
  Unity Engine" on the API-37 ps16k emulator). Real 4KB phones are fine. Production/Play
  longer-term fix = engine upgrade to Unity 2022.3 LTS+ (which emits 16KB-aligned libs).
- ⏳ NEXT (owner directive 2026-07-16): placeholders stay for now; Play Console = team's job.
  Focus on the GAME: full playability audit (start→gates→battle→win/lose→restart), fix what's
  broken, import Google Mobile Ads SDK + ADMOB_ENABLED + real ad unit IDs (owner has AdMob),
  make it genuinely monetizable. Then iOS later.

## 9. Open decisions (tracked in chat, to be finalized)
- Ad SDK / mediation choice.
- Final game name + package id (com.<owner>.<name>).
- Analytics (optional; GameAnalytics is free and standard for HC).
- Where the git repo lives (owner's GitHub).
