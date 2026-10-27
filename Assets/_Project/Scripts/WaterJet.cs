using UnityEngine;

/// <summary>
/// 水柱：激活后柱内持续把玩家往上推（直接设垂直速度，手感可控）。
/// Activate()/Deactivate() 给按钮/时钟的 UnityEvent 拖线用。
/// 需要一个勾了 Is Trigger 的 BoxCollider2D。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class WaterJet : MonoBehaviour
{
    [Header("推力")]
    public float pushSpeed = 13f;           // 托升速度（约等于起跳初速）
    public bool activeByDefault = false;    // 进场景就喷？

    [Header("视觉")]
    public SpriteRenderer columnVisual;     // 拖子物体 ColumnVisual
    public float wobbleAmount = 0.08f;      // 水柱呼吸幅度
    public float wobbleSpeed = 3f;

    BoxCollider2D col;
    Vector3 baseScale;
    public bool IsActive { get; private set; }

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        if (columnVisual != null) baseScale = columnVisual.transform.localScale;
        SetVisual(activeByDefault);
        IsActive = activeByDefault;
    }

    // ====== 给 UnityEvent 拖线 ======
    public void Activate()   { IsActive = true;  SetVisual(true); }
    public void Deactivate() { IsActive = false; SetVisual(false); }

    void SetVisual(bool on) { if (columnVisual != null) columnVisual.enabled = on; }

    // 每物理帧：玩家在水柱里 → 垂直速度直接抬到 pushSpeed
    void OnTriggerStay2D(Collider2D other)
    {
        if (!IsActive || !other.CompareTag("Player")) return;
        var rb = other.attachedRigidbody;
        if (rb != null)
            rb.velocity = new Vector2(rb.velocity.x, pushSpeed);
    }

    // 视觉：宽度轻微呼吸，看起来像活水
    void Update()
    {
        if (IsActive && columnVisual != null)
        {
            float s = 1f + Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount;
            columnVisual.transform.localScale = new Vector3(baseScale.x * s, baseScale.y, 1f);
        }
    }
}