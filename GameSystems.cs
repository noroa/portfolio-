using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// ══════════════════════════════════════════════════════════
// GAME EVENTS
// หน้าที่: เป็นกล่องรับส่งข่าวกลาง ทุก Script คุยกันผ่านตรงนี้
// วิธีใช้: GameEvents.OnLevelUp?.Invoke(level);
// ══════════════════════════════════════════════════════════
public enum MessageType { Default, Combat, Heal, Reward, System, Warning }

public static class GameEvents
{
    public static Action<int>                 OnLevelUp;
    public static Action                      OnStatsChanged;
    public static Action<int>                 OnPlayerDamaged;
    public static Action                      OnPlayerDeath;
    public static Action<Vector3>             OnEnemyDeath;
    public static Action<int>                 OnWaveStart;
    public static Action<int>                 OnWaveComplete;
    public static Action                      OnInventoryChanged;
    public static Action                      OnToggleInventory;
    public static Action<int>                 OnSkillUsed;
    public static Action<int>                 OnComboMax;
    public static Action<string, MessageType> OnMessage;
}

// ══════════════════════════════════════════════════════════
// PROJECTILE
// หน้าที่: กระสุนที่ SkillSystem ยิงออกมา
// อยู่บน: Projectile Prefab
// ══════════════════════════════════════════════════════════
public class Projectile : MonoBehaviour
{
    private Vector2 direction;
    private float   speed;
    private int     damage;
    private float   splashRadius;
    private float   lifetime = 2f;
    private SpriteRenderer sr;
    private TrailRenderer  trail;

    /// <summary>เรียกทันทีหลัง Instantiate — SkillSystem เรียกให้อัตโนมัติ</summary>
    public void Init(Vector2 dir, float spd, int dmg, Color color,
                     float splashRadius = 0)
    {
        direction         = dir.normalized;
        speed             = spd;
        damage            = dmg;
        this.splashRadius = splashRadius;

        sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;

        trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.startColor = color;
            trail.endColor   = new Color(color.r, color.g, color.b, 0f);
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<EnemyController>();
        if (enemy == null) return;

        enemy.TakeDamage(damage);

        if (splashRadius > 0)
        {
            foreach (var c in Physics2D.OverlapCircleAll(transform.position, splashRadius))
            {
                var e2 = c.GetComponent<EnemyController>();
                if (e2 != null && e2 != enemy) e2.TakeDamage(damage / 2);
            }
            ParticleManager.Instance?.SpawnBurst(transform.position, Color.red, 20);
        }
        else
        {
            ParticleManager.Instance?.SpawnBurst(transform.position, Color.white, 8);
        }

        Destroy(gameObject);
    }
}

// ══════════════════════════════════════════════════════════
// PARTICLE MANAGER
// หน้าที่: Spawn VFX แบบ Object Pool (ไม่กิน GC)
// อยู่บน: Empty GameObject ชื่อ "ParticleManager"
// ══════════════════════════════════════════════════════════
public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance;

    [Header("Prefabs")]
    public ParticleSystem burstPrefab;

    [Header("Pool Size")]
    public int poolSize = 20;

    private Queue<ParticleSystem> pool = new Queue<ParticleSystem>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        for (int i = 0; i < poolSize; i++)
        {
            if (burstPrefab == null) break;
            var ps = Instantiate(burstPrefab, transform);
            ps.gameObject.SetActive(false);
            pool.Enqueue(ps);
        }
    }

    /// <summary>เรียกจากทุกที่: ParticleManager.Instance?.SpawnBurst(pos, Color.red, 15);</summary>
    public void SpawnBurst(Vector3 pos, Color color, int count = 10)
    {
        var ps = pool.Count > 0
            ? pool.Dequeue()
            : (burstPrefab ? Instantiate(burstPrefab, transform) : null);

        if (ps == null) return;

        ps.transform.position = pos;
        ps.gameObject.SetActive(true);

        var main = ps.main;
        main.startColor   = color;
        main.maxParticles = count;
        ps.Play();

        StartCoroutine(ReturnToPool(ps, main.duration + main.startLifetime.constantMax));
    }

    IEnumerator ReturnToPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }
}

// ══════════════════════════════════════════════════════════
// CAMERA SHAKE
// หน้าที่: สั่นกล้องเมื่อโดนโจมตีหรือ Combo Max
// อยู่บน: Main Camera
// ══════════════════════════════════════════════════════════
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;
    void Awake() { if (Instance == null) Instance = this; else Destroy(gameObject); }

    /// <summary>เรียก: CameraShake.Instance?.Shake(0.2f, 0.3f);</summary>
    public void Shake(float duration, float magnitude)
    {
        StartCoroutine(DoShake(duration, magnitude));
    }

    IEnumerator DoShake(float duration, float magnitude)
    {
        Vector3 origin  = transform.localPosition;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            transform.localPosition = origin + new Vector3(
                UnityEngine.Random.Range(-1f, 1f) * magnitude,
                UnityEngine.Random.Range(-1f, 1f) * magnitude, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = origin;
    }
}

// ══════════════════════════════════════════════════════════
// FLOATING TEXT MANAGER
// หน้าที่: แสดงตัวเลข -15, +40 HP ลอยขึ้นเหนือหัว
// อยู่บน: Empty GameObject ชื่อ "FloatingTextManager"
// ══════════════════════════════════════════════════════════
public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance;
    public GameObject floatingTextPrefab;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>เรียก: FloatingTextManager.Instance?.ShowText(pos, "-15", Color.red);</summary>
    public void ShowText(Vector3 pos, string text, Color color)
    {
        if (floatingTextPrefab == null) return;
        var go = Instantiate(floatingTextPrefab,
            pos + Vector3.up * 0.5f, Quaternion.identity);
        go.GetComponent<FloatingText>()?.Init(text, color);
    }
}

// ── FloatingText — แนบกับ FloatingText Prefab ────────────
public class FloatingText : MonoBehaviour
{
    public float riseSpeed = 1.5f;
    public float lifetime  = 1.0f;

    private TextMeshPro tmp;
    private float       timer;

    public void Init(string text, Color color)
    {
        tmp = GetComponent<TextMeshPro>();
        if (tmp != null) { tmp.text = text; tmp.color = color; }
        timer = lifetime;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;
        timer -= Time.deltaTime;

        if (tmp != null)
        {
            Color c = tmp.color;
            c.a       = Mathf.Clamp01(timer / lifetime);
            tmp.color = c;
        }
    }
}
