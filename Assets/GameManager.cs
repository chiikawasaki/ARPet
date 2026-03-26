using UnityEngine;

public class GameManager : MonoBehaviour
{
    // どこからでも GameManager.Instance でアクセスできるようにする
    public static GameManager Instance { get; private set; }

    [Header("犬の永続ステータス")]
    public float DogEnergy = 0.6f;        // 元気 (0.0 - 1.0)
    public float DogHunger = 0.0f;        // 空腹 (0.0 - 1.0)
    public float DogProficiency = 0.0f;   // おすわり熟練度 (0.0 - 100.0)
    
    // セッション（起動中）の練習回数
    public int PracticeCountInSession = 0;

    [Header("ボール投球システム")]
    public int BallThrowCount = 0;         // ボール投球回数
    public int EnergyPeakThrows = 5;       // エネルギーがピークになる投球回数
    public float EnergyGainPerThrow = 0.1f;   // 5回まで：1回あたり +0.1
    public float EnergyLossPerThrow = 0.08f;  // 6回目以降：1回あたり -0.08 

    private void Awake()
    {
        // シングルトン化（世界に1つだけにする）
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ★これがお引越しの呪文！
        }
        else
        {
            Destroy(gameObject); // 2人目は存在できないので消える
        }
    }

    // 数値を更新する便利関数
    public void UpdateStatus(float newProficiency, float currentEnergy)
    {
        DogProficiency = newProficiency;
        DogEnergy = currentEnergy;
    }

    /// <summary>
    /// ボールが投げられた時に呼び出す
    /// 5回まではエネルギー上昇、6回目以降はエネルギー下降
    /// </summary>
    public void OnBallThrown()
    {
        BallThrowCount++;

        if (BallThrowCount <= EnergyPeakThrows)
        {
            // 5回まで：エネルギー上昇
            DogEnergy += EnergyGainPerThrow;
        }
        else
        {
            // 6回目以降：エネルギー下降
            DogEnergy -= EnergyLossPerThrow;
        }

        // エネルギーを 0.0 ～ 1.0 の範囲にクランプ
        DogEnergy = Mathf.Clamp01(DogEnergy);

        Debug.Log($"ボール投球 {BallThrowCount}回目 - エネルギー: {DogEnergy:F2}");
    }

    /// <summary>
    /// ボール投球カウントとエネルギーをリセット（デバッグ用）
    /// </summary>
    public void ResetBallThrowStats()
    {
        BallThrowCount = 0;
        DogEnergy = 0.6f;
        Debug.Log("ボール投球統計をリセットしました");
    }
}