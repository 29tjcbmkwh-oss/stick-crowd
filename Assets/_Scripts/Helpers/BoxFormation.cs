using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxFormation : FormationBase {
    [SerializeField] private int amount = 1;
    [SerializeField] private bool _hollow = false;
    [SerializeField] private float _nthOffset = 0;

    public override int Amount { get => amount; set => amount = value; }

    // Grid is sized to just contain `amount` units (as close to square as possible), then
    // capped to exactly `amount` points so callers that read Amount and EvaluatePoints().Count
    // always agree — ArmyController.SetFormation() spawns/kills strictly off point count.
    public override IEnumerable<Vector3> EvaluatePoints() {
        int safeAmount = Mathf.Max(0, amount);
        int unitWidth = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(safeAmount)));
        int unitDepth = Mathf.Max(1, Mathf.CeilToInt(safeAmount / (float)unitWidth));
        var middleOffset = new Vector3(unitWidth * 0.5f, 0, unitDepth * 0.5f);

        int yielded = 0;
        for (var z = 0; z < unitDepth; z++) {
            for (var x = 0; x < unitWidth; x++) {
                if (yielded >= safeAmount) yield break;
                if (_hollow && x != 0 && x != unitWidth - 1 && z != 0 && z != unitDepth - 1) continue;
                var pos = new Vector3(x + (z % 2 == 0 ? 0 : _nthOffset), 0, z);

                pos -= middleOffset;

                pos += GetNoise(pos);

                pos *= Spread;

                yielded++;
                yield return pos;
            }
        }
    }
}