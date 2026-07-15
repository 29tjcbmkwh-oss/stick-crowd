namespace _Scripts.Ads
{
    /// <summary>
    /// Central place for ad unit IDs and tuning. Ships using Google's OFFICIAL test ad units,
    /// which are safe to develop/test with. Before release: paste your real AdMob IDs into the
    /// Live* fields and set UseTestAds = false.
    ///
    /// IMPORTANT: never show your OWN real ads to yourself during testing — that violates
    /// AdMob policy and can get your account banned. Keep UseTestAds = true until release.
    /// </summary>
    public static class AdConfig
    {
        // static readonly (not const) so switching it doesn't produce "unreachable code" warnings.
        public static readonly bool UseTestAds = true;

#if UNITY_ANDROID
        // --- Google official TEST ids (Android) ---
        private const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewarded     = "ca-app-pub-3940256099942544/5224354917";
        // --- YOUR real Android ids go here ---
        private const string LiveInterstitial = "REPLACE_WITH_YOUR_ANDROID_INTERSTITIAL_ID";
        private const string LiveRewarded     = "REPLACE_WITH_YOUR_ANDROID_REWARDED_ID";
#elif UNITY_IOS
        // --- Google official TEST ids (iOS) ---
        private const string TestInterstitial = "ca-app-pub-3940256099942544/4411468910";
        private const string TestRewarded     = "ca-app-pub-3940256099942544/1712485313";
        // --- YOUR real iOS ids go here ---
        private const string LiveInterstitial = "REPLACE_WITH_YOUR_IOS_INTERSTITIAL_ID";
        private const string LiveRewarded     = "REPLACE_WITH_YOUR_IOS_REWARDED_ID";
#else
        // Editor / other platforms: ads never actually run here.
        private const string TestInterstitial = "unused";
        private const string TestRewarded     = "unused";
        private const string LiveInterstitial = "unused";
        private const string LiveRewarded     = "unused";
#endif

        public static string InterstitialId => UseTestAds ? TestInterstitial : LiveInterstitial;
        public static string RewardedId     => UseTestAds ? TestRewarded     : LiveRewarded;

        /// <summary>Show an interstitial on every Nth game-over (frequency cap for good UX + policy).</summary>
        public static readonly int InterstitialEveryNGameOvers = 2;
    }
}
