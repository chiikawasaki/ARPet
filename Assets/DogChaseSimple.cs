// unityのゲーム開発に必要な機能を使えるようにするための宣言
using UnityEngine;
using System.Collections;

// このスクリプトをアタッチしたオブジェクトには必ずRigidbodyが必要
[RequireComponent(typeof(Rigidbody))]
public class DogChaseSimple : MonoBehaviour
{
    // ====== 設定パラメータ ======
    public string ballTag = "Ball";     // ボールのタグ
    public float speed = 2f;            // 犬の移動速度
    public float stopDistance = 0.2f;   // どれくらい近づいたら止まるか
    public float throwThreshold = 0.5f; // これ以上の速度なら「投げられた」

    // 到達後のクールダウン(0.6秒)
    public float arriveCooldownSec = 0.6f;

    // 口の位置（犬プレハブの中に設置）
    public Transform mouthPoint;
    // 口に収めるときの微調整　犬の口の位置からボールをどれだけずらすか
    public Vector3 mouthLocalPosOffset = new Vector3(0f, 0f, 0.08f);
    // ボールの角度を調整
    public Vector3 mouthLocalEulerOffset = Vector3.zero;

    // プレイヤーにどこまで近づいたら渡すか
    public float deliverDistance = 0.7f;

    // 口から離れたとみなす距離（念のための自動ドロップ用）
    public float autoDropDistanceFromMouth = 0.2f;

    // ====== 内部状態 ======
    // シーン内のボール
    private GameObject ball;
    // ボールのRigidbody（物理演算）
    private Rigidbody ballRb;
    // 犬のRigidbody
    private Rigidbody rb;

    private bool chasing = false;         // 追跡中かどうか
    private bool ballWasThrown = false;   // ボールが投げられたかどうか
    private bool arrivedCoolDown = false; // 到達後クールダウン中であるかどうか
    private bool carryingToPlayer = false;// プレイヤーへ運搬中であるかどうか
    private bool holdingBall = false;     // 口にくわえてるかどうか

    // アニメーター
    public Animator animator;
    // ステート名をハッシュ化（高速化）
    private static readonly int stBreathing   = Animator.StringToHash("Breathing");
    private static readonly int stRunning     = Animator.StringToHash("Running");
    private static readonly int stEatingStart = Animator.StringToHash("EatingStart");

    // 犬が自分で落とした後はこの時刻まで投げ検出を無視
    private float ignoreThrowUntil = 0f;
    // 無視する秒数（好みで調整）
    public float ignoreAfterDogDropSec = 0.8f;

    // ====== Unityイベント ======
    void Start()
    {
        // 犬自身のRigidbodyを取得
        rb = GetComponent<Rigidbody>();
        // 犬自身のAnimatorを取得
        animator = GetComponent<Animator>();
        // 犬の重力を有効に
        rb.useGravity = true;

        ball = GameObject.FindGameObjectWithTag(ballTag);
        if (ball) ballRb = ball.GetComponent<Rigidbody>();

        // 起動時にもしボールが口の子になっていたら、口から外して正常な状態にする
        if (ball && mouthPoint && ball.transform.IsChildOf(mouthPoint))
        {
            // ワールド座標を維持したまま親オブジェクトととの親子関係を解除
            ball.transform.SetParent(null, true);
            // ボールのRbを初期化
            if (ballRb)
            {
                ballRb.isKinematic = false;
                ballRb.useGravity = true;
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
            }
            // ボールのコライダーを取得
            var col = ball.GetComponent<Collider>();
            // 当たり判定を有効にする
            if (col) col.isTrigger = false;
        }
    }

