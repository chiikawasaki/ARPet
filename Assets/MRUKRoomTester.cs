using System.Collections;
using System.Linq;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRUKRoomTester : MonoBehaviour
{
    public float waitSeconds = 3f;

    void Start()
    {
        StartCoroutine(TestRoutine());
    }

    IEnumerator TestRoutine()
    {
        yield return new WaitForSeconds(waitSeconds);

        if (MRUK.Instance == null)
        {
            Debug.LogError("[MRUKRoomTester] MRUK.Instance が null です。");
            yield break;
        }

        Debug.Log("[MRUKRoomTester] MRUK.Instance は存在します ✅");

        var rooms = FindObjectsOfType<MRUKRoom>();
        Debug.Log($"[MRUKRoomTester] MRUKRoom の数: {rooms.Length}");

        if (rooms.Length == 0)
        {
            Debug.LogWarning("[MRUKRoomTester] MRUKRoom が 0 個です。");
            yield break;
        }

        foreach (var room in rooms)
        {
            var meshFilters = room.GetComponentsInChildren<MeshFilter>(includeInactive: true);

            Debug.Log(
                $"[MRUKRoomTester] Room: {room.name} / MeshFilter数: {meshFilters.Length}"
            );

            // ★ ここで全部中身を dump
            foreach (var mf in meshFilters)
            {
                var mesh = mf.sharedMesh;
                int vertCount = mesh != null ? mesh.vertexCount : 0;
                var bounds = mesh != null ? mesh.bounds : new Bounds();

                Debug.Log(
                    $"  - MF: {mf.name} / verts: {vertCount} / " +
                    $"bounds: {bounds.size}"
                );
            }

            // おまけ：一番デカい Mesh を探す（後でミニマップ用に使える）
            var largest = meshFilters
                .Where(mf => mf.sharedMesh != null)
                .OrderByDescending(mf => mf.sharedMesh.bounds.size.magnitude)
                .FirstOrDefault();

            if (largest != null)
            {
                Debug.Log(
                    $"[MRUKRoomTester] largest MeshFilter: {largest.name} / " +
                    $"bounds: {largest.sharedMesh.bounds.size}"
                );
            }
        }
    }
}
