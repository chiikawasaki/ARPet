using UnityEngine;

public class HandCollisionDetector : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private GameObject dogObject; // 手動でアタッチした犬
    
    private bool _isTouching = false;

    void Start()
    {
        // 手にコライダーとRigidbodyを追加（物理判定用）
        if (GetComponent<Collider>() == null)
        {
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.05f; // 手のサイズ
            collider.isTrigger = true; // トリガーとして使用
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // 物理演算による移動を無効
        }
        
        Debug.Log("HandCollisionDetector初期化完了");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == dogObject && !_isTouching)
        {
            _isTouching = true;
            Debug.Log("🖐️ 手がコーギーに触れました！（コライダー判定）");
            OnTouchStart();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject == dogObject && _isTouching)
        {
            _isTouching = false;
            Debug.Log("🖐️ 手がコーギーから離れました（コライダー判定）");
            OnTouchEnd();
        }
    }

    private void OnTouchStart()
    {
        Debug.Log("撫で動作開始（コライダー）");
        // ここに撫でた時の処理を追加可能
    }

    private void OnTouchEnd()
    {
        Debug.Log("撫で動作終了（コライダー）");
        // ここに撫でるのを止めた時の処理を追加可能
    }
}