using UnityEngine;

public class MiniMapFollower : MonoBehaviour
{
    public Transform targetCamera;     // Main Camera
    public Vector3 localOffset = new Vector3(0.25f, -0.20f, 0.6f);

    void LateUpdate()
    {
        if (!targetCamera) return;

        // カメラ基準で右・下・前方向にオフセット
        Vector3 worldOffset =
            targetCamera.right * localOffset.x +
            targetCamera.up * localOffset.y +
            targetCamera.forward * localOffset.z;

        transform.position = targetCamera.position + worldOffset;

        // UIをカメラと同じ向きにする
        transform.rotation = Quaternion.LookRotation(
            targetCamera.forward,
            targetCamera.up
        );
    }
}
