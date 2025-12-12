using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;

public class EffectMeshMiniMap : MonoBehaviour
{
    public RawImage miniMapImage;
    public int textureSize = 512;

    private Texture2D tex;

    void Start()
    {
        tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        miniMapImage.texture = tex;

        Invoke(nameof(GenerateMiniMap), 2.0f); // MRUK + EffectMesh 読み込み待ち
    }

    void GenerateMiniMap()
    {
        // 画面をクリア
        Color[] blank = new Color[textureSize * textureSize];
        tex.SetPixels(blank);

        // ★ EffectMesh が生成した “実際の3Dメッシュ” を拾う
        MeshFilter[] allMeshes = FindObjectsOfType<MeshFilter>();

        foreach (var mf in allMeshes)
        {
            // EffectMesh が生成したものだけに限定（名前が一致）
            if (mf.name.Contains("EffectMeshObject") || 
                (mf.GetComponentInParent<EffectMesh>() != null))
            {
                DrawMesh(mf);
            }
        }

        tex.Apply();
    }

    void DrawMesh(MeshFilter mf)
    {
        Mesh mesh = mf.sharedMesh;
        if (!mesh) return;

        Vector3[] verts = mesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 world = mf.transform.TransformPoint(verts[i]);

            // とりあえず ±5m の範囲をマップに焼く仮スケール
            int x = (int)((world.x + 5f) / 10f * textureSize);
            int y = (int)((world.z + 5f) / 10f * textureSize);

            if (x >= 0 && x < textureSize && y >= 0 && y < textureSize)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }
    }
}
