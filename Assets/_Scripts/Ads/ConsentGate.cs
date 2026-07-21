using System;
using UnityEngine;
#if ADMOB_ENABLED
using GoogleMobileAds.Ump.Api;
#endif

namespace _Scripts.Ads
{
    /// <summary>
    /// Resolves GDPR/UK (EEA + UK) consent via Google's UMP SDK before any ad request is made.
    /// AdManager calls RequestConsent() once at startup, before initializing the ad service.
    /// Outside the EEA/UK, UMP determines consent isn't required and this becomes a same-frame
    /// no-op that still calls onReady — one code path for every region, no manual geo-check.
    ///
    /// No-op (calls onReady immediately) when ADMOB_ENABLED isn't defined, matching how
    /// NullAdService stands in for AdMobService elsewhere in this folder.
    /// </summary>
    public static class ConsentGate
    {
        public static void RequestConsent(Action onReady)
        {
#if ADMOB_ENABLED
            var request = new ConsentRequestParameters
            {
                // Leave false unless this app is child-directed (COPPA/child-consent flow is
                // a different, stricter path than the standard UMP form and isn't wired here).
                TagForUnderAgeOfConsent = false,
            };

            ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null)
                {
                    Debug.LogWarning($"[Consent] UMP update failed: {updateError}. " +
                                      "Proceeding without a resolved consent state.");
                    onReady?.Invoke();
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning($"[Consent] UMP form error: {formError}");

                    onReady?.Invoke();
                });
            });
#else
            onReady?.Invoke();
#endif
        }

        /// <summary>True once the user can legally be served ads under their resolved consent state.</summary>
        public static bool CanRequestAds
        {
            get
            {
#if ADMOB_ENABLED
                return ConsentInformation.CanRequestAds();
#else
                return true;
#endif
            }
        }
    }
}
