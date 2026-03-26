using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement; // シーン管理用

public class ExperimentRunner : MonoBehaviour
{
    // ========================================================================
    // シングルトン化（重複防止）
    // ========================================================================
    public static ExperimentRunner Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // シーン移動しても破壊されないようにする
        }
        else
        {
            Destroy(gameObject); // 既に存在していたら自分を消す
        }
    }

    // ========================================================================
    // 設定項目
    // ========================================================================
    [Header("実験設定")]
    public int trainingStep = 10;      
    public int maxTrainingCount = 100; 
    public int testTrials = 10;        

    [Header("シーン名設定")]
    public string trainingSceneName = "Traning";   // 訓練用シーン（おやつあり）
    public string productionSceneName = "Intaraction";     // 本番用シーン（おやつなし）

    [Header("API設定")]
    public string trainApiUrl = "http://localhost:8000/pet/train";
    public string productionApiUrl = "http://localhost:8000/pet/production";

    private string logFilePath;

    // JSON用クラス定義 (変更なし)
    [System.Serializable] public class TrainReq { public float current_proficiency; public int practice_count; public float energy; public bool is_success; }
    [System.Serializable] public class TrainRes { public float new_proficiency; public float proficiency_gain; public float new_energy; public string reaction; }
    [System.Serializable] public class ProdReq { public float current_proficiency; public int consecutive_practices; public float energy; }
    [System.Serializable] public class ProdRes { public string reaction; public float new_proficiency; public float new_energy; }


    [ContextMenu("Run Full Experiment")]
    public void StartExperiment()
    {
        if (!Application.isPlaying) return;
        StartCoroutine(ExperimentCoroutine());
    }

    IEnumerator ExperimentCoroutine()
    {
        Debug.Log("🚀 シーン横断型実験を開始します...");
        SetupLogFile();

        // 初期化
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DogProficiency = 0;
            GameManager.Instance.DogEnergy = 0.5f;
            GameManager.Instance.PracticeCountInSession = 0;
        }

        int currentTrainCount = 0;

        while (currentTrainCount <= maxTrainingCount)
        {
            // ----------------------------------------------------------------
            // 1. 成功率の測定 (Production Scene)
            // ----------------------------------------------------------------
            Debug.Log($"📊 【測定フェーズ】 訓練{currentTrainCount}回時点の性能を測ります...");

            // 本番シーンへ移動
            yield return StartCoroutine(LoadSceneAndWait(productionSceneName));

            // GameManagerがシーン移動で再生成されている可能性があるので再取得（シングルトンならOK）
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DogEnergy = 0.5f; // エネルギーを0.5に設定
            }
            float savedProf = GameManager.Instance.DogProficiency;
            float savedEnergy = 0.5f; // 常に0.5を使用

            int successCount = 0;
            int validTrials = 0; // 成功したAPI呼び出し回数（testTrials回になるまで続ける）
            int attemptCount = 0;
            int consecutivePractices = 0; // 連続試行回数（シーン切り替え時に0にリセット、成功した試行ごとに増える）
            int maxAttempts = testTrials * 10; // 無限ループ防止（最大試行回数を設定）
            
            while (validTrials < testTrials && attemptCount < maxAttempts)
            {
                attemptCount++;
                bool? apiResult = null;
                bool apiSuccess = false;
                yield return StartCoroutine(CallProductionApi(consecutivePractices, (result, success) => 
                { 
                    apiResult = result; 
                    apiSuccess = success;
                }));
                
                // API呼び出し自体が成功した場合のみカウント
                if (apiSuccess)
                {
                    validTrials++;
                    consecutivePractices++; // 成功した試行ごとに連続回数を増やす
                    if (apiResult == true) successCount++;
                }
                else
                {
                    Debug.LogWarning($"   測定APIエラー (試行: {attemptCount}) - リトライします...");
                    // エラー時は連続回数をリセット（または増やさない）
                }
                // エネルギーを常に0.5に保つ
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.DogEnergy = 0.5f;
                }
                yield return new WaitForSeconds(0.05f);
            }

            float successRate = validTrials > 0 ? (float)successCount / validTrials * 100f : 0f;
            int errorCount = attemptCount - validTrials;
            Debug.Log($"   Result: {successCount}/{validTrials} 成功 ({successRate:F1}%) [総試行: {attemptCount}, エラー: {errorCount}回]");
            
            if (validTrials < testTrials)
            {
                Debug.LogError($"⚠️ 警告: 測定回数が不足しています ({validTrials}/{testTrials})");
            }
            
            LogToCSV(currentTrainCount, savedProf, successRate, validTrials, errorCount);

            // パラメータ復元
            GameManager.Instance.DogProficiency = savedProf;
            GameManager.Instance.DogEnergy = 0.5f; // エネルギーは常に0.5に固定

            // 終了条件チェック
            if (currentTrainCount >= maxTrainingCount) break;


            // ----------------------------------------------------------------
            // 2. 訓練の実行 (Training Scene)
            // ----------------------------------------------------------------
            Debug.Log($"💪 【訓練フェーズ】 訓練シーンへ移動して +{trainingStep}回 強化します...");

            // 訓練シーンへ移動
            yield return StartCoroutine(LoadSceneAndWait(trainingSceneName));

            // エネルギーを0.5に設定
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DogEnergy = 0.5f;
            }

            // 成功した訓練回数だけをカウント（エラー時はリトライ）
            int successfulTrainings = 0;
            int trainingAttemptCount = 0;
            int maxTrainingAttempts = trainingStep * 10; // 無限ループ防止（最大試行回数を設定）
            
            while (successfulTrainings < trainingStep && trainingAttemptCount < maxTrainingAttempts)
            {
                trainingAttemptCount++;
                bool apiSuccess = false;
                yield return StartCoroutine(CallTrainApi((success) => { apiSuccess = success; }));
                
                if (apiSuccess)
                {
                    successfulTrainings++;
                    currentTrainCount++;
                    Debug.Log($"   訓練成功: {successfulTrainings}/{trainingStep} (総試行: {trainingAttemptCount})");
                }
                else
                {
                    Debug.LogWarning($"   訓練APIエラー (試行: {trainingAttemptCount}) - リトライします...");
                }
                // エネルギーを常に0.5に保つ
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.DogEnergy = 0.5f;
                }
                yield return new WaitForSeconds(0.05f);
            }
            
            if (successfulTrainings < trainingStep)
            {
                Debug.LogError($"⚠️ 警告: 訓練回数が不足しています ({successfulTrainings}/{trainingStep})");
            }
        }

        Debug.Log($"✅ 全実験完了！ CSV: {logFilePath}");
    }


    // ========================================================================
    // シーン読み込み待ち用コルーチン
    // ========================================================================
    IEnumerator LoadSceneAndWait(string sceneName)
    {
        // 既にそのシーンにいるなら何もしない
        if (SceneManager.GetActiveScene().name == sceneName) yield break;

        Debug.Log($"Scene Loading: {sceneName}...");
        
        // 非同期読み込み開始
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // 完了するまで待機
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // シーン読込直後は重いことがあるので少し待つ
        yield return new WaitForSeconds(0.5f);
    }


    // ========================================================================
    // API関数（エラーハンドリング強化版）
    // ========================================================================
    IEnumerator CallTrainApi(System.Action<bool> callback)
    {
        // GameManagerのnullチェック
        if (GameManager.Instance == null)
        {
            Debug.LogError("❌ CallTrainApi: GameManager.Instance が null です");
            callback(false);
            yield break;
        }

        TrainReq req = new TrainReq
        {
            current_proficiency = GameManager.Instance.DogProficiency,
            practice_count = GameManager.Instance.PracticeCountInSession,
            energy = 0.5f, // 常に0.5を送信
            is_success = true
        };

        using (UnityWebRequest r = new UnityWebRequest(trainApiUrl, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));
            r.uploadHandler = new UploadHandlerRaw(body);
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Content-Type", "application/json");
            yield return r.SendWebRequest();

            if (r.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    TrainRes res = JsonUtility.FromJson<TrainRes>(r.downloadHandler.text);
                    if (res != null)
                    {
                        GameManager.Instance.DogProficiency = res.new_proficiency;
                        GameManager.Instance.DogEnergy = 0.5f; // エネルギーは常に0.5に固定
                        callback(true);
                    }
                    else
                    {
                        Debug.LogError($"❌ CallTrainApi: JSONの解析に失敗しました。レスポンス: {r.downloadHandler.text}");
                        callback(false);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ CallTrainApi: JSON解析エラー - {e.Message}\nレスポンス: {r.downloadHandler.text}");
                    callback(false);
                }
            }
            else
            {
                Debug.LogError($"❌ CallTrainApi: APIエラー - {r.result}\nURL: {trainApiUrl}\nエラー: {r.error}");
                callback(false);
            }
        }
    }

    IEnumerator CallProductionApi(int consecutivePractices, System.Action<bool, bool> callback)
    {
        // GameManagerのnullチェック
        if (GameManager.Instance == null)
        {
            Debug.LogError("❌ CallProductionApi: GameManager.Instance が null です");
            callback(false, false);
            yield break;
        }

        ProdReq req = new ProdReq
        {
            current_proficiency = GameManager.Instance.DogProficiency,
            consecutive_practices = consecutivePractices, // 連続試行回数を渡す
            energy = 0.5f // 常に0.5を送信
        };

        using (UnityWebRequest r = new UnityWebRequest(productionApiUrl, "POST"))
        {
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(req));
            r.uploadHandler = new UploadHandlerRaw(body);
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Content-Type", "application/json");
            yield return r.SendWebRequest();

            if (r.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    ProdRes res = JsonUtility.FromJson<ProdRes>(r.downloadHandler.text);
                    if (res != null)
                    {
                        bool isSit = (res.reaction == "sit");
                        GameManager.Instance.DogEnergy = 0.5f; // エネルギーは常に0.5に固定
                        callback(isSit, true); // 第2引数: API呼び出しが成功したかどうか
                    }
                    else
                    {
                        Debug.LogError($"❌ CallProductionApi: JSONの解析に失敗しました。レスポンス: {r.downloadHandler.text}");
                        callback(false, false);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ CallProductionApi: JSON解析エラー - {e.Message}\nレスポンス: {r.downloadHandler.text}");
                    callback(false, false);
                }
            }
            else
            {
                Debug.LogError($"❌ CallProductionApi: APIエラー - {r.result}\nURL: {productionApiUrl}\nエラー: {r.error}");
                callback(false, false); // 第1引数: 結果、第2引数: API呼び出し失敗
            }
        }
    }

    void SetupLogFile()
    {
        string folder = Path.Combine(Application.dataPath, "../ExperimentLogs");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logFilePath = Path.Combine(folder, $"SceneExperiment_{ts}.csv");
        File.WriteAllText(logFilePath, "TrainingCount,Proficiency,SuccessRate,ValidTrials,ErrorCount\n", Encoding.UTF8);
    }

    void LogToCSV(int trainCount, float prof, float rate, int validTrials, int errorCount)
    {
        string line = $"{trainCount},{prof},{rate},{validTrials},{errorCount}\n";
        File.AppendAllText(logFilePath, line, Encoding.UTF8);
    }
}