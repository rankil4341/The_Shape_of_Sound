using System.Collections;
using UnityEngine;

// ============================================================
// ① TouchInput：触屏输入总线（不挂物体；虚拟按钮写、本脚本读）
// ============================================================
public static class TouchInput
{
    public static bool left;
    public static bool right;
    public static bool jumpPressed;
    public static bool wavePressed;
    public static bool interactPressed;
    public static bool dashPressed;
}

// ============================================================
// ② PlayerController2D v6：蔚蓝式手感
//    v6 相比 v5 新增（来自参考 PlayerController 的 4 件套）：
//    - 跳跃水平冲量（移动中起跳带惯性）
//    - 冲刺输入缓冲（按早 0.08s 不吞键）
//    - 超级跳：地面冲刺后 0.15s 内起跳 = 大水平速度
//    - 蹬墙锁定仅在按住方向时生效
//    v5 已有：VarJump 窗口、下落上限、加/减速分离、冲刺动量保留、
//    冲刺填充延迟、蹭墙下滑+蹬墙跳、顿帧、残影、能力解锁。
// ============================================================
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("能力解锁（默认锁定，道具或 F1/F2 解锁）")]
    public bool canDoubleJump = false;
    public bool canDash = false;

    [Header("移动")]
    public float moveSpeed = 8f;
    public float accel = 220f;
    public float airAccel = 150f;
    public float decel = 130f;
    public float airDecel = 50f;

    [Header("跳跃（VarJump 窗口式）")]
    public float jumpVelocity = 14f;
    public float airJumpVelocity = 12.5f;
    public float varJumpTime = 0.2f;
    public float jumpHBoost = 4f;            // v6: 移动中起跳附赠的水平冲量
    public float jumpCutMultiplier = 0.4f;
    public float jumpCutMinSpeed = 3f;
    public float gravityScale = 4.2f;
    public float fallGravityMultiplier = 1.7f;
    public float apexGravityMultiplier = 0.55f;
    public float apexThreshold = 3f;
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.1f;

    [Header("超级跳（地面冲刺接跳跃）")]
    public float superJumpWindow = 0.15f;    // v6: 地面冲刺结束后这窗口内起跳=超级跳
    public float superJumpH = 18f;           // v6: 超级跳水平速度

    [Header("下落速度上限")]
    public float maxFall = -16f;
    public float fastMaxFall = -24f;

    [Header("冲刺")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashSpeed = 24f;
    public float dashDuration = 0.15f;
    public float dashEndSpeed = 13f;
    public float endDashUpMult = 0.75f;
    public float dashCooldown = 0.2f;
    public float dashRefillDelay = 0.1f;
    public float dashUpBias = 0.8f;
    public float dashBufferTime = 0.08f;     // v6: 冲刺输入缓冲
    public bool dashHitstop = true;
    public float hitstopTime = 0.03f;

    [Header("墙壁")]
    public bool enableWallSlide = true;
    public bool enableWallJump = true;
    public float wallCheckDist = 0.3f;
    public float wallSlideMaxFall = -3f;
    public float wallJumpHSpeed = 12f;
    public float wallJumpForceTime = 0.15f;

    [Header("地面检测")]
    public float groundCastDistance = 0.08f;
    public LayerMask groundLayer;

    [Header("冲刺残影")]
    public bool dashTrail = true;
    public float trailInterval = 0.03f;
    public float trailFadeSpeed = 4f;

    [Header("临时测试键（正式版删除 Update 里对应两行）")]
    public KeyCode testUnlockJumpKey = KeyCode.F1;
    public KeyCode testUnlockDashKey = KeyCode.F2;

    // ---- 内部状态 ----
    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    ContactFilter2D groundFilter;
    readonly RaycastHit2D[] hits = new RaycastHit2D[4];

    int lastDir = 1;
    float coyoteTimer, jumpBufferTimer;
    int airJumpsUsed;

    float varJumpTimer;
    float currentJumpSpeed;
    float wallForceTimer;
    float groundDashJumpTimer;      // v6: >0 = 刚结束一次地面冲刺（超级跳窗口）
    int wallDir;

    bool isDashing;
    Vector2 dashDir;
    float dashTimer, dashCdTimer, dashRefillTimer, dashBufferTimer;
    bool dashAvailable;
    bool dashStartedOnGround;

    public void UnlockDoubleJump() { canDoubleJump = true;  Debug.Log("[能力] 解锁二段跳！"); }
    public void UnlockDash()       { canDash = true;        Debug.Log("[能力] 解锁冲刺！"); }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        dashAvailable = true;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ---- 0. 临时测试键 ----
        if (Input.GetKeyDown(testUnlockJumpKey)) UnlockDoubleJump();
        if (Input.GetKeyDown(testUnlockDashKey)) UnlockDash();

        jumpBufferTimer -= dt;
        dashCdTimer -= dt;
        dashRefillTimer -= dt;
        dashBufferTimer -= dt;          // v6: 冲刺缓冲计时
        wallForceTimer -= dt;
        groundDashJumpTimer -= dt;

        // ---- 1. 方向输入（A+D 同按取后按方向）----
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  lastDir = -1;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) lastDir = 1;
        bool left  = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)  || TouchInput.left;
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || TouchInput.right;
        int dir = left && right ? lastDir : left ? -1 : right ? 1 : 0;
        float vRaw = Input.GetAxisRaw("Vertical");
        bool jumpHeld = Input.GetButton("Jump");

        // ---- 2. 冲刺输入：进缓冲（v6），不直接判定 ----
        if (Input.GetKeyDown(dashKey) || TouchInput.dashPressed)
            dashBufferTimer = dashBufferTime;
        TouchInput.dashPressed = false;

        // ---- 3. 检测：地面 / 墙壁 ----
        groundFilter.useLayerMask = true;
        groundFilter.layerMask = groundLayer;
        groundFilter.useTriggers = false;
        bool grounded = rb.velocity.y <= 0.5f
                        && col.Cast(Vector2.down, groundFilter, hits, groundCastDistance) > 0;
        wallDir = 0;
        if (!grounded && (enableWallSlide || enableWallJump))
        {
            if (col.Cast(Vector2.right, groundFilter, hits, wallCheckDist) > 0)      wallDir = 1;
            else if (col.Cast(Vector2.left, groundFilter, hits, wallCheckDist) > 0)  wallDir = -1;
        }

        // ---- 4. 落地：刷新土狼/二段跳；冲刺延迟后填充 ----
        if (grounded && !isDashing)
        {
            coyoteTimer = coyoteTime;
            airJumpsUsed = 0;
            if (!dashAvailable && dashRefillTimer <= 0f) dashAvailable = true;
        }
        else coyoteTimer -= dt;

        // ---- 5. 冲刺：用缓冲值判定（按早了会在可冲瞬间自动触发）----
        if (dashBufferTimer > 0f && canDash && dashAvailable && !isDashing && dashCdTimer <= 0f)
        {
            Vector2 d = new Vector2(dir, vRaw);
            if (d.sqrMagnitude < 0.01f)
                d = new Vector2(sr != null && sr.flipX ? -1f : 1f, 0f);
            d.Normalize();
            if (Mathf.Abs(d.x) < 0.01f && d.y > 0f) d.y = dashUpBias;
            StartDash(d);
        }

        if (isDashing)
        {
            dashTimer -= dt;
            rb.velocity = dashDir * dashSpeed;
            if (dashTimer <= 0f) EndDash();
        }
        else
        {
            // ---- 6. VarJump：按住维持起跳速度，松手狠截 ----
            if (varJumpTimer > 0f)
            {
                if (jumpHeld)
                {
                    rb.velocity = new Vector2(rb.velocity.x, currentJumpSpeed);
                    varJumpTimer -= dt;
                }
                else
                {
                    varJumpTimer = 0f;
                    if (rb.velocity.y > jumpCutMinSpeed)
                        rb.velocity = new Vector2(rb.velocity.x,
                            Mathf.Max(rb.velocity.y * jumpCutMultiplier, jumpCutMinSpeed));
                }
            }

            // ---- 7. 水平移动（蹬墙锁定期不吃输入；加/减速分离）----
            if (wallForceTimer <= 0f)
            {
                float target = dir * moveSpeed;
                float rate = dir != 0f ? (grounded ? accel : airAccel)
                                       : (grounded ? decel : airDecel);
                float vx = Mathf.MoveTowards(rb.velocity.x, target, rate * dt);
                rb.velocity = new Vector2(vx, rb.velocity.y);
            }

            // ---- 8. 蹭墙下滑 ----
            if (enableWallSlide && wallDir != 0 && rb.velocity.y < 0f
                && ((wallDir == 1 && right) || (wallDir == -1 && left)))
            {
                if (rb.velocity.y < wallSlideMaxFall)
                    rb.velocity = new Vector2(rb.velocity.x, wallSlideMaxFall);
            }

            // ---- 9. 重力：顶点悬浮 > 下落加重 > 常规 ----
            float g = gravityScale;
            if (Mathf.Abs(rb.velocity.y) < apexThreshold) g *= apexGravityMultiplier;
            else if (rb.velocity.y < 0f)                   g *= fallGravityMultiplier;
            rb.gravityScale = g;

            // ---- 10. 下落上限（按S快速下坠）----
            float floorV = vRaw < -0.5f ? fastMaxFall : maxFall;
            if (rb.velocity.y < floorV)
                rb.velocity = new Vector2(rb.velocity.x, floorV);
        }

        // ---- 11. 跳跃缓冲 ----
        if (Input.GetButtonDown("Jump") || TouchInput.jumpPressed)
            jumpBufferTimer = jumpBufferTime;
        TouchInput.jumpPressed = false;

        // ---- 12. 起跳优先级：地面(土狼/超级跳) > 蹬墙 > 二段 ----
        if (jumpBufferTimer > 0f && !isDashing)
        {
            if (coyoteTimer > 0f)
            {
                if (groundDashJumpTimer > 0f) DoSuperJump(dir != 0 ? dir : (sr != null && sr.flipX ? -1 : 1));
                else DoJump(jumpVelocity, dir);
            }
            else if (enableWallJump && wallDir != 0)
                DoWallJump(wallDir, dir);
            else if (canDoubleJump && airJumpsUsed < 1)
            {
                airJumpsUsed++;
                DoJump(airJumpVelocity, 0);   // 二段跳不吃水平冲量，防止无限加速
            }
        }

        // ---- 13. 朝向 ----
        if (dir != 0 && sr != null) sr.flipX = dir < 0;
    }

    // 地面普通跳：带水平冲量（v6）
    void DoJump(float v, int dirAtJump)
    {
        rb.velocity = new Vector2(rb.velocity.x + jumpHBoost * dirAtJump, v);
        currentJumpSpeed = v;
        varJumpTimer = varJumpTime;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        groundDashJumpTimer = 0f;
    }

    // 超级跳：地面冲刺后马上起跳，水平速度远超跑速（v6）
    void DoSuperJump(int dirAtJump)
    {
        rb.velocity = new Vector2(superJumpH * dirAtJump, jumpVelocity);
        currentJumpSpeed = jumpVelocity;
        varJumpTimer = varJumpTime;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        groundDashJumpTimer = 0f;
        Debug.Log("[超级跳]");
    }

    // 蹬墙跳：只在按住方向时锁定水平（v6 修正）
    void DoWallJump(int wallDirSide, int dirAtJump)
    {
        rb.velocity = new Vector2(-wallDirSide * wallJumpHSpeed, jumpVelocity);
        currentJumpSpeed = jumpVelocity;
        varJumpTimer = varJumpTime;
        wallForceTimer = dirAtJump != 0 ? wallJumpForceTime : 0f;   // 没按方向就不锁
        airJumpsUsed = 0;
        dashAvailable = true;
        jumpBufferTimer = 0f;
        if (sr != null) sr.flipX = wallDirSide == 1;
    }

    void StartDash(Vector2 d)
    {
        isDashing = true;
        dashDir = d;
        dashTimer = dashDuration;
        dashCdTimer = dashDuration + dashCooldown;
        dashAvailable = false;
        dashRefillTimer = dashRefillDelay;
        dashBufferTimer = 0f;               // 消费掉缓冲
        dashStartedOnGround = coyoteTimer > 0f;   // 记录：地面起冲才能接超级跳
        airJumpsUsed = 0;
        varJumpTimer = 0f;
        rb.gravityScale = 0f;
        if (dashHitstop) StartCoroutine(Hitstop());
        if (dashTrail) StartCoroutine(TrailRoutine());
    }

    void EndDash()
    {
        isDashing = false;
        Vector2 end = dashDir * dashEndSpeed;
        if (dashDir.y > 0.5f) end.y *= endDashUpMult;
        rb.velocity = end;
        if (dashStartedOnGround) groundDashJumpTimer = superJumpWindow;  // 开超级跳窗口
    }

    IEnumerator Hitstop()
    {
        if (Time.timeScale != 1f) yield break;
        Time.timeScale = 0.02f;
        yield return new WaitForSecondsRealtime(hitstopTime);
        if (Time.timeScale < 1f) Time.timeScale = 1f;
    }

    IEnumerator TrailRoutine()
    {
        while (isDashing)
        {
            if (sr != null && sr.sprite != null)
            {
                var ghost = new GameObject("DashGhost");
                var gr = ghost.AddComponent<SpriteRenderer>();
                gr.sprite = sr.sprite;
                gr.flipX = sr.flipX;
                Color c = sr.color; c.a = 0.5f; gr.color = c;
                gr.sortingOrder = sr.sortingOrder - 1;
                ghost.transform.SetPositionAndRotation(transform.position, transform.rotation);
                ghost.transform.localScale = transform.localScale;
                StartCoroutine(FadeGhost(gr));
            }
            yield return new WaitForSeconds(trailInterval);
        }
    }

    IEnumerator FadeGhost(SpriteRenderer r)
    {
        Color c = r.color;
        while (c.a > 0f)
        {
            c.a -= Time.deltaTime * trailFadeSpeed;
            r.color = c;
            yield return null;
        }
        Destroy(r.gameObject);
    }
}