// ======================= ActionDef.cs =======================
using UnityEngine;

[System.Serializable]
public class ActionDef
{
    public DogAction action = DogAction.Breathe;

    [Header("Selection Weights & Timing")]
    [Min(0f)] public float weight = 1f;  // 出やすさ
    [Min(0f)] public float minDur = 1.0f; // 滞在(秒) / RoutineならCycle時間
    [Min(0f)] public float maxDur = 3.0f;
    [Min(0f)] public float cooldown = 1.0f; // 同行動の再出現までの最短間隔

    [HideInInspector] public float nextOkTime = 0f; // 実行側で使用
}