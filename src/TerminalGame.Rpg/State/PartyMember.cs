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

    public StatBlock stats => computeStats(equipped);

    /// <summary>What <see cref="stats"/> would be with <paramref name="item"/> in <paramref name="slot"/> (null = empty).</summary>
    public StatBlock previewStats(EquipSlot slot, ItemDef? item)
    {
        Dictionary<EquipSlot, string> trial = new(equipped);
        trial.Remove(slot);
        if (item is not null)
        {
            trial[slot] = item.id;
        }

        return computeStats(trial);
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

        if (!item.canBeEquippedBy(def))
        {
            throw new ArgumentException($"{def.id} cannot equip '{item.id}'", nameof(item));
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

    /// <summary>Overwrites progress with saved values. Unknown, misplaced or no longer allowed equipment ids are dropped.</summary>
    internal void load(int savedLevel, int savedExp, IReadOnlyDictionary<EquipSlot, string> savedEquipment, int savedHp, int savedMp)
    {
        level = Math.Clamp(savedLevel, 1, Progression.maxLevel);
        exp = Math.Max(0, savedExp);
        equipped.Clear();
        foreach ((EquipSlot slot, string itemId) in savedEquipment)
        {
            if (content.hasItem(itemId) && content.item(itemId).slot == slot && content.item(itemId).canBeEquippedBy(def))
            {
                equipped[slot] = itemId;
            }
        }

        currentHp = 0;
        currentMp = 0;
        hp = savedHp;
        mp = savedMp;
    }

    private StatBlock computeStats(IReadOnlyDictionary<EquipSlot, string> gear)
    {
        StatBlock total = def.baseStats + def.growth * (level - 1);
        foreach (string itemId in gear.Values)
        {
            total += content.item(itemId).bonus;
        }

        return total.clamped();
    }

    private void clampResources()
    {
        hp = currentHp;
        mp = currentMp;
    }
}
