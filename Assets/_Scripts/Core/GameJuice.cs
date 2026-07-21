using DG.Tweening;
using TMPro;
using UnityEngine;
using _Scripts.Models;
using _Scripts.Utilities;

namespace _Scripts.Core
{
    /// <summary>
    /// Code-only game feel: gate feedback, battle shake, win confetti + victory jumps,
    /// lose slump. No scene or prefab edits required — safe for every future reskin.
    /// </summary>
    public static class GameJuice
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameFlowManager.onGameStateChange += HandleState;
        }

        private static void HandleState(GameState state)
        {
            switch (state)
            {
                case GameState.Win:
                    PlayConfetti();
                    CrowdVictoryJump();
                    FovKick(6f, 0.25f);
                    if (UIManager.Instance != null) UIManager.Instance.PlayCoinFountain();
                    break;
                case GameState.Lose:
                    CrowdSlump();
                    CameraShake(0.35f, 0.25f);
                    break;
                case GameState.Battle:
                case GameState.MiniBattle:
                    CameraShake(0.45f, 0.35f);
                    break;
            }
        }

        /// <summary>Called by CorridorController when the crowd passes through a gate.</summary>
        public static void OnGatePassed(Corridor corridor, bool positive)
        {
            if (corridor != null)
            {
                TMP_Text label = corridor.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.transform.DOKill(true);
                    label.transform.DOPunchScale(Vector3.one * 0.55f, 0.35f, 8, 0.7f);
                }
                Vector3 burstPos = corridor.transform.position + Vector3.up * 0.5f;
                BurstAt(burstPos, BrandPalette.ForChoice(positive), 18);
                SpawnFloatingText(burstPos + Vector3.up * 0.4f, GateDeltaText(corridor), positive);
            }
            FovKick(positive ? 4f : -3f, 0.15f);
        }

        // Same leading-glyph convention as the gate label itself (VisualOverhaul.cs) so the
        // floating combat-text reinforces rather than duplicates a different format.
        private static string GateDeltaText(Corridor corridor) => corridor.GetCorridorType() switch
        {
            Constants.CorridorTypes.Increase => $"+{corridor.increaseAmount}",
            Constants.CorridorTypes.Decrease => $"-{corridor.decreaseAmount}",
            Constants.CorridorTypes.Multiply => $"×{corridor.multiplyAmount}",
            Constants.CorridorTypes.Divide   => $"÷{corridor.divideAmount}",
            _ => "?"
        };

        // World-space floating text: a short up-and-fade combat-text readout of the gate's
        // effect, in the same Blue/Orange as the choice itself. Built entirely in code —
        // no prefab/scene dependency, consistent with the rest of this file.
        private static void SpawnFloatingText(Vector3 worldPos, string text, bool positive)
        {
            GameObject go = new GameObject("GateFeedbackText");
            go.transform.position = worldPos;

            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = BrandPalette.ForChoice(positive);
            // Guard against a null font material the same way UIManager.StyleHudLabel does —
            // see that method's comment for the exact crash this avoids.
            if (tmp.fontSharedMaterial != null)
            {
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color32(6, 8, 14, 255);
            }

            if (go.GetComponent<Billboard>() == null) go.AddComponent<Billboard>();

            Transform t = go.transform;
            t.DOMoveY(worldPos.y + 1.3f, 0.9f).SetEase(Ease.OutCubic);
            tmp.DOFade(0f, 0.9f).SetDelay(0.15f).SetEase(Ease.InQuad);
            Object.Destroy(go, 1.1f);
        }

        // ---------- camera ----------

        private static void FovKick(float delta, float inTime)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.DOKill();
            cam.DOFieldOfView(cam.fieldOfView + delta, inTime)
               .SetLoops(2, LoopType.Yoyo)
               .SetEase(Ease.OutQuad);
        }

        private static void CameraShake(float strength, float duration)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.transform.DOKill(true);
            cam.transform.DOShakePosition(duration, strength, 18, 90f, false, true);
        }

        // ---------- crowd reactions ----------

        private static void CrowdVictoryJump()
        {
            foreach (Cat cat in Object.FindObjectsOfType<Cat>())
            {
                Transform t = cat.transform;
                t.DOKill();
                t.DOJump(t.position, 0.6f + Random.value * 0.4f, 1, 0.45f)
                 .SetLoops(4, LoopType.Restart)
                 .SetDelay(Random.value * 0.2f);
            }
        }

        private static void CrowdSlump()
        {
            foreach (Cat cat in Object.FindObjectsOfType<Cat>())
            {
                Transform t = cat.transform;
                t.DOKill();
                t.DORotate(new Vector3(80f, t.eulerAngles.y, 0f), 0.6f)
                 .SetEase(Ease.OutBounce);
            }
        }

        // ---------- particles (built in code — no assets) ----------

        private static void PlayConfetti()
        {
            Camera cam = Camera.main;
            Vector3 pos = cam != null
                ? cam.transform.position + cam.transform.forward * 6f + Vector3.up * 2.5f
                : Vector3.up * 3f;
            BurstAt(pos, BrandPalette.Blue, 60, BrandPalette.Orange);
        }

        private static void BurstAt(Vector3 position, Color colorA, int count, Color? colorB = null)
        {
            GameObject go = new GameObject("JuiceBurst");
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB ?? colorA);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.gravityModifier = 1.1f;
            main.loop = false;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.3f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            ps.Play();
            Object.Destroy(go, 3f);
        }
    }
}
