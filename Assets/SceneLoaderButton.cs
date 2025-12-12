using UnityEngine;
using UnityEngine.SceneManagement; // これがシーン移動に必須！

public class SceneLoader : MonoBehaviour
{
    // Inspectorで移動先のシーン名を指定できるようにする変数
    public string nextSceneName;

    // ボタンが押されたら呼ばれる機能
    public void ChangeScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}