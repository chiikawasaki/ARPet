using UnityEngine;
using Oculus.Haptics;

public class HeadPetting : MonoBehaviour
{
    // 触ってるかどうかのフラグ
    private bool _isTouching = false;
    // 撫でてるかどうかのフラグ
    private bool _isPatting = false;
    // アニメーター
    private Animator _animator;
    // 開始遅延 (0.5秒)を管理するもの
    private Coroutine _startDelayCoroutine;
    // 終了遅延 (1秒)を管理するもの
    private Coroutine _endDelayCoroutine;
    // 撫で開始時刻
    private float _pettingStartTime;
    // HapticClipPlayerのインスタンス 撫でるのに使う
    private HapticClipPlayer player;
    public HapticClip clip; // Unity Editorで設定するためにpublicにする

    //　犬の頭のコライダー
    [SerializeField] private Collider headCollider;
    // 撫で開始判定を行うまでの時間
    [SerializeField] private float PetStartDelay = 0.5f;
    // 撫で終了判定を行うまでの時間
    [SerializeField] private float PetEndDelay = 1f;

    private Transform left;
    private Transform right;

    // 初期化
    void Awake(){
        player = new HapticClipPlayer(clip);
    }

    void Start()
    {
        if (headCollider != null)
        {
            headCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError("Head Colliderが設定されていません");
        }

        _animator = GetComponent<Animator>();

        Debug.Log("🐕 HeadPetting初期化完了（犬側）");

        // 犬が生成されたときに一度だけ探す。手のオブジェクトを取得
        GameObject leftObject = GameObject.Find("LeftInteractions");
        GameObject rightObject = GameObject.Find("RightInteractions");

        if (leftObject != null)
        {
            left = leftObject.transform;
        }
        else
        {
            Debug.LogWarning("LeftInteractionsが見つかりません");
        }
        if (rightObject != null)
        {
            right = rightObject.transform;
        }
        else
        {
            Debug.LogWarning("RightInteractionsが見つかりません");
        }
    }

    
    //トリガーコライダーに何かが侵入した時に発火するUnityの組み込み関数
    void OnTriggerEnter(Collider other)
    {
        // 手かどうかをチェック（LeftInteractionsの子オブジェクトかどうか）
        if (IsHandObject(other.gameObject) && !_isTouching)
        {
            _isTouching = true;
            Debug.Log("🖐️ 手がコーギーの頭に触れました（コライダー判定）");
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

   // objが手（コントローラ）の子オブジェクトかどうかをチェックする関数
   private bool IsHandObject(GameObject obj)
   {
       // 左の子オブジェクトかチェック
       if (left != null && obj.transform.IsChildOf(left.transform))
       {
           return true;
       }

       // 右の子オブジェクトかチェック
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
        // 撫でている途中に前の記録で離れたとされないようにする
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
    
    // 撫で開始の処理
    private System.Collections.IEnumerator StartPetAnimation()
    {
        yield return new WaitForSeconds(PetStartDelay);
        
        // 0.5秒後、まだ触れていたらアニメーション開始
        if (_isTouching)
        {   
            // 親密度を上げる
            if (GameManager.Instance != null)
            {
                GameManager.Instance.intimacy += 0.01f;
                GameManager.Instance.intimacy = Mathf.Clamp01(GameManager.Instance.intimacy);
                // F2 => 小数点2桁まで表示
                Debug.Log($"🐕 撫でてもらって親密度UP！ 現在の親密度: {GameManager.Instance.intimacy:F2}");
            }
            if (_animator != null)
            {
                _animator.SetTrigger("PatHeadTrigger");
                // 振動再生
                player.Play(Controller.Both);
                Debug.Log("🐕 PatHeadアニメーション再生！");
            }
            _isPatting = true;
            _pettingStartTime = Time.time;
        }
        // 開始待機コルーチンが終了したことを表す
        _startDelayCoroutine = null;
    }
    
    // 撫で終わりの処理
    private System.Collections.IEnumerator EndPetAnimation()
    {
        yield return new WaitForSeconds(PetEndDelay);
        
        // 1秒後、まだ離れたままなら終了処理
        if (!_isTouching)
        {
            // 撫でていた時間を計算
            float pettingDuration = 0f;
            if (_isPatting)
            {
                pettingDuration = Time.time - _pettingStartTime;
            }
            
            Debug.Log("🐕 1秒経過！撫で終了処理開始");
            Debug.Log($"🐕 撫でていた時間: {pettingDuration:F2}秒");
            _isPatting = false;
             
            // Breathingアニメーション再生
            if (_animator != null)
            {
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("PatHeadLoop"))
                {
                    _animator.SetTrigger("PatHeadEndTrigger");
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
}
