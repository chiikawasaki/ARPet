using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class HandTouchDetector : MonoBehaviour
{
    // 触ってるかどうかのフラグ
    private bool _isTouching = false;
    // アニメーター
    private Animator _animator;
    // タイマー
    private Coroutine _patTimerCoroutine;
    // 開始遅延 (0.5秒)
    private Coroutine _startDelayCoroutine;
    // 終了遅延 (1秒)
    private Coroutine _endDelayCoroutine;
    // 撫でてるかどうかのフラグ
    private bool _isPatting = false;

    void Start()
    {
        _animator = GetComponent<Animator>();
        // BoxColliderをトリガーに設定
        var collider = GetComponent<BoxCollider>();
        Debug.Log("🐕 HandTouchDetector初期化完了（犬側）");
    }

    
    //トリガーコライダーに何かが侵入した時に発火するUnityの組み込み関数
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
       // 左手の子オブジェクトかチェック
       var left = GameObject.Find("LeftInteractions");
       if (left != null && obj.transform.IsChildOf(left.transform))
       {
           return true;
       }

       // 右手の子オブジェクトかチェック
       var right = GameObject.Find("RightInteractions");
       if (right != null && obj.transform.IsChildOf(right.transform))
       {
          return true;
       }
    return false;
   }

    private void OnTouchStart()
    {
        Debug.Log("撫で動作開始 - 0.5秒待機中...");
        
        // 既存の終了遅延があれば停止
        if (_endDelayCoroutine != null)
        {
            StopCoroutine(_endDelayCoroutine);
            _endDelayCoroutine = null;
        }
        
        // 0.5秒遅延でアニメーション開始
        if (_startDelayCoroutine != null)
            StopCoroutine(_startDelayCoroutine);
            
        _startDelayCoroutine = StartCoroutine(StartPetAnimation());
    }

    private void OnTouchEnd()
    {
        Debug.Log("撫で動作終了 - 1秒待機中...");
        
        // 開始遅延があれば停止
        if (_startDelayCoroutine != null)
        {
            StopCoroutine(_startDelayCoroutine);
            _startDelayCoroutine = null;
        }
        
        // 1秒遅延で終了処理
        if (_endDelayCoroutine != null)
            StopCoroutine(_endDelayCoroutine);
            
        _endDelayCoroutine = StartCoroutine(EndPetAnimation());
    }
    
    private System.Collections.IEnumerator StartPetAnimation()
    {
        yield return new WaitForSeconds(0.5f);
        
        // 0.5秒後、まだ触れていたらアニメーション開始
        if (_isTouching && _animator != null)
        {
            Debug.Log("🐕 0.5秒経過！撫でアニメーション開始");
            
            // 親密度を上げる
            if (GameManager.Instance != null)
            {
                GameManager.Instance.intimacy += 0.01f;
                GameManager.Instance.intimacy = Mathf.Clamp01(GameManager.Instance.intimacy);
                Debug.Log($"🐕 撫でてもらって親密度UP！ 現在の親密度: {GameManager.Instance.intimacy:F2}");
            }
            
            _animator.Play("PatChest"); // 胸を撫でる
            Debug.Log("🐕 PatChestアニメーション再生！");
            
            // 10秒タイマー開始
            if (_patTimerCoroutine != null)
                StopCoroutine(_patTimerCoroutine);
            
            _patTimerCoroutine = StartCoroutine(PatTimer());
            _isPatting = true;
        }
        
        _startDelayCoroutine = null;
    }
    
    private System.Collections.IEnumerator EndPetAnimation()
    {
        yield return new WaitForSeconds(1f);
        
        // 1秒後、まだ離れたままなら終了処理
        if (!_isTouching)
        {
            Debug.Log("🐕 1秒経過！撫で終了処理開始");
            _isPatting = false;
            
            // タイマー停止
            if (_patTimerCoroutine != null)
            {
                StopCoroutine(_patTimerCoroutine);
                _patTimerCoroutine = null;
            }
            
            // Breathingアニメーション再生
            if (_animator != null)
            {
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("PatLie"))
                {
                    _animator.SetTrigger("EndPatLieTrigger");
                }
                else
                {
                    _animator.Play("Breathing");
                }
                Debug.Log("🐕 Breathingアニメーション再生！");
            }
            else
            {
                Debug.LogWarning("⚠️ Animatorが見つかりません");
            }
        }
        
        _endDelayCoroutine = null;
    }
    
    private System.Collections.IEnumerator PatTimer()
    {
        yield return new WaitForSeconds(3f);
        
        // 10秒後、まだ撫でていたらトリガー発火
        if (_isPatting && _animator != null)
        {
            _animator.SetTrigger("PatLieTrigger");
            //ここに右撫でてる時と左撫でてる時の処理を書く
            Debug.Log("🐕 10秒経過！PatLieトリガー発火！");
        }
    }
}