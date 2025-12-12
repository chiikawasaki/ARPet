using UnityEngine;

public class MeshColliderAssigner : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
    }

    void Start()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null && meshCollider != null)
        {
            meshCollider.sharedMesh = meshFilter.sharedMesh;
        }
        else
        {
            Debug.LogWarning("MeshColliderAssigner: MeshFilter または Mesh が null です");
        }
    }
}
