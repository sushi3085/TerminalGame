# TerminalGame

用 C# 寫文字 JRPG 的終端機 UI 框架（`TerminalGame.Tui`），以及用它製作中的 JRPG《光之試煉》——目前第 1 章「低語森林」可以完整遊玩（村莊、森林探索、夥伴、商店、旅館存檔、Boss）。

- .NET 10、零第三方執行期依賴，直接輸出 ANSI/VT 序列
- 雙緩衝＋差異輸出（只送變動的格子）、真彩色（自動降級 256 / 16 色）
- 全形字（中日韓）正確對齊，CJK 感知的換行與禁則處理
- JRPG 元件：打字機對話框（逐字、分頁、標點停頓）、指令選單、HP/MP 條、視窗框、modal 選單
- 遊戲式主迴圈與場景堆疊（標題 → 城鎮 → 戰鬥，暫停選單可疊在上面）
- 可無頭測試：`HeadlessBackend` 把畫面渲染到記憶體，不需要真的終端機

設計細節與取捨見 [docs/DESIGN.md](docs/DESIGN.md)。

## 執行 Demo

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download) 與支援 UTF-8 的終端機（建議 80×24 以上）。

```bash
dotnet run --project src/TerminalGame.Demo
```

| 按鍵 | 動作 |
|---|---|
| ↑ ↓ ← → | 移動游標 |
| Enter / Space / Z | 確認；對話中：先顯示整段，再按翻頁 |
| Esc / Backspace / X | 取消 |
| Tab / M | 開啟隊伍選單（道具、技能、裝備、狀態） |
| ← → | 裝備／狀態畫面中切換角色 |
| Ctrl+C | 離開 |

沒有 TTY（CI、遠端 shell）時，可印出一段腳本化的遊戲流程畫面：

```bash
dotnet run --project src/TerminalGame.Demo -- --snapshot
```

## 最小範例

```csharp
using TerminalGame.Tui;
using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

using Application app = new(new ConsoleBackend());
app.run(new HelloScene());

sealed class HelloScene : Scene
{
    public HelloScene()
    {
        DialogueBox dialogue = new() { layoutHeight = Length.cells(6) };
        dialogue.show("你好，[gold]勇者[/]！歡迎來到[cyan]晨曦鎮[/]。", "長老");
        dialogue.completed += () => application!.quit();

        StackPanel layout = new();
        layout.add(new Spacer());   // 把對話框推到畫面底部
        layout.add(dialogue);

        root = layout;
        setFocus(dialogue);
    }
}
```

文字支援行內標記：`[gold b]傳說之劍[/]`、`[#40c060]…[/]`、`[on:blue white]…[/]`；無法辨識的括號（如 `[Potion]`）會原樣顯示。

## 專案結構

```
src/TerminalGame.Tui/        框架
  Text/        UnicodeWidth · StyledText · Markup · TextLayout
  Rendering/   ScreenBuffer · Canvas · FrameRenderer · ColorMode
  Input/       KeyEvent · KeyMap（按鍵 → GameAction）
  Backends/    ITerminalBackend · ConsoleBackend · HeadlessBackend · VtScreen
  Widgets/     Widget · StackPanel · Border · Label · TextBlock
               MenuList · DialogueBox · ProgressBar · ArtBlock · Spacer
  Runtime/     Application（主迴圈）· Scene（場景、焦點、modal）
src/TerminalGame.Rpg/        遊戲規則（不依賴 UI，可單元測試、可模擬）
  Data/        *Def 定義 · ContentDb（載入 JSON 並驗證交叉引用）
  State/       PartyMember · Inventory · Flags · GameSession · Progression · SaveData
  Battle/      BattleEngine（事件式）· BattleEvent · DamageFormula
  Script/      Condition（條件式）· ScriptRunner（事件腳本直譯器）
  World/       WorldRules（移動、遇敵）· ShopRules · FieldRules
src/TerminalGame.Demo/       遊戲本體（Scenes：Title · Location · Battle · Shop · PartyMenu · Equip · Status）
content/                     遊戲資料 JSON：技能、道具、角色、敵人、開局設定
tests/TerminalGame.Tui.Tests/  框架測試
tests/TerminalGame.Rpg.Tests/  規則與內容驗證測試
tests/TerminalGame.Demo.Tests/ 以無頭終端實際按鍵遊玩的流程測試
docs/DESIGN.md               框架設計文件
docs/ROADMAP.md              遊戲開發路線圖與進度
docs/CONTENT.md              內容 JSON 撰寫指南
tools/TerminalGame.Sim/      平衡模擬器（dotnet run --project tools/TerminalGame.Sim）
```

### 遊戲資料

`content/*.json` 會複製到執行檔旁的 `content/` 目錄，啟動時載入。技能、道具、角色、敵人、地點、商店、事件腳本全部是資料；所有 id 引用與條件式在載入時檢查，打錯字會直接列出錯誤而不是遊戲中途當掉。格式說明見 [docs/CONTENT.md](docs/CONTENT.md)。

存檔位置：`~/.local/share/TerminalGame/saves/`（Windows 為 `%LOCALAPPDATA%\TerminalGame\saves\`）。在旅館休息時可以存檔。

戰鬥規則集中在 `BattleEngine` 與 `DamageFormula`：引擎每次行動回傳一串 `BattleEvent`，`BattleScene` 只負責把事件轉成訊息並播放，因此規則可以脫離畫面測試與模擬。

## 開發

目標框架統一設定在 `Directory.Build.props`（目前 `net10.0`），所有專案共用。

```bash
dotnet build          # 同時以 .editorconfig 檢查命名規範（IDE1006）
dotnet test
```

命名慣例：變數、函式、屬性、欄位、參數用 lowerCamelCase；型別與列舉成員用 PascalCase。

## 已知限制

- 框線等「East Asian Ambiguous」字元一律當 1 欄寬；若終端機設為「ambiguous 當全形」會錯位，請改用 `BorderStyle.ascii`。
- 目前沒有滑鼠、文字輸入框。
- 在 Linux pty 上實測過；Windows 與 macOS 的終端機路徑尚未實測。

完整清單見 [docs/DESIGN.md](docs/DESIGN.md) 第 11 節。
