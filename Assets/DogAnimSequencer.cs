using UnityEngine;
using System.Collections;

public class DogAnimSequencer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private const int BaseLayer = 0;

    // 犬の歩行速度（1秒間に進む距離、単位はメートル）
    [SerializeField] private float walkSpeed = 1.0f;
    [SerializeField] private float runSpeed = 3.0f;

    private void Start()
    {
        StartCoroutine(PlayLoop());
    }

    private IEnumerator PlayLoop()
    {
        while (true)
        {
            Play("Breathing", 0.15f);  yield return new WaitForSeconds(2f);
            Play("Walking01", 0.15f);  yield return StartCoroutine(MoveForwardForSeconds(walkSpeed, 3f));
            Play("Running", 0.15f);    yield return StartCoroutine(MoveForwardForSeconds(runSpeed, 2f));
        }
    }

    /// <summary>
    /// 指定時間だけ指定速度で前進する
    /// </summary>
    private IEnumerator MoveForwardForSeconds(float speed, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 前方向に移動（Space.Selfは犬の向いている方向）
            transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }


    private void Play(string state, float fade)
    {
        animator.CrossFade(state, fade, BaseLayer, 0f);
    }
}
