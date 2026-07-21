using _Scripts.Controllers;
using _Scripts.Core;
using UnityEngine;

namespace _Scripts.Models
{
    /// <summary>
    /// World-space two-tone health bar floating above the boss (reference-standard for the
    /// genre; HOD A-list A6). Fully code-built in Awake — no prefab/YAML dependency — using
    /// the same world-canvas pattern as the crowd counter chip. Shows full red-backed green
    /// until the tug-of-war battle starts, then drains with BattleController.EnemyFraction.
    /// Sprite-less Images render plain quads, so the fill is a left-anchored child scaled in
    /// x rather than an Image.fillAmount (which needs a sprite to slice).
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        private RectTransform _fill;
        private BattleController _battle;
        private Canvas _canvas;

        private void Awake()
        {
            _battle = FindObjectOfType<BattleController>();

            // Unparented on purpose: the boss is scaled ~2.2x, and a parented canvas both
            // inherits that scale (5+ units wide) and pushes a local y-offset ~7 world units
            // up, out of frame (the bar was invisible in the 18:44 capture). World-space
            // sibling + follow in LateUpdate keeps authored world dimensions.
            var canvasGo = new GameObject("BossHealthCanvas");
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvasGo.transform;
            rt.sizeDelta = new Vector2(2.4f, 0.34f);
            canvasGo.AddComponent<Billboard>(); // always face the camera, like the reference

            var backGo = new GameObject("Back");
            backGo.transform.SetParent(canvasGo.transform, false);
            var back = backGo.AddComponent<UnityEngine.UI.Image>();
            back.color = new Color(0.85f, 0.18f, 0.12f, 0.95f); // red = damage taken
            var backRt = back.rectTransform;
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(backGo.transform, false);
            var fill = fillGo.AddComponent<UnityEngine.UI.Image>();
            fill.color = new Color(0.24f, 0.83f, 0.39f, 1f); // green = health remaining
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = new Vector2(0.03f, 0.03f);
            _fill.offsetMax = new Vector2(-0.03f, -0.03f);
            _fill.pivot = new Vector2(0f, 0.5f);
        }

        private void LateUpdate()
        {
            if (_canvas != null)
                _canvas.transform.position = transform.position + Vector3.up * 7.5f; // verified visible in the 19:26 battle capture; 5.6 hid behind the boss's head in close framings
            float fraction = _battle != null && _battle.BattleRunning ? _battle.EnemyFraction : 1f;
            _fill.localScale = new Vector3(fraction, 1f, 1f);
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
