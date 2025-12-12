using System.Collections;
using UnityEngine;
using UnityEngine.AI;        // ← NavMeshAgent 用（DogBrain側で使うときなど）
using Unity.AI.Navigation;   // ★ NavMeshSurface / NavMeshModifier 用
using Meta.XR.MRUtilityKit;

[DefaultExecutionOrder(1000)]
public class ARNavmeshBaker_MRUK : MonoBehaviour
{
    [Header("NavMesh")]
    public NavMeshSurface surface;

    [Tooltip("Room生成後、NavMeshを焼くまでの待ち時間（EffectMesh生成待ち用）")]
    public float buildDelaySeconds = 1.5f;

    [Tooltip("Roomが作り直されたときに再Bakeするか")]
    public bool rebuildOnRoomCreated = true;

    EffectMesh _effectMesh;

    void Awake()
    {
        if (!surface)
        {
            surface = GetComponent<NavMeshSurface>();
        }
    }

    void OnEnable()
    {
        if (MRUK.Instance != null)
        {
            // Sceneロード完了・Room作成のイベントにフック
            MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);
            MRUK.Instance.RoomCreatedEvent.AddListener(OnRoomCreated);
        }
        else
        {
            Debug.LogWarning("[ARNavmeshBaker_MRUK] MRUK.Instance が null です。MRUK prefab がシーンにあるか確認してください。");
        }
    }

    void OnDisable()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
            MRUK.Instance.RoomCreatedEvent.RemoveListener(OnRoomCreated);
        }
    }

    // MRUK がシーンモデルをロードし終わったタイミング
    void OnSceneLoaded()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room != null)
        {
            StartCoroutine(BuildNavMeshWhenReady(room));
        }
    }

    // 新しいRoomが作成されたとき（リスキャンなど）
    void OnRoomCreated(MRUKRoom room)
    {
        if (!rebuildOnRoomCreated) return;
        StartCoroutine(BuildNavMeshWhenReady(room));
    }

    IEnumerator BuildNavMeshWhenReady(MRUKRoom room)
    {
        if (surface == null)
        {
            Debug.LogWarning("[ARNavmeshBaker_MRUK] NavMeshSurface が設定されていません。");
            yield break;
        }

        // EffectMesh を探す（MRUKの部屋メッシュ表示コンポーネント）
        if (_effectMesh == null)
        {
            _effectMesh = FindAnyObjectByType<EffectMesh>();
        }

        // EffectMesh が見つかるまで待つ
        while (_effectMesh == null)
        {
            _effectMesh = FindAnyObjectByType<EffectMesh>();
            yield return null;
        }

        // EffectMesh のメッシュがAnchorsとひも付くまで少し待つ（公式サンプルもこのパターン）
        // 必要以上に凝らず、少し待ってから焼く簡易版
        yield return new WaitForSeconds(buildDelaySeconds);

        Debug.Log("[ARNavmeshBaker_MRUK] Building NavMesh from EffectMesh / room: " + room.name);
        surface.BuildNavMesh();
    }
}
