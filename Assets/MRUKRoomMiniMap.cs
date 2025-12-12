using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Meta.XR.MRUtilityKit;

public class MRUKRoomMiniMap : MonoBehaviour
{
    [Header("UI")]
    public RawImage miniMapImage;   // ここに RawImage をアサイン
    public int textureSize = 512;

    [Header("MRUK")]
    [Tooltip("Room / EffectMesh が生成されるまで待つ時間（秒）")]
    public float waitSeconds = 3f;

    Texture2D tex;

    class MeshRect
    {
        public MeshFilter mf;
        public float minX, maxX, minZ, maxZ;
    }

    void Start()
    {
        tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        miniMapImage.texture = tex;

        Debug.Log("[MRUKRoomMiniMap] Start");
        Invoke(nameof(GenerateMiniMap), waitSeconds);
    }

    void GenerateMiniMap()
    {
        Debug.Log("[MRUKRoomMiniMap] GenerateMiniMap called");

        var room = FindObjectOfType<MRUKRoom>();
        if (room == null)
        {
            Debug.LogError("[MRUKRoomMiniMap] MRUKRoom が見つかりません");
            return;
        }

        var meshFilters = room.GetComponentsInChildren<MeshFilter>(true)
            .Where(mf => mf.sharedMesh != null)
            .ToArray();

        if (meshFilters.Length == 0)
        {
            Debug.LogError("[MRUKRoomMiniMap] MeshFilter がありません");
            return;
        }

        // 1. 各メッシュのXZ範囲と、部屋全体のXZ範囲を計算
        List<MeshRect> rects = new List<MeshRect>();

        float worldMinX = float.PositiveInfinity, worldMaxX = float.NegativeInfinity;
        float worldMinZ = float.PositiveInfinity, worldMaxZ = float.NegativeInfinity;

        foreach (var mf in meshFilters)
        {
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var t = mf.transform;

            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;

            for (int i = 0; i < verts.Length; i++)
            {
                var w = t.TransformPoint(verts[i]);
                if (w.x < minX) minX = w.x;
                if (w.x > maxX) maxX = w.x;
                if (w.z < minZ) minZ = w.z;
                if (w.z > maxZ) maxZ = w.z;
            }

            if (!float.IsFinite(minX) || !float.IsFinite(minZ)) continue;

            MeshRect r = new MeshRect
            {
                mf = mf,
                minX = minX,
                maxX = maxX,
                minZ = minZ,
                maxZ = maxZ
            };
            rects.Add(r);

            if (minX < worldMinX) worldMinX = minX;
            if (maxX > worldMaxX) worldMaxX = maxX;
            if (minZ < worldMinZ) worldMinZ = minZ;
            if (maxZ > worldMaxZ) worldMaxZ = maxZ;
        }

        if (rects.Count == 0)
        {
            Debug.LogError("[MRUKRoomMiniMap] rects が 0");
            return;
        }

        float worldSizeX = Mathf.Max(worldMaxX - worldMinX, 0.001f);
        float worldSizeZ = Mathf.Max(worldMaxZ - worldMinZ, 0.001f);

        Debug.Log($"[MRUKRoomMiniMap] world bounds X:{worldMinX}〜{worldMaxX}, Z:{worldMinZ}〜{worldMaxZ}");

        // 2. 背景クリア（黒）
        var clear = Enumerable.Repeat(Color.black, textureSize * textureSize).ToArray();
        tex.SetPixels(clear);

        // ヘルパー：一つのMeshRectを塗る関数
        void DrawRect(MeshRect r, Color col)
        {
            int xMin = Mathf.RoundToInt((r.minX - worldMinX) / worldSizeX * (textureSize - 1));
            int xMax = Mathf.RoundToInt((r.maxX - worldMinX) / worldSizeX * (textureSize - 1));
            int yMin = Mathf.RoundToInt((r.minZ - worldMinZ) / worldSizeZ * (textureSize - 1));
            int yMax = Mathf.RoundToInt((r.maxZ - worldMinZ) / worldSizeZ * (textureSize - 1));

            xMin = Mathf.Clamp(xMin, 0, textureSize - 1);
            xMax = Mathf.Clamp(xMax, 0, textureSize - 1);
            yMin = Mathf.Clamp(yMin, 0, textureSize - 1);
            yMax = Mathf.Clamp(yMax, 0, textureSize - 1);

            if (xMax < xMin) xMax = xMin;
            if (yMax < yMin) yMax = yMin;

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    tex.SetPixel(x, y, col);
                }
            }
        }

        // 3. 描画順：
        //    ① FLOOR（床）→ ② 家具 → ③ 壁 → ④ その他

        // ① 床（濃い緑）: 背景として先に
        foreach (var r in rects.Where(r => r.mf.name.ToUpper().Contains("FLOOR")))
        {
            DrawRect(r, new Color(0.0f, 0.3f, 0.0f));      // 濃い緑
        }

        // ② 机・ソファ・棚（明るい緑）
        foreach (var r in rects.Where(r =>
                     {
                         var n = r.mf.name.ToUpper();
                         return n.Contains("TABLE") || n.Contains("DESK") ||
                                n.Contains("COUCH") || n.Contains("SOFA") ||
                                n.Contains("STORAGE");
                     }))
        {
            DrawRect(r, new Color(0.3f, 0.9f, 0.3f));      // 明るい緑
        }

        // ③ 壁・窓枠（青〜シアン）
        foreach (var r in rects.Where(r =>
                     {
                         var n = r.mf.name.ToUpper();
                         return n.Contains("WALL") || n.Contains("WINDOW");
                     }))
        {
            DrawRect(r, new Color(0.2f, 0.5f, 1.0f));      // 青系
        }

        tex.Apply();
        Debug.Log("[MRUKRoomMiniMap] Draw done");
    }
}
