using UnityEngine;

/// 正交相机横版跟随
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, -10f);
    public float smooth = 5f;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 want = target.position + offset;
        want.z = -10f;
        transform.position = Vector3.Lerp(transform.position, want, smooth * Time.deltaTime);
    }
}