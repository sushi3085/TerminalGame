using TerminalGame.Rpg.Data;

namespace TerminalGame.Rpg.State;

public sealed record LevelUp(PartyMember member, int newLevel, IReadOnlyList<SkillDef> learnedSkills);

/// <summary>A playable character's mutable state. Stats are always derived: base + growth × (level − 1) + equipment.</summary>
public sealed class PartyMember
{
    private readonly ContentDb content;
    private readonly Dictionary<EquipSlot, string> equipped = new();
    private int currentHp;
    private int currentMp;

    public PartyMember(ContentDb content, CharacterDef def, int level = 1)
    {
        this.content = content;
        this.def = def;
        this.level = Math.Clamp(level, 1, Progression.maxLevel);
        foreach (string itemId in def.initialEquipment)
        {
            ItemDef item = content.item(itemId);
            equipped[item.slot!.Value] = itemId;
        }

        restore();
    }

    public CharacterDef def { get; }
    public string name => def.name;
    public int level { get; private set; }

    /// <summary>Experience gathered toward the next level.</summary>
    public int exp { get; private set; }

    public int expToNext => Progression.expToNext(level);

    public int hp
    {
        get => currentHp;
        set => currentHp = Math.Clamp(value, 0, stats.maxHp);
    }

    public int mp
    {
        get => currentMp;
        set => currentMp = Math.Clamp(value, 0, stats.maxMp);
    }

    public bool isAlive => currentHp > 0;

    public StatBlock stats
    {
        get
        {
            StatBlock total = def.baseStats + def.growth * (level - 1);
            foreach (string itemId in equipped.Values)
            {
                total += content.item(itemId).bonus;
            }

            return total.clamped();
        }
    }

    public IReadOnlyDictionary<EquipSlot, string> equipment => equipped;

    public IReadOnlyList<SkillDef> skills =>
        def.skills.Where(s => s.level <= level).Select(s => content.skill(s.skillId)).ToList();

    public ItemDef? equippedIn(EquipSlot slot) =>
        equipped.TryGetValue(slot, out string? id) ? content.item(id) : null;

    /// <summary>Puts <paramref name="item"/> on and returns what was in that slot (null if empty).</summary>
    public ItemDef? equip(ItemDef item)
    {
        if (item.kind != ItemKind.Equipment || item.slot is not EquipSlot slot)
        {
            throw new ArgumentException($"'{item.id}' is not equipment", nameof(item));
        }

        ItemDef? previous = equippedIn(slot);
        equipped[slot] = item.id;
        clampResources();
        return previous;
    }

    public ItemDef? unequip(EquipSlot slot)
    {
        ItemDef? previous = equippedIn(slot);
        equipped.Remove(slot);
        clampResources();
        return previous;
    }

    public void restore()
    {
        StatBlock s = stats;
        currentHp = s.maxHp;
        currentMp = s.maxMp;
    }

    /// <summary>Adds experience, levelling up as many times as it covers. Max HP/MP gains are also added to current HP/MP.</summary>
    public IReadOnlyList<LevelUp> gainExp(int amount)
    {
        List<LevelUp> levelUps = new();
        if (amount <= 0)
        {
            return levelUps;
        }

        exp += amount;
        while (level < Progression.maxLevel && exp >= expToNext)
        {
            exp -= expToNext;
            StatBlock before = stats;
            level++;
            StatBlock after = stats;
            currentHp += after.maxHp - before.maxHp;
            currentMp += after.maxMp - before.maxMp;

            List<SkillDef> learned = def.skills.Where(s => s.level == level).Select(s => content.skill(s.skillId)).ToList();
            levelUps.Add(new LevelUp(this, level, learned));
        }

        if (level >= Progression.maxLevel)
        {
            exp = 0;
        }

        return levelUps;
    }

    private void clampResources()
    {
        hp = currentHp;
        mp = currentMp;
    }
}
