using UnityEngine;

// ============================================================
// ① TouchInput：触屏输入总线（静态类，不用挂任何物体）
//    08 章的虚拟按钮（TouchButton）写这些字段，本脚本读；
//    键盘输入与它叠加。注意：本类定义在本文件内，
//    工程里不要再单独建 TouchInput.cs，否则报重复定义（CS0101）。
// ============================================================
public static class TouchInput
{
    public static bool left;
    public static bool right;
    public static bool jumpPressed;     // 按一下置 true，读后自动清零
    public static bool wavePressed;
    public static bool interactPressed;
}

// ============================================================
// ② PlayerController2D：横版平台跳跃控制器（v3）
//    v3 修复"平地偶发跳不出"：
//    - 着地检测不再依赖 GroundCheck 子物体（它的实际位置随 Player
//      缩放漂移、摆错就闪烁误判），改为用自身碰撞体向下 Cast，
//      与缩放和子物体摆放完全无关；
//    - 跳高截断加保底速度，短按也是可见小跳而不是 6 像素抽动。
//    教程章节：04。
// ============================================================
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 7f;              // 目标水平速度
    [Tooltip("加速度：越大换向越快。60≈瞬间，20≈带一点惯性")]
    public float accel = 60f;

    [Header("跳跃手感")]
    public float jumpVelocity = 14f;          // 起跳瞬时速度
    public float gravityScale = 4f;           // 上升重力倍率
    public float fallGravityMultiplier = 1.6f;// 下落附加重力：过顶后掉得更干脆
    public float coyoteTime = 0.12f;          // 土狼时间：离地后仍可跳的宽限
    public float jumpBufferTime = 0.12f;      // 跳跃缓冲：落地前按跳，落地瞬间自动起跳
    public float jumpCutMultiplier = 0.5f;    // 上升中松键 → 低跳
    public float jumpCutMinSpeed = 3f;        // 截断后的保底速度：短按也是可见小跳

    [Header("地面检测")]
    public float groundCastDistance = 0.08f;  // 从自身碰撞体向下探测的距离
    public LayerMask groundLayer;             // 只勾 Ground 层！

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    ContactFilter2D groundFilter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[4];
    int lastDir = 1;                          // A+D 同按时保留"最后按下的方向"
    float coyoteTimer;
    float jumpBufferTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;             // 跳跃时身体不打转
        rb.gravityScale = gravityScale;
    }

    void Update()
    {
        // ---- 1. 方向输入：记录最后按下的方向键（A+D 同按不再归零）----
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  lastDir = -1;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) lastDir = 1;

        bool left  = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)  || TouchInput.left;
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || TouchInput.right;

        int dir = 0;
        if (left && right) dir = lastDir;     // 同按：走最后按的那边
        else if (left)     dir = -1;
        else if (right)    dir = 1;

        // ---- 2. 水平速度：向目标速度平滑逼近，accel 决定换向快慢 ----
        float targetX = dir * moveSpeed;
        float vx = Mathf.MoveTowards(rb.velocity.x, targetX, accel * Time.deltaTime);
        rb.velocity = new Vector2(vx, rb.velocity.y);

        // ---- 3. 朝向：往哪走脸朝哪 ----
        if (dir != 0 && sr != null) sr.flipX = dir < 0;

        // ---- 4. 下落附加重力：跳过顶点后下落更干脆 ----
        rb.gravityScale = rb.velocity.y < 0f ? gravityScale * fallGravityMultiplier : gravityScale;

        // ---- 5. 着地检测：自身碰撞体向下 Cast（不依赖任何子物体位置）----
        groundFilter.useLayerMask = true;
        groundFilter.layerMask = groundLayer;
        groundFilter.useTriggers = false;
        bool grounded = rb.velocity.y <= 0.5f                                // 上升中不算着地
                        && col.Cast(Vector2.down, groundFilter, hits, groundCastDistance) > 0;

        // ---- 6. 土狼时间：着地刷新，离地后短暂仍可起跳 ----
        if (grounded) coyoteTimer = coyoteTime;
        else coyoteTimer -= Time.deltaTime;

        // ---- 7. 跳跃缓冲：落地前按的跳，落地瞬间生效 ----
        if (Input.GetButtonDown("Jump") || TouchInput.jumpPressed)
            jumpBufferTimer = jumpBufferTime;
        TouchInput.jumpPressed = false;       // 一次性信号，读完即清
        jumpBufferTimer -= Time.deltaTime;

        // ---- 8. 起跳：缓冲窗口 + 土狼时间 任一有效即可 ----
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        // ---- 9. 可变跳高：上升中松键截断速度，但保底 jumpCutMinSpeed ----
        if (Input.GetButtonUp("Jump") && rb.velocity.y > jumpCutMinSpeed)
            rb.velocity = new Vector2(rb.velocity.x,
                Mathf.Max(rb.velocity.y * jumpCutMultiplier, jumpCutMinSpeed));
    }
}
