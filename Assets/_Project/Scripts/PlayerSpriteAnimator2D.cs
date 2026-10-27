using UnityEngine;

/// <summary>
/// 角色逐帧动画播放器（走路/跑动的小动画）。
///
/// 设计原则：
/// 1. 不依赖 Animator / Animator Controller —— 零基础也能在 Inspector 里看懂、改懂；
/// 2. 不修改 PlayerController2D.cs 一行代码 —— 只“读”它的结果（刚体速度）来决定播不播；
/// 3. 帧序列直接用数组拖拽，想加帧、换顺序、改速度都不用碰代码。
///
/// 放在哪：Assets/_Project/Scripts/PlayerSpriteAnimator2D.cs
/// 挂在谁身上：Player（和 SpriteRenderer 同一个物体）
/// </summary>
[DisallowMultipleComponent]
public class PlayerSpriteAnimator2D : MonoBehaviour
{
    [Header("① 帧序列：按播放顺序把图拖进来（同一张可以拖多次）")]
    public Sprite[] frames;

    [Header("② 播放速度：每秒播放几帧，数字越大动得越快")]
    [Min(0.1f)] public float framesPerSecond = 8f;

    [Header("③ 只在水平移动时播放（站着不动就停在第 1 帧）")]
    public bool playOnlyWhenMoving = true;

    [Header("④ 停下来时回到第 1 帧")]
    public bool resetWhenIdle = true;

    [Header("⑤ 判定“在移动”的速度阈值，一般不用改")]
    [Min(0f)] public float moveThreshold = 0.05f;

    [Header("⑥ 可选：指定要换图的 SpriteRenderer；留空 = 用自己身上的")]
    [SerializeField] SpriteRenderer targetRenderer;

    SpriteRenderer sr;
    Rigidbody2D rb;
    float timer;   // 距离下一帧还差多少时间
    int index;     // 当前播到第几帧

    void Awake()
    {
        // 没手动指定就用自己身上的渲染器
        sr = targetRenderer != null ? targetRenderer : GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (sr == null || frames == null || frames.Length == 0) return;

        // ---- 1. 判断现在是否在移动 ----
        bool moving = true;
        if (playOnlyWhenMoving)
        {
            // 直接读刚体的水平速度：角色被 PlayerController2D 推着走，这里只观察，不干预
            float vx = rb != null ? rb.velocity.x : 0f;
            moving = Mathf.Abs(vx) > moveThreshold;
        }

        // ---- 2. 没在动：回到第 1 帧 ----
        if (!moving)
        {
            if (resetWhenIdle && index != 0)
            {
                index = 0;
                timer = 0f;
                sr.sprite = frames[0];
            }
            return;
        }

        // ---- 3. 在动：按 fps 推进帧 ----
        timer += Time.deltaTime;
        float frameTime = 1f / Mathf.Max(0.1f, framesPerSecond);

        // 用 while 而不是 if：万一某帧卡顿，能一次补上多帧，不会“越播越慢”
        while (timer >= frameTime)
        {
            timer -= frameTime;
            index = (index + 1) % frames.Length;  // 到末尾自动回到开头
            sr.sprite = frames[index];
        }
    }

    /// <summary>
    /// 运行时改速度（比如冲刺、受伤时变快/变慢）。策划想加这种效果时调它即可。
    /// </summary>
    public void SetSpeed(float fps)
    {
        framesPerSecond = Mathf.Max(0.1f, fps);
    }
}
