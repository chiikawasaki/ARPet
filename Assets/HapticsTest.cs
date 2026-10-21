using UnityEngine;
using Oculus.Haptics;
// コルーチンを使用するために追加
using System.Collections;

public class HapticsTest : MonoBehaviour
{// Assign both clips in the Unity Editor
    public HapticClip clip1;
    public HapticClip clip2;
    private HapticClipPlayer player;

    // シーン開始時にインスタンス化
// 主に初期化処理で使用

    void Awake()
{
    Debug.Log("① Awake開始");

    player = new HapticClipPlayer(clip2);

    Debug.Log("② インスタンス化完了");
}

void OnEnable()
{
    Debug.Log("③ OnEnable実行");
}

IEnumerator Start()
{
    Debug.Log("④ Start開始");

    yield return new WaitForSecondsRealtime(1f);

    Debug.Log("⑤ 振動再生");
    player.Play(Controller.Both);
}
}
