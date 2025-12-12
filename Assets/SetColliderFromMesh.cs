using UnityEngine;

[RequireComponent(typeof(MeshCollider), typeof(MeshFilter))]
public class SetColliderFromMesh : MonoBehaviour
{
    void Start()
    {
        var filter = GetComponent<MeshFilter>();
        var collider = GetComponent<MeshCollider>();
        if (filter != null && filter.sharedMesh != null)
        {
            collider.sharedMesh = filter.sharedMesh;
            Debug.Log("✅ MeshColliderにMeshをセットしました: " + filter.sharedMesh.name);
        }
        else
        {
            Debug.Log("❌ MeshFilterまたはMeshがnullです");
        }
    }
}
