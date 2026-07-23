using System;
using UnityEngine;

namespace _Scripts.Ads
{
    /// <summary>
    /// The single entry point the game uses for ads. Self-contained and auto-bootstrapping:
    /// it creates itself before the first scene loads, so AdManager.Instance is NEVER null and
    /// no manual scene setup is required. Depends on nothing in this project, so it drops
    /// unchanged into any future reskin.
    ///
    /// Ad-service init is gated behind ConsentGate (GDPR/UK via Google UMP), so
    /// IsInterstitialReady/IsRewardedReady correctly report false until consent resolves and
    /// the underlying SDK finishes loading its first ads — same "no ad ready yet, don't block
    /// gameplay" shape callers already handle, just possibly true for slightly longer on
    /// first launch in the EEA/UK while the consent form is shown.
    ///
    /// Usage from gameplay code:
    ///   AdManager.Instance.NotifyGameOver();                       // interstitial every Nth loss
    ///   AdManager.Instance.ShowRewarded(onEarned, onFailed);       // e.g. revive / 2x coins
    /// </summary>
    public sealed class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        private IAdService _service;
        private int _gameOverCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[AdManager]");
            go.AddComponent<AdManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if ADMOB_ENABLED
            _service = new AdMobService();
#else
            _service = new NullAdService();
#endif
            // Consent (GDPR/UK via Google UMP) must resolve before the first ad request.
            // ConsentGate no-ops straight to onReady when ADMOB_ENABLED isn't set, or
            // immediately outside the EEA/UK once UMP determines consent isn't required —
            // so this never adds a real delay for the common case, only when a form is due.
            ConsentGate.RequestConsent(() => _service.Initialize());
        }

        public bool IsInterstitialReady => _service != null && _service.IsInterstitialReady;
        public bool IsRewardedReady     => _service != null && _service.IsRewardedReady;

        /// <summary>
        /// Call every time the player loses. Shows an interstitial on every Nth game-over
        /// (see AdConfig.InterstitialEveryNGameOvers). onContinue always fires afterwards
        /// (immediately if no ad is shown), so it's safe to drive your game-over UI from it.
        /// </summary>
        public void NotifyGameOver(Action onContinue = null)
        {
            _gameOverCount++;
            var everyN = Mathf.Max(1, AdConfig.InterstitialEveryNGameOvers);
            if (_gameOverCount % everyN == 0 && IsInterstitialReady)
            {
                _Scripts.Analytics.Analytics.Ad("shown", "interstitial");
                _service.ShowInterstitial(() =>
                {
                    _Scripts.Analytics.Analytics.Ad("completed", "interstitial");
                    onContinue?.Invoke();
                });
            }
            else
            {
                onContinue?.Invoke();
            }
        }

        /// <summary>
        /// Show a rewarded ad. onRewardEarned fires ONLY if the user watched to completion;
        /// onFailedOrDismissed fires if they closed it early, it failed, or none was available.
        /// </summary>
        public void ShowRewarded(Action onRewardEarned, Action onFailedOrDismissed = null)
        {
            if (_service == null)
            {
                _Scripts.Analytics.Analytics.Ad("failed", "rewarded");
                onFailedOrDismissed?.Invoke();
                return;
            }
            // "requested" rather than "shown": the NullAdService (and a not-loaded AdMob
            // unit) fails without ever displaying anything, so logging "shown" here would
            // overcount. completed/failed from the callbacks are the truthful outcomes.
            _Scripts.Analytics.Analytics.Ad("requested", "rewarded");
            _service.ShowRewarded(() =>
            {
                _Scripts.Analytics.Analytics.Ad("completed", "rewarded");
                onRewardEarned?.Invoke();
            }, () =>
            {
                _Scripts.Analytics.Analytics.Ad("failed", "rewarded");
                onFailedOrDismissed?.Invoke();
            });
        }
    }
}
