using UnityEngine;

/// <summary>
/// 让物体绕 Z 轴匀速旋转。
/// 挂在齿轮、风扇这类"要自己转"的物体上。
/// 只负责转，不负责伤害（伤害交给 Hazard2D）。
/// </summary>
public class Spin2D : MonoBehaviour
{
    [Header("转速（度/秒）：正数逆时针，负数顺时针")]
    public float degreesPerSecond = 90f;

    [Header("临时暂停（调试用）")]
    public bool paused = false;

    void Update()
    {
        if (paused) return;
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
