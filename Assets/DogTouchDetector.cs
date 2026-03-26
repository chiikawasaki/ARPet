using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class DogTouchDetector : MonoBehaviour
{
    private bool _isTouching = false;
    private Animator _animator;
    private IHand _hand;

    void Start()
    {
        _animator = GetComponent<Animator>();
        
        // BoxColliderをトリガーに設定
        var collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
        
        // 左手を探す
        var leftInteractions = GameObject.Find("LeftInteractions");
        if (leftInteractions != null)
        {
            var handRef = leftInteractions.GetComponentInChildren<HandRef>();
            if (handRef != null)
            {
                _hand = handRef.Hand;
            }
        }
        
        Debug.Log("🐕 DogTouchDetector初期化完了");
    }

    void OnTriggerEnter(Collider other)
    {
        // 手かどうかをチェック（LeftInteractionsの子オブジェクトかどうか）
        if (IsHandObject(other.gameObject) && !_isTouching)
        {
            _isTouching = true;
            Debug.Log("🖐️ 手がコーギーに触れました！");
            OnTouchStart();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsHandObject(other.gameObject) && _isTouching)
        {
            _isTouching = false;
            Debug.Log("🖐️ 手がコーギーから離れました");
            OnTouchEnd();
        }
    }

    private bool IsHandObject(GameObject obj)
    {
        // LeftInteractionsの子オブジェクトかチェック
        var leftInteractions = GameObject.Find("LeftInteractions");
        if (leftInteractions != null)
        {
            return obj.transform.IsChildOf(leftInteractions.transform);
        }
        return false;
    }

    private void OnTouchStart()
    {
        Debug.Log("撫で動作開始 - アニメーション再生可能");
        
        // ここで撫でるアニメーション再生
        if (_animator != null)
        {
            // 例: _animator.SetTrigger("PetTrigger");
        }
    }

    private void OnTouchEnd()
    {
        Debug.Log("撫で動作終了");
    }
}