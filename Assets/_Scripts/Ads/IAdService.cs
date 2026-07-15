using System;

namespace _Scripts.Ads
{
    /// <summary>
    /// Ad provider abstraction. The game only ever talks to this interface (via AdManager),
    /// so we can swap AdMob for a mediation SDK (AppLovin MAX, LevelPlay, etc.) later WITHOUT
    /// touching gameplay code. See AdMobService (real) and NullAdService (safe fallback).
    /// </summary>
    public interface IAdService
    {
        void Initialize(Action onInitialized = null);

        void LoadInterstitial();
        bool IsInterstitialReady { get; }
        /// <summary>Shows an interstitial if ready. onClosed fires when the ad is dismissed
        /// (or immediately if no ad was available), so gameplay can safely resume after it.</summary>
        void ShowInterstitial(Action onClosed = null);

        void LoadRewarded();
        bool IsRewardedReady { get; }
        /// <summary>Shows a rewarded ad. onRewardEarned fires ONLY if the user watched to completion.
        /// onFailedOrDismissed fires if the ad was closed early, failed, or was not available.</summary>
        void ShowRewarded(Action onRewardEarned, Action onFailedOrDismissed = null);
    }

    /// <summary>
    /// No-op fallback used when no ad SDK is compiled in (ADMOB_ENABLED not defined) or in the
    /// Editor. Lets the whole game run and build without the SDK present. Rewarded calls report
    /// "failed" so callers never wrongly hand out a reward when no ad actually played.
    /// </summary>
    public sealed class NullAdService : IAdService
    {
        public bool IsInterstitialReady => false;
        public bool IsRewardedReady => false;

        public void Initialize(Action onInitialized = null) => onInitialized?.Invoke();
        public void LoadInterstitial() { }
        public void ShowInterstitial(Action onClosed = null) => onClosed?.Invoke();
        public void LoadRewarded() { }
        public void ShowRewarded(Action onRewardEarned, Action onFailedOrDismissed = null)
            => onFailedOrDismissed?.Invoke();
    }
}
