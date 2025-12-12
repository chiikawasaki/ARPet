using UnityEngine;
using Meta.XR.MRUtilityKit;

public class SceneMeshTo2D : MonoBehaviour
{
    [Range(256, 4096)]
    public int textureSize = 1024;

    public Texture2D Generate()
    {
        var mruk = MRUK.Instance;
        if (mruk == null)
        {
            Debug.LogError("MRUK.Instance is NULL");
            return null;
        }

        var room = mruk.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogError("No current room found.");
            return null;
        }

        var meshAnchor = room.GlobalMeshAnchor;
        if (meshAnchor == null)
        {
            Debug.LogError("GlobalMeshAnchor not found.");
            return null;
        }

        var mf = meshAnchor.GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("GlobalMeshAnchor does not have MeshFilter.");
            return null;
        }

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("Scene mesh not found.");
            return null;
        }

        // === 2D Texture 初期化 ===
        Texture2D tex = new Texture2D(textureSize, textureSize);
        Color32[] fill = new Color32[textureSize * textureSize];
        for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(0, 0, 0, 255);
        tex.SetPixels32(fill);

        // === メッシュの頂点取得 & 投影 ===
        Vector3[] verts = mesh.vertices;

        // 座標範囲調査
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var v in verts)
        {
            var w = meshAnchor.transform.TransformPoint(v); // ワールド座標
            minX = Mathf.Min(minX, w.x);
            maxX = Mathf.Max(maxX, w.x);
            minZ = Mathf.Min(minZ, w.z);
            maxZ = Mathf.Max(maxZ, w.z);
        }

        float scaleX = textureSize / (maxX - minX);
        float scaleZ = textureSize / (maxZ - minZ);

        // === 頂点をドットで描画 ===
        foreach (var v in verts)
        {
            var w = meshAnchor.transform.TransformPoint(v);

            int px = Mathf.RoundToInt((w.x - minX) * scaleX);
            int pz = Mathf.RoundToInt((w.z - minZ) * scaleZ);

            if (px >= 0 && px < textureSize && pz >= 0 && pz < textureSize)
            {
                tex.SetPixel(px, pz, Color.white);
            }
        }

        tex.Apply();
        return tex;
    }
}
