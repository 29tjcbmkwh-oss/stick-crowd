// Real AdMob implementation. Compiled ONLY when the "ADMOB_ENABLED" scripting define symbol is set.
// This keeps the project compiling BEFORE the Google Mobile Ads SDK is imported.
//
// SETUP (do this once, after installing the SDK):
//   1. Import the Google Mobile Ads Unity SDK (Assets > Import Package, or via UPM).
//   2. Set your AdMob App ID:  Assets > Google Mobile Ads > Settings.
//   3. Add ADMOB_ENABLED to:   Edit > Project Settings > Player > Other Settings >
//                              Scripting Define Symbols (for Android AND iOS).
// After that, AdManager automatically uses this class instead of NullAdService.
#if ADMOB_ENABLED
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace _Scripts.Ads
{
    public sealed class AdMobService : IAdService
    {
        private InterstitialAd _interstitial;
        private RewardedAd _rewarded;
        private Action _interstitialOnClosed;

        public bool IsInterstitialReady => _interstitial != null && _interstitial.CanShowAd();
        public bool IsRewardedReady     => _rewarded != null && _rewarded.CanShowAd();

        public void Initialize(Action onInitialized = null)
        {
            // Marshal all ad callbacks onto Unity's main thread so it's safe to touch GameObjects/UI.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
                onInitialized?.Invoke();
            });
        }

        // ---------------- Interstitial ----------------

        public void LoadInterstitial()
        {
            if (_interstitial != null) { _interstitial.Destroy(); _interstitial = null; }

            InterstitialAd.Load(AdConfig.InterstitialId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] Interstitial load failed: {error}");
                    return;
                }
                _interstitial = ad;
                ad.OnAdFullScreenContentClosed += HandleInterstitialClosed;
                ad.OnAdFullScreenContentFailed += e =>
                {
                    Debug.LogWarning($"[Ads] Interstitial show failed: {e}");
                    HandleInterstitialClosed();
                };
            });
        }

        private void HandleInterstitialClosed()
        {
            var cb = _interstitialOnClosed;
            _interstitialOnClosed = null;
            cb?.Invoke();
            LoadInterstitial(); // preload the next one
        }

        public void ShowInterstitial(Action onClosed = null)
        {
            if (IsInterstitialReady)
            {
                _interstitialOnClosed = onClosed;
                _interstitial.Show();
            }
            else
            {
                onClosed?.Invoke();   // nothing to show: don't block gameplay
                LoadInterstitial();
            }
        }

        // ---------------- Rewarded ----------------

        public void LoadRewarded()
        {
            if (_rewarded != null) { _rewarded.Destroy(); _rewarded = null; }

            RewardedAd.Load(AdConfig.RewardedId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Ads] Rewarded load failed: {error}");
                    return;
                }
                _rewarded = ad;
            });
        }

        public void ShowRewarded(Action onRewardEarned, Action onFailedOrDismissed = null)
        {
            if (!IsRewardedReady)
            {
                onFailedOrDismissed?.Invoke();
                LoadRewarded();
                return;
            }

            var earned = false;
            _rewarded.OnAdFullScreenContentClosed += () =>
            {
                if (!earned) onFailedOrDismissed?.Invoke();
                LoadRewarded(); // preload the next one
            };
            _rewarded.OnAdFullScreenContentFailed += e =>
            {
                Debug.LogWarning($"[Ads] Rewarded show failed: {e}");
                if (!earned) onFailedOrDismissed?.Invoke();
                LoadRewarded();
            };
            _rewarded.Show(_ =>
            {
                earned = true;
                onRewardEarned?.Invoke();
            });
        }
    }
}
#endif
