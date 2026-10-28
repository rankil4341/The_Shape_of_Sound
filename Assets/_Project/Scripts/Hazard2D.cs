using UnityEngine;

/// <summary>
/// 碰到玩家就把玩家送回出生点。
/// 用法：挂在带 Collider2D 且勾了 Is Trigger 的物体上（齿轮、尖刺预制体）。
/// spawnPoint 留空时，会自动在场景里找名叫 "Spawn" 的物体。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Hazard2D : MonoBehaviour
{
    [Header("重生点（留空则自动找场景里名为 Spawn 的物体）")]
    public Transform spawnPoint;

    [Header("两次触发的最短间隔（秒），防止连续触发")]
    public float cooldown = 0.5f;

    float timer;

    void Update()
    {
        if (timer > 0f) timer -= Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other) { TryKill(other); }
    void OnTriggerStay2D(Collider2D other) { TryKill(other); }

    void TryKill(Collider2D other)
    {
        if (timer > 0f) return;
        if (!other.CompareTag("Player")) return;

        Transform target = spawnPoint != null ? spawnPoint : FindSpawn();
        if (target == null)
        {
            Debug.LogWarning("Hazard2D：没找到重生点。请在 spawnPoint 拖一个物体，或建一个名叫 Spawn 的空物体。");
            return;
        }

        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.position = target.position;
        }
        else
        {
            other.transform.position = target.position;
        }

        timer = cooldown;
    }

    static Transform FindSpawn()
    {
        GameObject go = GameObject.Find("Spawn");
        return go != null ? go.transform : null;
    }
}
