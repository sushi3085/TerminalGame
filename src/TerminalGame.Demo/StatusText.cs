using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;

namespace TerminalGame.Demo;

/// <summary>Chinese names, tags and narration for statuses.</summary>
public static class StatusText
{
    /// <summary>A short colored tag for status lines, e.g. "[purple]毒[/]".</summary>
    public static string tag(StatusKind kind) => kind switch
    {
        StatusKind.Poison => "[purple]毒[/]",
        StatusKind.Sleep => "[cyan]睡[/]",
        StatusKind.Paralysis => "[gold]麻[/]",
        StatusKind.AttackUp => "[green]攻↑[/]",
        StatusKind.DefenseUp => "[green]防↑[/]",
        StatusKind.AttackDown => "[red]攻↓[/]",
        StatusKind.DefenseDown => "[red]防↓[/]",
        _ => "?",
    };

    public static string tags(IEnumerable<StatusKind> kinds) => string.Concat(kinds.Select(k => " " + tag(k)));

    public static string name(StatusKind kind) => kind switch
    {
        StatusKind.Poison => "中毒",
        StatusKind.Sleep => "睡眠",
        StatusKind.Paralysis => "麻痺",
        StatusKind.AttackUp => "攻擊提升",
        StatusKind.DefenseUp => "防禦提升",
        StatusKind.AttackDown => "攻擊下降",
        StatusKind.DefenseDown => "防禦下降",
        _ => kind.ToString(),
    };

    public static string name(Element element) => element switch
    {
        Element.Fire => "火",
        Element.Ice => "冰",
        Element.Thunder => "雷",
        Element.Holy => "聖",
        _ => "",
    };

    /// <summary>The line after the target's name, e.g. "中毒了！".</summary>
    public static string applied(StatusKind kind) => kind switch
    {
        StatusKind.Poison => "[purple]中毒了[/]！",
        StatusKind.Sleep => "[cyan]睡著了[/]！",
        StatusKind.Paralysis => "[gold]全身麻痺了[/]！",
        StatusKind.AttackUp => "的攻擊力[green]提升了[/]！",
        StatusKind.DefenseUp => "的防禦力[green]提升了[/]！",
        StatusKind.AttackDown => "的攻擊力[red]下降了[/]！",
        StatusKind.DefenseDown => "的防禦力[red]下降了[/]！",
        _ => "",
    };

    public static string removed(StatusKind kind, StatusEndReason reason) => (kind, reason) switch
    {
        (StatusKind.Sleep, StatusEndReason.WokeUp) => "被打醒了！",
        (StatusKind.Sleep, _) => "醒過來了！",
        (StatusKind.Poison, StatusEndReason.Cured) => "的毒解除了！",
        (StatusKind.Poison, _) => "身上的毒消退了。",
        (StatusKind.Paralysis, _) => "的麻痺解除了。",
        (StatusKind.AttackUp or StatusKind.AttackDown, _) => "的攻擊力恢復了原狀。",
        _ => "的防禦力恢復了原狀。",
    };

    public static string skipped(StatusKind cause) => cause == StatusKind.Sleep ? "正在呼呼大睡……" : "全身麻痺，動彈不得！";
}
