using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 时钟进度机关：刻度块围成表盘，AddProgress() 点亮下一格，
/// ResetProgress() 全部熄灭；满格触发 onCompleted（拖给门/传送门）。
/// 刻度块运行时自动生成，无需美术资产。
/// </summary>
public class ClockProgress : MonoBehaviour
{
    [Header("进度")]
    public int targetProgress = 3;          // 满格需要的进度数

    [Header("表盘外观")]
    public float dialRadius = 1.6f;         // 刻度围成的圆半径
    public float segmentSize = 0.5f;        // 每格大小（世界单位）
    public Color offColor  = new Color(0.20f, 0.22f, 0.30f); // 未点亮：暗蓝灰
    public Color onColor   = new Color(0.31f, 0.85f, 1.00f); // 已点亮：青色
    public Color doneColor = new Color(1.00f, 0.75f, 0.30f); // 满格：金色
    public float flashTime = 0.4f;          // 满格渐变成金色的秒数

    [Header("事件（策划拖线）")]
    public UnityEvent onCompleted;          // 满格触发一次：拖 Door.ForceOpen 等
    public UnityEvent onProgressChanged;    // 每次加/清都触发：可接音效

    [Header("临时测试键（正式版删掉 Update 里那两行）")]
    public KeyCode testAddKey = KeyCode.T;
    public KeyCode testResetKey = KeyCode.Y;

    public int Current { get; private set; }

    SpriteRenderer[] segs;
    bool completed;
    bool flashing;

    void Awake()
    {
        // ---- 运行时生成一块白色方形贴图（Point 过滤，像素风不糊）----
        const int px = 8;
        var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
        var colors = new Color32[px * px];
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        // PPU = 像素数/想要的 world 尺寸 → 恰好渲染成 segmentSize 大
        var sprite = Sprite.Create(tex, new Rect(0, 0, px, px),
            new Vector2(0.5f, 0.5f), px / segmentSize);

        // ---- 生成刻度块，从 12 点方向顺时针围一圈 ----
        segs = new SpriteRenderer[targetProgress];
        for (int i = 0; i < targetProgress; i++)
        {
            float angle = (-90f + i * 360f / targetProgress) * Mathf.Deg2Rad;
            var go = new GameObject("Seg_" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * dialRadius;
            segs[i] = go.AddComponent<SpriteRenderer>();
            segs[i].sprite = sprite;
            segs[i].color = offColor;
        }
    }

    void Update()
    {
        // 临时测试键：T 加一格，Y 清零（接线完成后删这两行）
        if (Input.GetKeyDown(testAddKey)) AddProgress();
        if (Input.GetKeyDown(testResetKey)) ResetProgress();
    }

    // ============ 策划拖线的两个函数 ============

    public void AddProgress()
    {
        if (completed) return;
        Current = Mathf.Min(Current + 1, targetProgress);
        Refresh();
        onProgressChanged.Invoke();

        if (Current >= targetProgress)
        {
            completed = true;
            if (!flashing) StartCoroutine(FlashDone());
            onCompleted.Invoke();
        }
    }

    public void ResetProgress()
    {
        Current = 0;
        completed = false;
        Refresh();
        onProgressChanged.Invoke();
    }

    void Refresh()
    {
        for (int i = 0; i < segs.Length; i++)
            segs[i].color = i < Current ? onColor : offColor;
    }

    // 满格：整体从青色渐变成金色常亮
    IEnumerator FlashDone()
    {
        flashing = true;
        float t = 0f;
        while (t < 1f && completed)
        {
            t += Time.deltaTime / flashTime;
            for (int i = 0; i < segs.Length; i++)
                segs[i].color = Color.Lerp(onColor, doneColor, t);
            yield return null;
        }
        if (completed)
            for (int i = 0; i < segs.Length; i++) segs[i].color = doneColor;
        flashing = false;
    }
}