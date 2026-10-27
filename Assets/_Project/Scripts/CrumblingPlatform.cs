using System.Collections;
using UnityEngine;

/// <summary>
/// 破碎平台：玩家站上 → 停顿 → 抖动 → 碎掉（关碰撞关显示）→ 定时重生。
/// 需要实心 BoxCollider2D（非 Trigger），Layer 建议 Ground。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CrumblingPlatform : MonoBehaviour
{
    [Header("时序")]
    public float shakeDelay = 0.5f;      // 踩上后多久开始抖
    public float shakeDuration = 0.45f;  // 抖多久后碎
    public float respawnDelay = 3f;      // 碎后多久重生

    [Header("抖动")]
    public float shakeMagnitude = 0.06f; // 抖动幅度

    BoxCollider2D col;
    SpriteRenderer sr;
    Vector3 homePos;
    Coroutine breaking;

    void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        sr = GetComponent<SpriteRenderer>();
        homePos = transform.position;
    }

    void OnCollisionStay2D(Collision2D c)
    {
        if (breaking != null) return;                       // 已经在碎的流程里
        if (!c.collider.CompareTag("Player")) return;
        // 玩家中心高于平台中心 = 站在"上面"（侧面撞不算）
        if (c.collider.transform.position.y > transform.position.y)
            breaking = StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        yield return new WaitForSeconds(shakeDelay);

        // 抖动：随机小偏移，视觉预警
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            transform.position = homePos + (Vector3)(Random.insideUnitCircle * shakeMagnitude);
            yield return null;
        }
        transform.position = homePos;

        // 碎：碰撞和显示一起关，玩家踩空
        col.enabled = false;
        sr.enabled = false;

        yield return new WaitForSeconds(respawnDelay);

        // 重生：解谜可重试
        col.enabled = true;
        sr.enabled = true;
        breaking = null;
    }
}