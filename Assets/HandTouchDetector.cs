using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class HandTouchDetector : MonoBehaviour
{
    private bool _isTouching = false;
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        
        // BoxColliderをトリガーに設定
        var collider = GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
        
        Debug.Log("🐕 HandTouchDetector初期化完了（犬側）");
    }

    void OnTriggerEnter(Collider other)
    {
        // 手かどうかをチェック（LeftInteractionsの子オブジェクトかどうか）
        if (IsHandObject(other.gameObject) && !_isTouching)
        {
            _isTouching = true;
            Debug.Log("🖐️ 手がコーギーに触れました！（コライダー判定）");
            OnTouchStart();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsHandObject(other.gameObject) && _isTouching)
        {
            _isTouching = false;
            Debug.Log("🖐️ 手がコーギーから離れました（コライダー判定）");
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
        Debug.Log("撫で動作開始");
        
        // Patアニメーション再生
        if (_animator != null)
        {
            _animator.Play("pat");
            Debug.Log("🐕 Patアニメーション再生！");
        }
        else
        {
            Debug.LogWarning("⚠️ Animatorが見つかりません");
        }
    }

    private void OnTouchEnd()
    {
        Debug.Log("撫で動作終了");
        
        // Breathingアニメーション再生
        if (_animator != null)
        {
            _animator.Play("Breathing");
            Debug.Log("🐕 Breathingアニメーション再生！");
        }
        else
        {
            Debug.LogWarning("⚠️ Animatorが見つかりません");
        }
    }
}