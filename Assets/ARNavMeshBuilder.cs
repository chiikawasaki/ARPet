using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;

public class ARNavMeshBuilder : MonoBehaviour
{
    public NavMeshSurface surface;
    public float delayBeforeBuild = 2.0f; // EffectMeshが出揃うまでの待ち時間（あとで調整）

    IEnumerator Start()
    {
        // EffectMesh / MRUK が環境メッシュを生成し終わるのを待つ
        yield return new WaitForSeconds(delayBeforeBuild);

        if (surface == null)
            surface = GetComponent<NavMeshSurface>();

        if (surface != null)
        {
            surface.BuildNavMesh();
            Debug.Log("[ARNavMeshBuilder] NavMesh built.");
        }
    }
}
