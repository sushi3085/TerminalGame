namespace TerminalGame.Demo;

public sealed class PartyMember
{
    public PartyMember(string name, int level, int maxHp, int maxMp)
    {
        this.name = name;
        this.level = level;
        this.maxHp = maxHp;
        this.maxMp = maxMp;
        hp = maxHp;
        mp = maxMp;
    }

    public string name { get; }
    public int level { get; }
    public int maxHp { get; }
    public int maxMp { get; }
    public int hp { get; set; }
    public int mp { get; set; }

    public void restore()
    {
        hp = maxHp;
        mp = maxMp;
    }
}

public sealed class GameState
{
    public GameState(int? seed = null)
    {
        random = seed is null ? new Random() : new Random(seed.Value);
    }

    public Random random { get; }
    public PartyMember hero { get; } = new("勇者 艾倫", 3, 60, 20);
    public int potions { get; set; } = 3;
    public int experience { get; set; }
}
