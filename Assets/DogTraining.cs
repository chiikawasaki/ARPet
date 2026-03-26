using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.IO; 
using System.Text; 

public class DogTraining : MonoBehaviour
{
    // ========================================================================
    // Inspector設定項目
    // ========================================================================
    [Header("参照オブジェクト")]
    public Transform snack;       
    public Transform dogHead;     
    public Animator dogAnimator;  

    [Header("お座り判定パラメーター")]
    public float detectDistance = 1.0f;       
    public float upwardSpeedThreshold = 0.5f; 
    public float cooldownTime = 1.0f;         

    [Header("食べる判定パラメーター")]
    public float eatDistance = 0.6f;          
    public float waitTimeBeforeEat = 1.0f;    

    [Header("AIサーバー設定")]
    public string trainApiUrl = "http://localhost:8000/pet/train";

    [Header("実験用設定")]
    public bool enableLogging = true; 
    private string logFilePath;       
    private int _autoTrialCount = 0; 

    // ========================================================================
    // 内部変数
    // ========================================================================
    private float lastTriggerTime = -99f;
    private Vector3 lastSnackPosition;
    private bool isEating = false; 
    private float stopTimer = 0f; 
    private bool hasSat = false; 


    // ========================================================================
    // Unity Lifecycle
    // ========================================================================
    void Start()
    {
        FindSnack();
        
        string folderPath = Path.Combine(Application.dataPath, "../ExperimentLogs");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logFilePath = Path.Combine(folderPath, $"SnackTrainingData_{timestamp}.csv");

        if (enableLogging)
        {
            string header = "Trial,Proficiency_Before,Energy_Before,Proficiency_Gain,Proficiency_After,Energy_After,Reaction";
            File.WriteAllText(logFilePath, header + "\n", Encoding.UTF8);
        }
    }

    void Update()
    {
        if (snack == null)
        {
            FindSnack();
            return;
        }

        if (!isEating)
        {
            if (!hasSat) CheckSit();
            else CheckEat();
        }

        if(snack != null) lastSnackPosition = snack.position;
    }


    // ========================================================================
    // Logic Methods
    // ========================================================================
    void FindSnack()
    {
        GameObject foundSnack = GameObject.Find("CakeRabbit");
        if (foundSnack != null)
        {
            snack = foundSnack.transform;
            lastSnackPosition = snack.position;
            hasSat = false; 
        }
    }

    void CheckSit()
    {
        float verticalSpeed = (snack.position.y - lastSnackPosition.y) / Time.deltaTime;
        float distance = Vector3.Distance(dogHead.position, snack.position);

        if (distance < detectDistance && 
            verticalSpeed > upwardSpeedThreshold && 
            Time.time > lastTriggerTime + cooldownTime)
        {
            dogAnimator.SetTrigger("SitTrigger");
            lastTriggerTime = Time.time;
            hasSat = true;
            Debug.Log("お座り成功！AIに報告します...");

            StartCoroutine(PostTrainingResult(true, false)); 
        }
    }

    void CheckEat()
    {
        float moveSpeed = Vector3.Distance(snack.position, lastSnackPosition) / Time.deltaTime;
        if (moveSpeed < 0.01f) stopTimer += Time.deltaTime;
        else stopTimer = 0f; 

        float distance = Vector3.Distance(dogHead.position, snack.position);

        if (distance < eatDistance && stopTimer > waitTimeBeforeEat)
        {
            StartCoroutine(EatSnackSequence());
        }
    }