    void Update()
    {
        // ボールを見失っていたら探す
        if (!ball)
        {
            ball = GameObject.FindGameObjectWithTag(ballTag);
            if (ball) ballRb = ball.GetComponent<Rigidbody>();
            return;
        }
        if (!ballRb) return;

        // holdingBall==trueの際に、口から外れたり距離離れたりしたら落として手元に置く
        if (holdingBall)
        {
            if (!mouthPoint ||
                ball.transform.parent != mouthPoint ||
                Vector3.Distance(ball.transform.position, mouthPoint.position) > autoDropDistanceFromMouth)
            {
                DropBall();
            }
        }

        // ===== 投球検出 =====
        bool nowThrown = (Time.time >= ignoreThrowUntil) && (ballRb.linearVelocity.magnitude > throwThreshold);
        if (nowThrown)
        {
            ballWasThrown = true;

            // 投げられた瞬間は物理衝突が必要（拾うまではすり抜けNG）
            ballRb.useGravity = true;
            var ballCol = ball.GetComponent<Collider>();
            if (ballCol) ballCol.isTrigger = false;

            // 到達後の抑止を解除
            arrivedCoolDown = false;
        }

        // ===== 追跡開始判定（ボールがほぼ停止して地面付近にある想定） =====
        if (!arrivedCoolDown && ballWasThrown && !chasing
            // ここを調節する必要あり
            && ballRb.linearVelocity.magnitude < 0.05f
            && ball.transform.position.y < 0.3f)
        {
            chasing = true;
            if (animator) animator.CrossFade(stRunning, 0.1f, 0, 0f);
            // Debug.Log("Dog starts chasing!");
        }

        // ===== 追跡処理 =====
        if (chasing)
        {
            Vector3 target = new Vector3(ball.transform.position.x, transform.position.y, ball.transform.position.z);
            Vector3 dir = (target - transform.position).normalized;

            if (Vector3.Distance(transform.position, target) > stopDistance)
            {
                rb.MovePosition(transform.position + dir * speed * Time.deltaTime);
                transform.LookAt(target);
            }
            else
            {
                // ボールに到達
                chasing = false;
                ballWasThrown = false;
                arrivedCoolDown = true;

                if (animator)
                {
                    // ボールを拾う演技 → 終了後に口へ
                    PlayEatingAndGrab();
                }
            }
        }

        // ===== プレイヤーへ運搬 =====
        if (carryingToPlayer)
        {
            // プレイヤー位置（今回はカメラ）
            Vector3 playerPos = Camera.main ? Camera.main.transform.position : Vector3.zero;
            Vector3 returnTarget = new Vector3(playerPos.x, transform.position.y, playerPos.z);
            Vector3 dir = (returnTarget - transform.position);
            float dist = dir.magnitude;

            if (dist > deliverDistance)
            {
                dir.Normalize();
                rb.MovePosition(transform.position + dir * speed * Time.deltaTime);
                transform.LookAt(returnTarget);
            }
            else
            {
                // 到着：ここで渡す
                carryingToPlayer = false;
                if (animator) animator.CrossFade(stBreathing, 0.1f, 0, 0f);

                // 口から落として手元に置く
                DropBall();
            }
        }

        // 必要なら状態ログ
        // Debug.Log($"thrown={ballWasThrown} chasing={chasing} holding={holdingBall} carry={carryingToPlayer} vel={ballRb.velocity.magnitude:F3}");
    }

    // ===== アニメ → 終了待ち → 掴む =====
    void PlayEatingAndGrab()
    {
        if (!animator) return;
        animator.CrossFade(stEatingStart, 0.05f, 0, 0f);
        StartCoroutine(WaitEatingThenGrab());
    }

    IEnumerator WaitEatingThenGrab()
    {
        yield return null;

        // 目標ステートに入るまで待機
        while (animator.IsInTransition(0) ||
               animator.GetCurrentAnimatorStateInfo(0).shortNameHash != stEatingStart)
        {
            yield return null;
        }

        // 終了まで待機
        var info = animator.GetCurrentAnimatorStateInfo(0);
        while (info.normalizedTime < 1f)
        {
            yield return null;
            info = animator.GetCurrentAnimatorStateInfo(0);
        }

        // 掴む（口へスナップ）
        GrabBall();
    }

    // ===== くわえる / 離す =====
    void GrabBall()
    {
        if (!ball || !ballRb || !mouthPoint) return;

        // 物理停止・安定化
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.useGravity = false;
        ballRb.isKinematic = true;

        // 口の中はすり抜けにしておく
        var ballCol = ball.GetComponent<Collider>();
        if (ballCol) ballCol.isTrigger = true;

        // 親子付け（口へスナップ）
        ball.transform.SetParent(mouthPoint, false);
        ball.transform.localPosition = mouthLocalPosOffset;
        ball.transform.localEulerAngles = mouthLocalEulerOffset;

        holdingBall = true;

        // プレイヤーへ運ぶ
        carryingToPlayer = true;
        if (animator) animator.CrossFade(stRunning, 0.1f, 0, 0f);
        // Debug.Log("Ball grabbed (mouth)!");
    }

    void DropBall()
    {
        if (!ball || !ballRb) return;

        //ボールの親が口なら
        if (ball.transform.parent == mouthPoint)
        {
            // 親子解除（ワールド座標維持）
            ball.transform.SetParent(null, true);
        }

        // 物理復帰
        ballRb.isKinematic = false;
        ballRb.useGravity = true;

        var col = ball.GetComponent<Collider>();
        if (col) col.isTrigger = false;

        holdingBall = false;

        //犬が落とした直後は投げ検出＆追跡を無視
        ignoreThrowUntil = Time.time + ignoreAfterDogDropSec;

    }
}
