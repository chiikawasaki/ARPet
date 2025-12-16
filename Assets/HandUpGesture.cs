using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

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

    private DogController _dog; // 犬
    private float _lastTriggerTime;
    private Vector3 _prevPos;
    private bool _isInitialized = false;

    void Start()
    {
        _isInitialized = false;
        // Startではあえて探しません（まだいないかもしれないから）
    }

    void Update()
    {
        // ---------------------------------------------------------
        // 【ここが修正ポイント】
        // 犬がまだ見つかっていないなら、毎フレーム探しに行く！
        // ---------------------------------------------------------
        if (_dog == null)
        {
            _dog = FindObjectOfType<DogController>();
            
            // まだ見つからないなら、ハンドトラッキング処理もせず一旦帰る
            if (_dog == null) return;

            // 見つかった瞬間だけログを出す
            Debug.Log("🐕 犬が出現しました！追跡を開始します！");
        }

        // --- ここから下はいつもの処理 ---

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
        if (isFist) {
            Debug.Log("✊✊✊✊✊グーになったよ✊✊✊✊✊");
            Debug.Log("はやさ：" + velocity.y);
        }
        if (isFist && velocity.y > requiredSpeed)
        {
            Debug.Log($"おすわり指令！ Speed: {velocity.y:F2}");
            
            // 見つけてある犬に命令！
            if (_dog != null)
            {
                _dog.Sit();
            }
            
            _lastTriggerTime = Time.time;
        }
    }
}