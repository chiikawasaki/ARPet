using UnityEngine;
using Unity.AI.Navigation;
using Meta.XR.MRUtilityKit;

public class ARNavmeshBaker : MonoBehaviour
{
    public NavMeshSurface surface;
    public OVRSceneManager sceneManager;

    void OnEnable()
    {
        sceneManager.SceneModelLoadedSuccessfully += BuildNav;
    }

    void OnDisable()
    {
        sceneManager.SceneModelLoadedSuccessfully -= BuildNav;
    }

    void BuildNav()
    {
        Debug.Log("OVRSceneManager: Scene loaded, building NavMesh...");

        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("MRUK: No room found!");
            return;
        }

        foreach (var anchor in room.Anchors)
        {
            // Labelは MRUKAnchor.SceneLabels 型のenum
            if (anchor.Label == MRUKAnchor.SceneLabels.FLOOR)
            {
                if (!anchor.gameObject.GetComponent<MeshCollider>())
                {
                    anchor.gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        surface.BuildNavMesh();
        Debug.Log("NavMesh built from MRUK floor anchors!");
    }
}
