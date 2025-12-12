using UnityEngine;

public class AnimatorDebugger : MonoBehaviour
{
    private Animator anim;
    private int lastStateHash = 0;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (anim == null) return;

        // 現在再生中のアニメーション状態を取得
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);

        // 状態が変わった時だけログを出す
        if (stateInfo.shortNameHash != lastStateHash)
        {
            // ハッシュ値だと読めないので、分かる範囲で推測するか、
            // ステートが切り替わったタイミングだけでも分かればOK
            Debug.Log($"<color=yellow>【アニメーション遷移】</color> 新しい状態ハッシュ: {stateInfo.shortNameHash}");
            
            // もし名前が分かれば出す（クリップ名などで確認）
            if (stateInfo.IsName("Sit_Blend_Idle")) Debug.Log("→ 今：Sit_Blend_Idle (座る動作中)");
            else if (stateInfo.IsName("SitLoop2")) Debug.Log("→ 今：SitLoop2 (座り待機中)");
            else if (stateInfo.IsName("Expression_Eat")) Debug.Log("→ 今：Expression_Eat (食事中)");
            else Debug.Log("→ 今：その他の状態");

            lastStateHash = stateInfo.shortNameHash;
        }
    }
}