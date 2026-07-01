using UnityEngine;
using System.Collections;

/// <summary>
/// ENEMY CONTROLLER
/// หน้าที่: AI ศัตรู — Wander, Chase, Attack + Enrage เมื่อ HP ต่ำ
/// อยู่บน: Enemy Prefab
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    public int   maxHp       = 30;
    public int   attack      = 8;
    public int   defense     = 0;
    public float moveSpeed   = 1.4f;
    public int   xpReward    = 15;
    public int   goldReward  = 5;

    [Header("AI")]
    public float detectionRange = 10f;
    public float attackRange    = 1f;
    public float attackCooldown = 1.2f;
    public float wanderRadius   = 3f;

    [Header("Enrage (HP ต่ำกว่า %)")]
    public float enrageThreshold = 0.3f;
    public float enrageSpeedMult = 1.5f;
    public Color enrageColor     = Color.red;

    [Header("Loot")]
    public GameObject lootDropPrefab;
    [Range(0,1)] public float dropChance = 0.35f;

    // ── AI states ─────────────────────────────
    enum State { Wander, Chase, Attack, Dead }
    private State currentState = State.Wander;

    // ── private ───────────────────────────────
    private Rigidbody2D    rb;
    private Animator       anim;
    private SpriteRenderer sr;
    private Transform      player;

    private int   currentHp;
    private float attackTimer;
    private bool  isEnraged;
    private Vector2 wanderTarget;
    private float   wanderTimer;

    // ── public getter untuk HP Bar ────────────
    public float HpPercent => (float)currentHp / maxHp;

    void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr   = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        currentHp = maxHp;
        player    = GameObject.FindGameObjectWithTag("Player")?.transform;
        SetNewWanderTarget();
    }

    void Update()
    {
        if (currentState == State.Dead || player == null) return;
        CheckEnrage();
        UpdateState();
        HandleAttackTimer();
    }

    void FixedUpdate()
    {
        if (currentState == State.Dead) return;
        MoveByState();
    }

    // ── state machine ─────────────────────────
    void UpdateState()
    {
        float dist = Vector2.Distance(transform.position, player.position);

        if      (dist <= attackRange)    currentState = State.Attack;
        else if (dist <= detectionRange) currentState = State.Chase;
        else                             currentState = State.Wander;
    }

    void MoveByState()
    {
        float spd = moveSpeed * (isEnraged ? enrageSpeedMult : 1f);

        switch (currentState)
        {
            case State.Chase:
                var dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
                rb.velocity = dir * spd;
                FlipToward(player.position);
                break;

            case State.Wander:
                if (Vector2.Distance(transform.position, wanderTarget) < 0.2f)
                {
                    rb.velocity  = Vector2.zero;
                    wanderTimer -= Time.fixedDeltaTime;
                    if (wanderTimer <= 0) SetNewWanderTarget();
                }
                else
                {
                    var wd = (wanderTarget - (Vector2)transform.position).normalized;
                    rb.velocity = wd * (spd * 0.5f);
                    FlipToward(wanderTarget);
                }
                break;

            case State.Attack:
                rb.velocity = Vector2.zero;
                break;
        }

        anim?.SetFloat("Speed", rb.velocity.magnitude);
    }

    void HandleAttackTimer()
    {
        if (attackTimer > 0) { attackTimer -= Time.deltaTime; return; }
        if (currentState != State.Attack) return;

        attackTimer = attackCooldown;
        player.GetComponent<PlayerController>()?.TakeDamage(attack);
    }

    // ── damage & death ────────────────────────
    public void TakeDamage(int damage)
    {
        if (currentState == State.Dead) return;

        int actual = Mathf.Max(1, damage - defense);
        currentHp -= actual;

        FloatingTextManager.Instance?.ShowText(
            transform.position, $"-{actual}", Color.white);

        StartCoroutine(HitFlash());

        if (currentHp <= 0) Die();
    }

    void Die()
    {
        currentState = State.Dead;
        rb.velocity  = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        var ps = player.GetComponent<PlayerStats>();
        if (ps != null)
        {
            ps.GainXp(xpReward);
            ps.Gold  += goldReward + Random.Range(0, goldReward);
            ps.Kills += 1;
            GameEvents.OnStatsChanged?.Invoke();
        }

        if (lootDropPrefab != null && Random.value <= dropChance)
            Instantiate(lootDropPrefab, transform.position, Quaternion.identity);

        GameEvents.OnEnemyDeath?.Invoke(transform.position);
        ParticleManager.Instance?.SpawnBurst(transform.position, sr.color, 15);
        anim?.SetTrigger("Death");
        Destroy(gameObject, 1.5f);
    }

    // ── enrage ────────────────────────────────
    void CheckEnrage()
    {
        if (isEnraged) return;
        if ((float)currentHp / maxHp > enrageThreshold) return;

        isEnraged = true;
        if (sr != null) sr.color = enrageColor;
        GameEvents.OnMessage?.Invoke($"{name} ENRAGED!", MessageType.Warning);
    }

    // ── helpers ───────────────────────────────
    void SetNewWanderTarget()
    {
        wanderTarget = (Vector2)transform.position + Random.insideUnitCircle * wanderRadius;
        wanderTimer  = Random.Range(0.8f, 1.8f);
    }

    void FlipToward(Vector2 target)
    {
        float d = target.x - transform.position.x;
        if (Mathf.Abs(d) > 0.01f)
            transform.localScale = new Vector3(Mathf.Sign(d), 1, 1);
    }

    IEnumerator HitFlash()
    {
        if (sr == null) yield break;
        Color orig = isEnraged ? enrageColor : Color.white;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        sr.color = orig;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
