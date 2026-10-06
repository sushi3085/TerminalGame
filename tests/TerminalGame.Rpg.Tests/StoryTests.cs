using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.Script;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;

namespace TerminalGame.Rpg.Tests;

/// <summary>Journal, banter and the branching script commands used for NPC rumors.</summary>
public class StoryTests
{
    private const string journal = """
        {
          "objectives": [
            { "text": "Talk to the elder." },
            { "if": "flag:metElder", "text": "Explore the woods." },
            { "if": "flag:caveOpen", "text": "Enter the cave." }
          ],
          "chronicle": [
            { "if": "flag:metElder", "text": "Met the elder." },
            { "if": "flag:caveOpen", "text": "The cave opened." },
            { "if": "flag:metElder", "text": "Bought a map." }
          ]
        }
        """;

    private const string banter = """
        [
          { "id": "first", "at": [ "town" ], "script": "chat" },
          { "id": "second", "if": "flag:metElder", "script": "chat" },
          { "id": "anywhere", "once": false, "script": "chat" },
          { "id": "caveOnly", "once": false, "at": [ "cave" ], "script": "chat" }
        ]
        """;

    private static GameSession session(int seed = 1)
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.journalFile] = journal;
        files[ContentDb.banterFile] = banter;
        return GameSession.newGame(ContentDb.parse(files), seed);
    }

    private static string sayOf(ScriptRequest? request) => Assert.IsType<SayRequest>(request).text;

    [Fact]
    public void casesRunTheFirstBranchThatHolds()
    {
        GameSession s = session();
        Assert.Equal("Nothing new.", sayOf(ScriptRunner.forScript(s, "gossip").start()));

        s.flags["metElder"] = 1;
        Assert.Equal("You met the elder.", sayOf(ScriptRunner.forScript(s, "gossip").start()));

        s.flags["caveOpen"] = 1;
        Assert.Equal("The cave is open.", sayOf(ScriptRunner.forScript(s, "gossip").start()));
    }

    [Fact]
    public void oneOfPicksRandomlyAmongBranchesThatHold()
    {
        GameSession s = session();
        HashSet<string> seen = new();
        for (int i = 0; i < 100; i++)
        {
            seen.Add(sayOf(ScriptRunner.forScript(s, "rumor").start()));
        }

        Assert.Equal(new HashSet<string> { "A", "B" }, seen);

        s.flags["secret"] = 1;
        for (int i = 0; i < 100 && !seen.Contains("Secret"); i++)
        {
            seen.Add(sayOf(ScriptRunner.forScript(s, "rumor").start()));
        }

        Assert.Contains("Secret", seen);
    }

    [Fact]
    public void journalFollowsTheFlags()
    {
        GameSession s = session();
        Assert.Equal("Talk to the elder.", s.currentObjective());
        Assert.Empty(s.chronicle());

        s.flags["metElder"] = 1;
        Assert.Equal("Explore the woods.", s.currentObjective());
        Assert.Equal(new[] { "Met the elder.", "Bought a map." }, s.chronicle());

        s.flags["caveOpen"] = 1;
        Assert.Equal("Enter the cave.", s.currentObjective());
        Assert.Equal(new[] { "Met the elder.", "The cave opened.", "Bought a map." }, s.chronicle());
    }

    [Fact]
    public void journalSurvivesSaveAndLoadBecauseItIsOnlyFlags()
    {
        GameSession s = session();
        s.flags["metElder"] = 1;

        GameSession loaded = SaveSystem.restore(s.content, SaveSystem.deserialize(SaveSystem.serialize(SaveSystem.capture(s))));

        Assert.Equal(s.currentObjective(), loaded.currentObjective());
        Assert.Equal(s.chronicle(), loaded.chronicle());
    }

    [Fact]
    public void banterNeedsACompanion()
    {
        GameSession s = session();
        LocationDef town = s.content.location("town");

        Assert.Empty(s.availableBanters(town));
        Assert.Null(s.takeBanter(town));

        s.addMember("slowpoke");
        Assert.NotEmpty(s.availableBanters(town));
    }

    [Fact]
    public void oneOffBantersComeFirstInOrderThenRepeatablesFillIn()
    {
        GameSession s = session();
        s.addMember("slowpoke");
        LocationDef town = s.content.location("town");
        LocationDef woods = s.content.location("woods");

        Assert.False(s.hasNewBanter(woods)); // "first" is only in town; "second" needs the elder
        Assert.Equal("anywhere", s.takeBanter(woods)!.id);

        Assert.True(s.hasNewBanter(town));
        Assert.Equal("first", s.takeBanter(town)!.id);
        Assert.Equal(1, s.flags["banter_first"]);
        Assert.False(s.hasNewBanter(town));
        Assert.Equal("anywhere", s.takeBanter(town)!.id);

        s.flags["metElder"] = 1;
        Assert.True(s.hasNewBanter(woods));
        Assert.Equal("second", s.takeBanter(woods)!.id);
        Assert.Equal("anywhere", s.takeBanter(woods)!.id);
    }

    [Fact]
    public void repeatableBantersArePickedAtRandom()
    {
        GameSession s = session();
        s.addMember("slowpoke");
        LocationDef cave = s.content.location("cave");
        HashSet<string> seen = new();
        for (int i = 0; i < 100; i++)
        {
            seen.Add(s.takeBanter(cave)!.id);
        }

        Assert.Equal(new HashSet<string> { "caveOnly", "anywhere" }, seen); // no one-off applies in the cave yet
        Assert.False(s.flags.isSet("banter_caveOnly")); // repeatables are never marked as seen
    }

    [Fact]
    public void brokenStoryReferencesAreReported()
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.journalFile] = """{ "objectives": [ { "if": "flag:x ==", "text": "?" } ] }""";
        files[ContentDb.banterFile] = """
            [
              { "id": "a", "script": "nope" },
              { "id": "a", "at": [ "moon" ], "script": "chat", "if": "item:nothing" }
            ]
            """;
        files[ContentDb.scriptsFile] = """{ "chat": [ { "cases": [ { "if": "party:ghost", "then": [ { "giveItem": "air" } ] } ] } ] }""";

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(files));

        Assert.Contains(ex.errors, e => e.StartsWith(ContentDb.journalFile));
        Assert.Contains(ex.errors, e => e.Contains("banter 'a': unknown script 'nope'"));
        Assert.Contains(ex.errors, e => e.Contains("duplicate banter id 'a'"));
        Assert.Contains(ex.errors, e => e.Contains("banter 'a': unknown location 'moon'"));
        Assert.Contains(ex.errors, e => e.Contains("banter 'a': unknown item 'nothing'"));
        Assert.Contains(ex.errors, e => e.Contains("script 'chat': unknown character 'ghost'"));
        Assert.Contains(ex.errors, e => e.Contains("script 'chat': unknown item 'air'"));
    }
}
