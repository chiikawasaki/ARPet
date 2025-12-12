using UnityEngine;
using Meta.XR.MRUtilityKit;       // ← これを追加

/// <summary>
/// AnchorPrefabSpawner が生成したプレハブのうち
/// HMD（メインカメラ）に最も近い 1 つだけ残し、他は即削除する。
/// [BuildingBlock] AnchorPrefabSpawner と同じ GameObject に付けてください。
/// </summary>
[RequireComponent(typeof(AnchorPrefabSpawner))]
public class KeepNearestSpawn : MonoBehaviour
{
    AnchorPrefabSpawner spawner;

    void Awake()
    {
        spawner = GetComponent<AnchorPrefabSpawner>();

        // MRUK v70+ では PrefabInstantiated が廃止。
        // 代わりに onPrefabSpawned（Obsolete と表示される）を使う。
#pragma warning disable CS0618   // Obsolete 警告を抑制
        spawner.onPrefabSpawned.AddListener(FilterNearest);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Prefab が一通り生成された直後に呼ばれる
    /// </summary>
    void FilterNearest()
    {
        var camPos = Camera.main.transform.position;
        GameObject nearestObj = null;
        float nearestSqr = float.MaxValue;

        // AnchorPrefabSpawnerObjects には「Anchor → 生成されたPrefab」が全て入っている
        foreach (var kv in spawner.AnchorPrefabSpawnerObjects)
        {
            var go   = kv.Value;
            float d2 = (go.transform.position - camPos).sqrMagnitude;
            if (d2 < nearestSqr)
            {
                nearestSqr = d2;
                nearestObj = go;
            }
        }

        // いちばん近いもの「以外」を破棄
        foreach (var kv in spawner.AnchorPrefabSpawnerObjects)
        {
            if (kv.Value != nearestObj)
            {
                Destroy(kv.Value);
            }
        }
    }
}
