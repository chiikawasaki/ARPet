using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class ControllerTouchDetector: MonoBehaviour
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
    // 撫で開始時刻
    private float _pettingStartTime;
    
    private Transform _rightController;
    private Transform _leftController;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _rightController = GameObject.Find("[BuildingBlock] Controller Tracking Right")?.transform;
        _leftController = GameObject.Find("[BuildingBlock] Controller Tracking Left")?.transform;
        // BoxColliderをトリガーに設定
        var collider = GetComponent<BoxCollider>();
        Debug.Log("🐕 ControllerTouchDetector初期化完了（犬側）");
    }

    
    //トリガーコライダーに何かが侵入した時に発火するUnityの組み込み関数
    void OnTriggerEnter(Collider other)
    {
        // コントローラーかどうかをチェック（LeftInteractionsの子オブジェクトかどうか）
        if (IsControllerObject(other.gameObject) && !_isTouching)
        {
            _isTouching = true;
            Debug.Log("🖐️ コントローラーがコーギーに触れました！（コライダー判定）");
            OnTouchStart();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsControllerObject(other.gameObject) && _isTouching)
        {
            _isTouching = false;
            Debug.Log("🖐️ コントローラーがコーギーから離れました（コライダー判定）");
            OnTouchEnd();
        }
    }

   private bool IsControllerObject(GameObject obj)
   {
       // コントローラーが触れたかチェック
       Transform t = obj.transform;
       // ぶつかったother.gameObjectがコントローラーの一部かどうかをチェックする
       if (_rightController != null && t.IsChildOf(_rightController))
       {
          return true;
       }
       if (_leftController != null && t.IsChildOf(_leftController))
       {
          return true;
       }
       return false;
   }

    private void OnTouchStart()
    {
        Debug.Log("撫で動作開始 - 0.5秒待機中...");
        
        // 既存の終了遅延があれば停止
        // _endDelayCoroutine != nullの時、終了コルーチンが始まっている
        if (_endDelayCoroutine != null)
        {
            StopCoroutine(_endDelayCoroutine);
            _endDelayCoroutine = null;
        }
        
        // 0.5秒遅延でアニメーション開始
        // _startDelayCoroutine != nullの時、開始コルーチンが始まっている
        if (_startDelayCoroutine != null)
            StopCoroutine(_startDelayCoroutine);
            _startDelayCoroutine = null;
        
        //0.5秒後に撫で開始を予約
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
            
            // _patTimerCoroutineがまだ実行されていなければ、実行する
            if (_patTimerCoroutine != null)
                StopCoroutine(_patTimerCoroutine);
            // 3秒タイマー開始
            _patTimerCoroutine = StartCoroutine(PatTimer());
            _isPatting = true;
            _pettingStartTime = Time.time;
        }
        
        _startDelayCoroutine = null;
    }
    
    private System.Collections.IEnumerator EndPetAnimation()
    {   
        // 1秒待機
        yield return new WaitForSeconds(1f);
        
        // 1秒後、まだ離れたままなら終了処理
        if (!_isTouching)
        {
            Debug.Log("🐕 撫で終了処理開始");
            // 撫でていた時間を計算
            float pettingDuration = 0f;
            // 撫で状態が始まっていた場合、撫でていた時間を記録
            if (_isPatting)
            {
                pettingDuration = Time.time - _pettingStartTime;
            }
            
            Debug.Log($"🐕 撫でていた時間: {pettingDuration:F2}秒");
            // 撫で状態を終了
            _isPatting = false;
            
            // 撫でタイマーを停止
            // _patTimerCoroutineが実行されていたら、止める(3秒記録するやつ)
            if (_patTimerCoroutine != null)
            {
                StopCoroutine(_patTimerCoroutine);
                _patTimerCoroutine = null;
            }
            
            // Breathingアニメーション再生
            if (_animator != null)
            {   //寝転がっていた時、EndPatLieTriggerを発火することで自然に起き上がる
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
        
        // 3秒後、まだ撫でていたらトリガー発火
        if (_isPatting && _animator != null)
        {
            // 寝転がる
            _animator.SetTrigger("PatLieTrigger");
            //ここに右撫でてる時と左撫でてる時の処理を書く
            Debug.Log("🐕 3秒経過！PatLieトリガー発火！");
        }
    }
}
    
