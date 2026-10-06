using System.Text;
using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;

namespace TerminalGame.Demo;

/// <summary>One message-window page and the engine events whose HP/MP changes should appear with it.</summary>
public sealed record BattlePage(string markup, IReadOnlyList<BattleEvent> events);

/// <summary>Turns engine events into Chinese battle narration. Pure text; knows nothing about widgets.</summary>
public static class BattleNarrator
{
    public static string encounter(IReadOnlyList<Combatant> enemies) =>
        enemies.Count == 1
            ? $"野生的{nameOf(enemies[0])}出現了！"
            : $"{string.Join("、", enemies.Select(nameOf))}出現了！";

    /// <summary>
    /// Groups events into pages: each action (attack, skill, item, guard, escape) and each reward line starts a new
    /// page, and its results (damage, healing, defeats) are appended to it.
    /// </summary>
    public static IReadOnlyList<BattlePage> narrate(IEnumerable<BattleEvent> events)
    {
        List<BattlePage> pages = new();
        StringBuilder text = new();
        List<BattleEvent> pageEvents = new();

        void flush()
        {
            if (text.Length > 0 || pageEvents.Count > 0)
            {
                pages.Add(new BattlePage(text.ToString().TrimEnd('\n'), pageEvents.ToList()));
            }

            text.Clear();
            pageEvents.Clear();
        }

        foreach (BattleEvent e in events)
        {
            string? opening = e switch
            {
                AttackEvent a => nameOf(a.actor) + orDefault(attackMessage(a.actor), "發動攻擊！"),
                SkillUsedEvent s => nameOf(s.actor) + orDefault(s.skill.useMessage, $"使出了[magenta]{s.skill.name}[/]！"),
                ItemUsedEvent i => $"{nameOf(i.actor)}使用了[green]{i.item.name}[/]。",
                GuardEvent g => $"{nameOf(g.actor)}擺出了防禦姿勢。",
                TurnSkippedEvent t => nameOf(t.actor) + StatusText.skipped(t.cause),
                PhaseChangedEvent p => orDefault(p.previous.def.phaseMessage, $"{nameOf(p.next)}現出了真正的姿態！"),
                PoisonDamageEvent p => $"{nameOf(p.target)}受到毒的侵蝕，損失了 [purple]{p.amount}[/] 點 HP。",
                EscapeEvent { succeeded: true } => "順利逃走了！",
                EscapeEvent => "沒能逃掉！",
                VictoryEvent v => victory(v),
                LevelUpEvent l => levelUp(l),
                PartyDefeatedEvent => "全員倒下了……",
                _ => null,
            };

            string? result = e switch
            {
                DamageEvent d when d.target.side == Side.Enemies =>
                    (d.isCritical ? "[gold b]會心一擊！[/]" : "") + effectiveness(d) + $"對{nameOf(d.target)}造成 [gold b]{d.amount}[/] 點傷害！",
                DamageEvent d =>
                    (d.isCritical ? "[red b]痛恨一擊！[/]" : "") + effectiveness(d) + $"{nameOf(d.target)}受到 [red b]{d.amount}[/] 點傷害。",
                StatusAppliedEvent s => nameOf(s.target) + StatusText.applied(s.kind),
                StatusMissedEvent s => $"對{nameOf(s.target)}沒有效果。",
                StatusRemovedEvent s => nameOf(s.target) + StatusText.removed(s.kind, s.reason),
                HealEvent h => $"{nameOf(h.target)}恢復了 [gold]{h.amount}[/] 點 HP。",
                MpRestoreEvent m => $"{nameOf(m.target)}恢復了 [gold]{m.amount}[/] 點 MP。",
                ReviveEvent r => $"{nameOf(r.target)}[gold]復活了[/]！",
                DefeatedEvent { target.side: Side.Enemies } d => $"{nameOf(d.target)}倒下了！",
                DefeatedEvent d => $"{nameOf(d.target)}倒下了……",
                _ => null,
            };

            if (opening is not null)
            {
                flush();
                text.Append(opening).Append('\n');
            }
            else if (result is not null)
            {
                text.Append(result).Append('\n');
            }

            pageEvents.Add(e);
        }

        flush();
        return pages;
    }

    public static string nameOf(Combatant c) => c.side == Side.Enemies ? $"[red]{c.name}[/]" : c.name;

    private static string? attackMessage(Combatant c) => c switch
    {
        PartyCombatant p => p.member.def.attackMessage,
        EnemyCombatant e => e.def.attackMessage,
        _ => null,
    };

    private static string effectiveness(DamageEvent d) => d.effectiveness switch
    {
        Effectiveness.Weak => "[gold b]效果拔群！[/]",
        Effectiveness.Resisted => "[dim]效果不太好……[/]",
        _ => "",
    };

    private static string orDefault(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string victory(VictoryEvent v)
    {
        StringBuilder text = new("戰鬥勝利！\n");
        text.Append($"獲得 [gold]{v.exp}[/] 點經驗值、[gold]{v.gold}[/] 枚金幣。");
        foreach (ItemDef item in v.drops)
        {
            text.Append($"\n得到了[green]{item.name}[/]！");
        }

        return text.ToString();
    }

    private static string levelUp(LevelUpEvent e)
    {
        StringBuilder text = new($"[gold b]{e.levelUp.member.name}升到了 Lv.{e.levelUp.newLevel}！[/]");
        foreach (SkillDef skill in e.levelUp.learnedSkills)
        {
            text.Append($"\n學會了[magenta]{skill.name}[/]！");
        }

        return text.ToString();
    }
}
