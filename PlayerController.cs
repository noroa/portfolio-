using UnityEngine;

/// <summary>
/// PLAYER CONTROLLER
/// หน้าที่: รับ Input → เดิน, โจมตี, Dash, เรียก Skill
/// อยู่บน: Player GameObject
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed    = 5f;

    [Header("Dash")]
    public float dashSpeed    = 14f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 1.2f;

    [Header("Combat")]
    public float attackRange    = 1.5f;
    public float attackCooldown = 0.4f;
    public LayerMask enemyLayer;
    public GameObject attackEffectPrefab;

    [Header("Combo")]
    public int   maxCombo    = 3;
    public float comboWindow = 1.2f;

    // ── refs ──────────────────────────────────
    private Rigidbody2D rb;
    private Animator    anim;
    private PlayerStats stats;
    private SkillSystem skills;

    // ── state ─────────────────────────────────
    private Vector2 moveInput;
    private float   attackTimer;
    private float   comboTimer;
    private int     currentCombo;
    private bool    isInvincible;
    private float   invincibleTimer;
    private bool    isDashing;
    private float   dashTimer;
    private float   dashCdTimer;
    private Vector2 dashDir;

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        anim   = GetComponent<Animator>();
        stats  = GetComponent<PlayerStats>();
        skills = GetComponent<SkillSystem>();
    }

    void Update()
    {
        if (!isDashing) ReadMoveInput();
        ReadCombatInput();
        ReadSkillInput();
        ReadDashInput();
        HandleTimers();
        FlipSprite();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        rb.velocity = isDashing
            ? dashDir * dashSpeed
            : moveInput * moveSpeed;
    }

    // ── input ─────────────────────────────────
    void ReadMoveInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")).normalized;
    }

    void ReadCombatInput()
    {
        if (Input.GetMouseButtonDown(0) && attackTimer <= 0 && !isDashing)
            PerformAttack();
    }

    void ReadSkillInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) skills?.UseSkill(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) skills?.UseSkill(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) skills?.UseSkill(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) skills?.UseSkill(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) skills?.UseSkill(4);
        if (Input.GetKeyDown(KeyCode.I))      ToggleInventory();
    }

    void ReadDashInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && dashCdTimer <= 0 && !isDashing)
            StartDash();
    }

    // ── combat ────────────────────────────────
    void PerformAttack()
    {
        attackTimer  = attackCooldown;
        currentCombo = comboTimer > 0
            ? Mathf.Min(currentCombo + 1, maxCombo) : 1;
        comboTimer   = comboWindow;

        float[] mults = { 1f, 1.2f, 1.5f };
        int dmg = Mathf.RoundToInt(stats.Attack * mults[currentCombo - 1]);

        Vector2 atkDir = GetMouseDir();
        Vector2 atkPos = (Vector2)transform.position + atkDir * 0.8f;

        foreach (var h in Physics2D.OverlapCircleAll(atkPos, attackRange, enemyLayer))
            h.GetComponent<EnemyController>()?.TakeDamage(dmg);

        if (attackEffectPrefab != null)
        {
            float angle = Mathf.Atan2(atkDir.y, atkDir.x) * Mathf.Rad2Deg;
            Instantiate(attackEffectPrefab,
                (Vector2)transform.position + atkDir,
                Quaternion.Euler(0, 0, angle));
        }

        if (currentCombo == maxCombo)
            CameraShake.Instance?.Shake(0.2f, 0.3f);

        AudioManager.Instance?.Play(
            AudioManager.Instance?.GetAttackClip(currentCombo));

        anim?.SetTrigger("Attack");
    }

    public void TakeDamage(int damage)
    {
        if (isInvincible) return;

        if (skills != null && skills.IsShieldActive)
        {
            GameEvents.OnMessage?.Invoke("Shield Blocked!", MessageType.System);
            AudioManager.Instance?.Play(AudioManager.Instance?.sfxBlock);
            return;
        }

        int actual = Mathf.Max(1, damage - stats.Defense);
        stats.CurrentHp -= actual;

        FloatingTextManager.Instance?.ShowText(
            transform.position, $"-{actual}", Color.red);
        AudioManager.Instance?.Play(AudioManager.Instance?.sfxHurt);

        isInvincible    = true;
        invincibleTimer = 0.6f;
        CameraShake.Instance?.Shake(0.15f, 0.25f);
        GameEvents.OnPlayerDamaged?.Invoke(actual);
        anim?.SetTrigger("Hurt");

        if (stats.CurrentHp <= 0) Die();
    }

    // ── dash ──────────────────────────────────
    void StartDash()
    {
        isDashing       = true;
        dashTimer       = dashDuration;
        dashCdTimer     = dashCooldown;
        dashDir         = moveInput.magnitude > 0.1f ? moveInput : GetMouseDir();
        isInvincible    = true;
        invincibleTimer = dashDuration + 0.05f;

        anim?.SetTrigger("Dash");
        AudioManager.Instance?.Play(AudioManager.Instance?.sfxDash);
        ParticleManager.Instance?.SpawnBurst(transform.position, Color.cyan, 8);
    }

    // ── timers ────────────────────────────────
    void HandleTimers()
    {
        if (attackTimer > 0)  attackTimer  -= Time.deltaTime;
        if (dashCdTimer > 0)  dashCdTimer  -= Time.deltaTime;

        if (comboTimer > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0) currentCombo = 0;
        }

        if (isInvincible)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0) isInvincible = false;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) { isDashing = false; rb.velocity = Vector2.zero; }
        }
    }

    // ── helpers ───────────────────────────────
    Vector2 GetMouseDir()
    {
        Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return ((Vector2)(mouse - transform.position)).normalized;
    }

    void FlipSprite()
    {
        if (moveInput.x != 0)
            transform.localScale = new Vector3(
                moveInput.x > 0 ? 1 : -1, 1, 1);
    }

    void UpdateAnimator()
    {
        if (anim == null) return;
        anim.SetFloat("Speed",   moveInput.magnitude);
        anim.SetFloat("MoveX",   moveInput.x);
        anim.SetFloat("MoveY",   moveInput.y);
        anim.SetBool ("Dashing", isDashing);
    }

    void Die()
    {
        anim?.SetTrigger("Death");
        GameEvents.OnPlayerDeath?.Invoke();
    }

    void ToggleInventory() => GameEvents.OnToggleInventory?.Invoke();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
