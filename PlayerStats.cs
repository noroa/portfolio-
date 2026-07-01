using UnityEngine;

/// <summary>
/// PLAYER STATS
/// หน้าที่: เก็บค่า HP, MP, Level, XP, Gold และคำนวณ Stats
/// อยู่บน: Player GameObject (เดียวกับ PlayerController)
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    public int   baseHp      = 100;
    public int   baseMp      = 80;
    public int   baseAttack  = 12;
    public int   baseDefense = 5;
    public float mpRegenRate = 4f;     // MP ต่อวินาที

    [Header("Growth Per Level")]
    public int hpPerLevel      = 20;
    public int mpPerLevel      = 10;
    public int attackPerLevel  = 3;
    public int defensePerLevel = 2;

    // ── runtime ───────────────────────────────
    [HideInInspector] public int Level    = 1;
    [HideInInspector] public int Xp       = 0;
    [HideInInspector] public int XpToNext = 100;
    [HideInInspector] public int Gold     = 0;
    [HideInInspector] public int Kills    = 0;

    private int   _currentHp;
    private float _currentMp;
    private int   _bonusAtk;
    private int   _bonusDef;

    // ── computed properties ───────────────────
    public int MaxHp   => baseHp      + (Level - 1) * hpPerLevel;
    public int MaxMp   => baseMp      + (Level - 1) * mpPerLevel;
    public int Attack  => baseAttack  + (Level - 1) * attackPerLevel  + _bonusAtk;
    public int Defense => baseDefense + (Level - 1) * defensePerLevel + _bonusDef;

    public int CurrentHp
    {
        get => _currentHp;
        set
        {
            _currentHp = Mathf.Clamp(value, 0, MaxHp);
            GameEvents.OnStatsChanged?.Invoke();
        }
    }

    public float CurrentMp
    {
        get => _currentMp;
        set
        {
            _currentMp = Mathf.Clamp(value, 0, MaxMp);
            GameEvents.OnStatsChanged?.Invoke();
        }
    }

    void Start()
    {
        _currentHp = MaxHp;
        _currentMp = MaxMp;
    }

    void Update()
    {
        if (_currentMp < MaxMp)
            CurrentMp += mpRegenRate * Time.deltaTime;
    }

    // ── xp & leveling ─────────────────────────
    public void GainXp(int amount)
    {
        Xp += amount;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            LevelUp();
        }
        GameEvents.OnStatsChanged?.Invoke();
    }

    void LevelUp()
    {
        Level++;
        XpToNext   = Mathf.RoundToInt(100 * Mathf.Pow(1.4f, Level - 1));
        _currentHp = MaxHp;
        _currentMp = MaxMp;
        GameEvents.OnLevelUp?.Invoke(Level);
    }

    // ── equipment ─────────────────────────────
    public void AddEquipmentBonus(int atk, int def)
    {
        _bonusAtk += atk;
        _bonusDef += def;
        GameEvents.OnStatsChanged?.Invoke();
    }

    public void RemoveEquipmentBonus(int atk, int def)
    {
        _bonusAtk = Mathf.Max(0, _bonusAtk - atk);
        _bonusDef = Mathf.Max(0, _bonusDef - def);
        GameEvents.OnStatsChanged?.Invoke();
    }

    // ── heal ──────────────────────────────────
    public void Heal(int amount)
    {
        CurrentHp += amount;
        FloatingTextManager.Instance?.ShowText(
            transform.position, $"+{amount}", Color.green);
    }

    public void RestoreMp(int amount)
    {
        CurrentMp += amount;
        FloatingTextManager.Instance?.ShowText(
            transform.position, $"+{amount} MP", Color.cyan);
    }
}
