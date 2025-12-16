// ジェスチャーをしたときに、お座りするかどうかを判定するコード
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine.Networking; // 通信用
using System.Collections;

public class HandUpGesture : MonoBehaviour
{
    [Header("設定")]
    [SerializeField, Interface(typeof(IActiveState))] private UnityEngine.Object _fistState;
    public IActiveState FistState => _fistState as IActiveState;

    [SerializeField, Interface(typeof(IHand))] private UnityEngine.Object _hand;
    public IHand Hand => _hand as IHand;

    [Header("調整パラメータ")]
    [SerializeField] private float requiredSpeed = 0.5f;
    [SerializeField] private float cooldownTime = 1.0f;

    [Header("AIサーバー設定")]
    public string productionApiUrl = "http://localhost:8000/pet/production";

    private Animator _animator;

    private DogController _dog;
    private float _lastTriggerTime;
    private Vector3 _prevPos;
    private bool _isInitialized = false;
    private int _comboCount = 0; 
    

    void Start()
    {
        _isInitialized = false;
        _comboCount = 0;
        // Startではあえて探しません（まだいないかもしれないから）
    }

    void Update()
    {
        // 犬がまだ見つかっていないなら、毎フレーム探しに行く
        if (_dog == null)
            {
                _dog = FindObjectOfType<DogController>();
                _animator = _dog.GetComponent<Animator>();
            // まだ見つからないなら、ハンドトラッキング処理もせず一旦帰る
            if (_dog == null) return;

            // 見つかった瞬間だけログを出す
            Debug.Log("🐕 犬が出現しました！追跡を開始します！");
        }

        if (Hand == null || !Hand.IsTrackedDataValid) return;

        Hand.GetRootPose(out Pose currentPose);

        if (!_isInitialized)
        {
            _prevPos = currentPose.position;
            _isInitialized = true;
            return;
        }

        Vector3 velocity = (currentPose.position - _prevPos) / Time.deltaTime;
        _prevPos = currentPose.position;

        if (Time.time - _lastTriggerTime < cooldownTime) return;

        // グー判定
        bool isFist = FistState != null && FistState.Active;
        if (isFist && velocity.y > requiredSpeed)
        {
            Debug.Log($"おすわり指令！ Speed: {velocity.y:F2}");
            // AIに問い合わせ開始 犬の動作まで行う
            StartCoroutine(PostProductionResult());
            _lastTriggerTime = Time.time;
        }
    }

    // ========================================================================
    // AI Communication (Coroutine)
    // ========================================================================
    IEnumerator PostProductionResult()
    {
        // GameManagerがない場合は中断
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManagerが見つかりません！シーンに配置してください。");
            yield break;
        }
    
    // 送信データ作成
    ProductionReq reqData = new ProductionReq
    {
        current_proficiency = GameManager.Instance.DogProficiency,
        consecutive_practices = _comboCount,
        energy = GameManager.Instance.DogEnergy
    };

    string json = JsonUtility.ToJson(reqData);

    // HTTP POST送信
    using (UnityWebRequest req = new UnityWebRequest(productionApiUrl, "POST"))
    {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Production Result: " + req.downloadHandler.text);
            ProductionRes resData = JsonUtility.FromJson<ProductionRes>(req.downloadHandler.text);
            // ★ AIが決めた「新しい熟練度」と「新しいエネルギー」をGameManagerに保存
            GameManager.Instance.DogProficiency = resData.new_proficiency;
            GameManager.Instance.DogEnergy      = resData.new_energy;
            // コンボカウント更新
            UpdateComboCount(resData.reaction);
            // 犬のリアクションを判定し、アニメーション実行
            HandleDogReaction(resData.reaction);
        }else{
            Debug.LogError($"通信エラー: {req.error}");
        }
    }
    }
    void UpdateComboCount(string reaction)
    {
        if(reaction == "sit"){
            _comboCount++;
        }else{
            _comboCount = 0;
        }
    }
    // 犬のリアクションを受け取って、アニメーション実行
    void HandleDogReaction(string reaction)
    {
        if(_dog == null){
            Debug.Log("犬が見つかりません");
            return;
        }
        Debug.Log("犬のリアクション：" + reaction);
        switch(reaction){
            case "sit":
                _dog.Sit();
                break;
            case "fail":
                // _dog.Confused();
                _animator.Play("Confused", 0, 0.0f);
                break;
            case "tired":
                // _dog.Refuse();
                _animator.Play("Refuse", 0, 0.0f);
                break;
            default:
                // _dog.Confused();
                _animator.Play("Confused", 0, 0.0f);
                break;
        }
    }
    // ========================================================================
    // JSON Data Classes
    // ========================================================================
    [System.Serializable]
    public class ProductionReq
    {
        public float current_proficiency;
        public int consecutive_practices;
        public float energy;
    }

    [System.Serializable]
    public class ProductionRes
    {
        public string reaction;
        public float new_proficiency;
        public float proficiency_gain;
        public float new_energy;
        public string comment;
    }

}