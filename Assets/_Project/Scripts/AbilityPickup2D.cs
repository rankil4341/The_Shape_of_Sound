using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 能力道具：玩家碰到 → 解锁对应能力 → 播放 onPicked（接音效/UI）→ 消失。
/// 一个道具解锁一种能力（勾选其一）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AbilityPickup2D : MonoBehaviour
{
    public bool unlockDoubleJump;
    public bool unlockDash;
    public UnityEvent onPicked;          // 反馈：音效/提示（可选）

    bool picked;

    void Reset() { GetComponent<Collider2D>().isTrigger = true; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (picked || !other.CompareTag("Player")) return;
        var pc = other.GetComponent<PlayerController2D>();
        if (pc == null) return;

        if (unlockDoubleJump) pc.UnlockDoubleJump();
        if (unlockDash)       pc.UnlockDash();

        onPicked.Invoke();
        picked = true;
        Destroy(gameObject);
    }
}