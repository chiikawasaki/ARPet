using UnityEngine;
using System.Collections;
using UnityEngine.Networking; // 通信用

public class DogTraining : MonoBehaviour
{
    // ========================================================================
    // Inspector設定項目
    // ========================================================================
    [Header("参照オブジェクト")]
    public Transform snack;       // おやつ（CakeRabbitなど）
    public Transform dogHead;     // 犬の頭（距離判定用）
    public Animator dogAnimator;  // 犬のアニメーター

    [Header("お座り判定パラメーター")]
    public float detectDistance = 1.0f;       // おやつと頭の距離
    public float upwardSpeedThreshold = 0.5f; // おやつを上げる速度
    public float cooldownTime = 1.0f;         // 連続判定防止用クールダウン

    [Header("食べる判定パラメーター")]
    public float eatDistance = 0.6f;          // 口元の距離
    public float waitTimeBeforeEat = 1.0f;    // 静止してから食べるまでの時間

    [Header("AIサーバー設定")]
    public string trainApiUrl = "http://localhost:8000/pet/train";

    // ========================================================================
    // 内部変数
    // ========================================================================
    private float lastTriggerTime = -99f;
    private Vector3 lastSnackPosition;
    private bool isEating = false; 
    private float stopTimer = 0f; 
    
    // お座りが完了したかのフラグ
    private bool hasSat = false; 


    // ========================================================================
    // Unity Lifecycle
    // ========================================================================
    void Start()
    {
        FindSnack(); // 最初のおやつを探す
    }

    void Update()
    {
        // おやつがない（まだ出てない or 食べ終わった）時は、次のおやつを探し続ける
        if (snack == null)
        {
            FindSnack();
            return;
        }

        // 食べてる最中は判定しない
        if (!isEating)
        {
            if (!hasSat)
            {
                // まだ座ってないなら「お座りチェック」
                CheckSit();
            }
            else
            {
                // 座った後なら「食べるチェック」
                CheckEat();
            }
        }

        if(snack != null) lastSnackPosition = snack.position;
    }


    // ========================================================================
    // Logic Methods
    // ========================================================================

    // おやつを見つける関数
    void FindSnack()
    {
        GameObject foundSnack = GameObject.Find("CakeRabbit");
        if (foundSnack != null)
        {
            snack = foundSnack.transform;
            lastSnackPosition = snack.position;
            hasSat = false; // 新しいおやつなのでリセット
        }
    }

    // お座りのジェスチャー判定
    void CheckSit()
    {
        // おやつの垂直移動速度
        float verticalSpeed = (snack.position.y - lastSnackPosition.y) / Time.deltaTime;
        float distance = Vector3.Distance(dogHead.position, snack.position);

        // 「近くにあって」かつ「素早く上に持ち上げられた」ら判定
        if (distance < detectDistance && 
            verticalSpeed > upwardSpeedThreshold && 
            Time.time > lastTriggerTime + cooldownTime)
        {
            // アニメーション再生
            dogAnimator.SetTrigger("SitTrigger");
            lastTriggerTime = Time.time;
            
            // お座り成功フラグ
            hasSat = true;
            Debug.Log("お座り成功！AIに報告します...");

            // ★ AI通信開始（成功フラグ: true）
            StartCoroutine(PostTrainingResult(true));
        }
    }

    // 食べる動作の判定
    void CheckEat()
    {
        float moveSpeed = Vector3.Distance(snack.position, lastSnackPosition) / Time.deltaTime;
        
        // おやつが静止している時間を計測
        if (moveSpeed < 0.01f) stopTimer += Time.deltaTime;
        else stopTimer = 0f; 

        float distance = Vector3.Distance(dogHead.position, snack.position);

        // 座っていて、かつ「近くにあって一定時間静止している」なら食べる
        if (distance < eatDistance && stopTimer > waitTimeBeforeEat)
        {
            StartCoroutine(EatSnackSequence());
        }
    }


    // ========================================================================
    // AI Communication (Coroutine)
    // ========================================================================
    IEnumerator PostTrainingResult(bool success)
    {
        // GameManagerがない場合は中断
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManagerが見つかりません！シーンに配置してください。");
            yield break;
        }

        // 練習回数をカウントアップ
        GameManager.Instance.PracticeCountInSession++;

        // 送信データ作成 (GameManagerの現在の値をAIに渡す)
        TrainReq reqData = new TrainReq
        {
            current_proficiency = GameManager.Instance.DogProficiency,
            practice_count = GameManager.Instance.PracticeCountInSession,
            energy = GameManager.Instance.DogEnergy,
            is_success = success
        };

        string json = JsonUtility.ToJson(reqData);

        // HTTP POST送信
        using (UnityWebRequest req = new UnityWebRequest(trainApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // AIからの応答をパース
                TrainRes resData = JsonUtility.FromJson<TrainRes>(req.downloadHandler.text);
                
                // ★ AIが決めた「新しい熟練度」と「新しいエネルギー」をGameManagerに保存
                GameManager.Instance.DogProficiency = resData.new_proficiency;
                GameManager.Instance.DogEnergy      = resData.new_energy;

                Debug.Log($"<color=cyan>【AI判定完了】</color>\n" +
                          $"反応: {resData.reaction}\n" +
                          $"熟練度: +{resData.proficiency_gain} (Total: {resData.new_proficiency})\n" +
                          $"体力: {reqData.energy:F2} -> {resData.new_energy:F2}");
            }
            else
            {
                Debug.LogError($"通信エラー: {req.error}");
            }
        }
    }


    // ========================================================================
    // Visual Sequences
    // ========================================================================
    IEnumerator EatSnackSequence()
    {
        isEating = true; 
        Debug.Log("いただきまーす！");

        dogAnimator.SetBool("IsEating", true);

        Vector3 originalScale = snack.localScale;

        // 立ち上がる時間などを考慮して待つ
        yield return new WaitForSeconds(2.0f); 

        // 1口目
        if (snack != null) snack.localScale = originalScale * 0.66f; 
        Debug.Log("パクッ（1口目）");

        // 2口目
        yield return new WaitForSeconds(1.0f); 
        if (snack != null) snack.localScale = originalScale * 0.33f; 

        // 完食
        yield return new WaitForSeconds(1.0f); 
        
        dogAnimator.SetBool("IsEating", false);

        if (snack != null)
        {
            Destroy(snack.gameObject); 
            snack = null; 
        }

        // ここで少し回復させたいなら GameManager.Instance.DogEnergy += 0.1f; とか書いてもOK

        isEating = false; 
    }


    // ========================================================================
    // JSON Data Classes
    // ========================================================================
    [System.Serializable]
    public class TrainReq
    {
        public float current_proficiency;
        public int practice_count;
        public float energy;
        public bool is_success;
    }

    [System.Serializable]
    public class TrainRes
    {
        public float new_proficiency;
        public float proficiency_gain;
        public float new_energy; // ★ AIが計算した新しいエネルギー値
        public string reaction;
        public string comment;
    }
}