using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Demo;

/// <summary>What every game scene shares: the content, where saves go, and the playthrough in progress.</summary>
public sealed class Game
{
    public const int saveSlot = 1;

    public Game(ContentDb content, SaveStore saves, int? seed = null)
    {
        this.content = content;
        this.saves = saves;
        this.seed = seed;
    }

    public ContentDb content { get; }
    public SaveStore saves { get; }

    /// <summary>Fixes the random sequence of new games (scripted snapshot run and tests).</summary>
    public int? seed { get; }

    /// <summary>The current playthrough; replaced by <see cref="startNew"/> and <see cref="loadSaved"/>.</summary>
    public GameSession session { get; private set; } = null!;

    public bool hasSave => saves.peek(saveSlot) is not null;

    public GameSession startNew() => session = GameSession.newGame(content, seed);

    public GameSession loadSaved() => session = saves.load(content, saveSlot);

    public void save() => saves.save(session, saveSlot);
}
