using UnityEngine;

public class DogController : MonoBehaviour
{
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    // この関数をハンドジェスチャーから呼び出します
    public void Sit()
    {
        if (_animator != null)
        {
            // Animatorで作ったトリガーの名前を指定
            _animator.SetTrigger("SitTrigger");
            Debug.Log("犬：わん！（座ったよ）");
        }
    }
    public void Confused()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("ConfusedTrigger");
            Debug.Log("犬：わん！（混乱したよ）");
        }
    }
    public void Refuse()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("RefuseTrigger");
            Debug.Log("犬：わん！（拒否したよ）");
        }
    }
}