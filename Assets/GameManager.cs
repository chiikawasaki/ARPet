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
}