    // ========================================================================
    // ★ 自動実験用機能
    // ========================================================================
    [ContextMenu("Auto Train 100 Times")]
    public void RunAutoTrainTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("⚠️ エラー: 自動テストはUnityの「再生ボタン」を押してから実行してください！");
            return;
        }
        StartCoroutine(RunAutomatedTrainingSequence(100));
    }

    IEnumerator RunAutomatedTrainingSequence(int count)
    {
        Debug.Log("🍖 おやつ訓練の自動実行を開始します（エネルギー減少なしモード）...");

        if (GameManager.Instance == null)
        {
            Debug.LogError("❌ エラー: GameManagerが見つかりません。");
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            _autoTrialCount++;
            yield return StartCoroutine(PostTrainingResult(true, true));
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"✅ 訓練終了！データ保存先: {logFilePath}");
    }


    // ========================================================================
    // AI Communication (Coroutine)
    // ========================================================================
    IEnumerator PostTrainingResult(bool success, bool isAutoMode)
    {
        if (GameManager.Instance == null) yield break;

        // 送信前の値を保持
        float beforeProf = GameManager.Instance.DogProficiency;
        float beforeEnergy = GameManager.Instance.DogEnergy;

        GameManager.Instance.PracticeCountInSession++;

        TrainReq reqData = new TrainReq
        {
            current_proficiency = beforeProf,
            practice_count = GameManager.Instance.PracticeCountInSession,
            energy = beforeEnergy,
            is_success = success
        };

        string json = JsonUtility.ToJson(reqData);

        using (UnityWebRequest req = new UnityWebRequest(trainApiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                TrainRes resData = JsonUtility.FromJson<TrainRes>(req.downloadHandler.text);
                
                // ★【修正ポイント】熟練度は更新するが、エネルギーは更新しない
                GameManager.Instance.DogProficiency = resData.new_proficiency;
                // GameManager.Instance.DogEnergy = resData.new_energy; // ← コメントアウトしてエネルギー減少を防ぐ

                if (!isAutoMode)
                {
                    Debug.Log($"熟練度UP: {resData.proficiency_gain}");
                }

                // CSVログ出力
                if (enableLogging)
                {
                    // ログには「減らなかった結果（beforeEnergy）」を記録してグラフを平坦にする
                    // ※サーバーが返してきた resData.new_energy を使うと、実際には減ってないのにログだけ減って見えるため
                    LogResultToCSV(beforeProf, beforeEnergy, resData.proficiency_gain, resData.new_proficiency, beforeEnergy, resData.reaction);
                }
            }
        }
    }

    // ========================================================================
    // ログ書き込み処理
    // ========================================================================
    void LogResultToCSV(float profBefore, float energyBefore, float gain, float profAfter, float energyAfter, string reaction)
    {
        // パス生成の安全装置
        if (string.IsNullOrEmpty(logFilePath))
        {
            string folderPath = Path.Combine(Application.dataPath, "../ExperimentLogs");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            logFilePath = Path.Combine(folderPath, $"SnackTrainingData_{timestamp}.csv");
            
            string header = "Trial,Proficiency_Before,Energy_Before,Proficiency_Gain,Proficiency_After,Energy_After,Reaction";
            File.WriteAllText(logFilePath, header + "\n", Encoding.UTF8);
            Debug.LogWarning("⚠️ ログパス再生成: " + logFilePath);
        }

        int count = (_autoTrialCount > 0) ? _autoTrialCount : GameManager.Instance.PracticeCountInSession;
        string line = $"{count},{profBefore},{energyBefore},{gain},{profAfter},{energyAfter},{reaction}";
        File.AppendAllText(logFilePath, line + "\n", Encoding.UTF8);
    }

    // ========================================================================
    // Visual Sequences (変更なし)
    // ========================================================================
    IEnumerator EatSnackSequence()
    {
        isEating = true; 
        dogAnimator.SetBool("IsEating", true);
        Vector3 originalScale = snack.localScale;
        yield return new WaitForSeconds(2.0f); 
        if (snack != null) snack.localScale = originalScale * 0.66f; 
        yield return new WaitForSeconds(1.0f); 
        if (snack != null) snack.localScale = originalScale * 0.33f; 
        yield return new WaitForSeconds(1.0f); 
        dogAnimator.SetBool("IsEating", false);
        if (snack != null) { Destroy(snack.gameObject); snack = null; }
        isEating = false; 
    }

    [System.Serializable]
    public class TrainReq { public float current_proficiency; public int practice_count; public float energy; public bool is_success; }
    [System.Serializable]
    public class TrainRes { public float new_proficiency; public float proficiency_gain; public float new_energy; public string reaction; public string comment; }
}