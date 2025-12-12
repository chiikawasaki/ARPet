using UnityEngine;

public class SmoothUIFollow : MonoBehaviour
{
    public Transform targetCamera; // Main Cameraを割り当てる
    public float distance = 2.0f;  // 顔からの距離
    public float smoothness = 0.1f; // 追従の遅れ具合（0に近いほどキビキビ、大きいほどふわふわ）

    void Update()
    {
        if (targetCamera == null) return;

        // 目標位置：カメラの正面方向に distance 分だけ離れた場所
        Vector3 targetPosition = targetCamera.position + targetCamera.forward * distance;
        
        // 現在位置から目標位置まで滑らかに移動（Lerp）
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothness);

        // 常にカメラの方を向く（ビルボード処理）
        transform.LookAt(transform.position + targetCamera.rotation * Vector3.forward, targetCamera.rotation * Vector3.up);
    }
}