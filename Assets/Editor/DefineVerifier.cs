// Logs at editor load whether ADMOB_ENABLED reached the Assembly-CSharp compilation
// (set via Assets/csc.rsp). Confirms AdMobService — not NullAdService — is the live path.
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DefineVerifier
{
    static DefineVerifier()
    {
#if ADMOB_ENABLED
        Debug.Log("[DefineVerifier] ADMOB_ENABLED = ON (AdMobService is the active ad path)");
#else
        Debug.Log("[DefineVerifier] ADMOB_ENABLED = OFF (NullAdService — no real ads)");
#endif
    }
}
