using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NavMeshVisualizer : MonoBehaviour
{
    [Tooltip("NavMeshを塗る色")]
    public Color meshColor = new Color(0f, 0.5f, 1f, 0.25f); // 半透明の青

    Mesh _mesh;
    Material _mat;

    void Awake()
    {
        // メッシュ準備
        _mesh = new Mesh();
        _mesh.name = "NavMeshVisualizerMesh";

        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = _mesh;

        // マテリアル準備
        var mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null)
        {
            // ビルトインなら "Standard"、URPなら "Universal Render Pipeline/Lit" とか
            var shader = Shader.Find("Standard");
            _mat = new Material(shader);
            _mat.color = meshColor;
            mr.material = _mat;
        }
        else
        {
            _mat = mr.material;
            _mat.color = meshColor;
        }

        // 半透明になるようにレンダリングモードを調整（Standard用の簡易対応）
        if (_mat.shader.name == "Standard")
        {
            _mat.SetFloat("_Mode", 3); // Transparent
            _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _mat.SetInt("_ZWrite", 0);
            _mat.DisableKeyword("_ALPHATEST_ON");
            _mat.EnableKeyword("_ALPHABLEND_ON");
            _mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _mat.renderQueue = 3000;
        }
    }

    void LateUpdate()
    {
        // 毎フレームNavMeshの三角形を取り直す（デバッグ用）
        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length == 0) return;

        _mesh.Clear();
        _mesh.vertices = tri.vertices;
        _mesh.triangles = tri.indices;
        _mesh.RecalculateNormals();
    }
}
