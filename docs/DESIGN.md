# 設計文件：TerminalGame.Tui

為「文字 JRPG」設計的終端機 UI 框架。本文記錄做了哪些決定、為什麼，以及已知限制。

## 1. 目標與非目標

**目標**

- 在終端機渲染 JRPG 常見元素：視窗框、指令選單、打字機對話框、HP/MP 條、場景切換。
- **中日韓文字是一等公民**：全形字（佔 2 欄）不能讓邊框錯位、不能殘影。
- 遊戲式主迴圈（固定幀率、`update(dt)` / `render`），不是表單式應用程式。
- 可自動化測試：不需要真的終端機就能驗證畫面。
- 零第三方執行期依賴。

**非目標（目前）**：滑鼠、文字輸入框、圖片協定（sixel/kitty）、音效、存檔。見第 11 節。

## 2. 技術選型

| 決定 | 理由 |
|---|---|
| .NET 8（C# 12） | LTS；`System.Text.Rune`、`StringInfo` 內建 Unicode 字素叢集（grapheme cluster）切分，省去自己實作 UAX #29。 |
| 直接輸出 ANSI/VT 序列 | 跨平台一致，且能精準控制「每幀送出什麼位元組」。.NET 的 `Console.SetCursorPosition` / `ForegroundColor` 呼叫次數多、不支援真彩色與同步輸出。 |
| 自製框架，不包 Terminal.Gui / Spectre.Console | 我對它們的印象是前者以「視窗＋表單」的應用程式為出發點、後者以格式化輸出為主，並非以遊戲迴圈為中心（此為選型時的判斷，未做正式評測）；而本專案的需求（逐字顯示、場景堆疊、CJK 對齊）都落在核心路徑上，自己掌控比較單純。自製後整個框架約 3,900 行（含註解與空行），範圍小到可以完全掌控。 |
| 保留模式（retained）widget 樹，不用 ECS 或立即模式 | JRPG 的 UI 是有狀態的（游標位置、打字進度、分頁），widget 物件天然持有這些狀態。 |

## 3. 分層

```
┌─────────────────────────────────────────────┐
│ Runtime   Application · Scene（堆疊、modal、焦點） │  遊戲層
├─────────────────────────────────────────────┤
│ Widgets   Widget · StackPanel · Border · Label │
│           TextBlock · MenuList · DialogueBox   │  UI 層
│           ProgressBar · ArtBlock · Spacer      │
├─────────────────────────────────────────────┤
│ Text      UnicodeWidth · StyledText · Markup   │
│           TextLayout（換行、禁則）              │  文字層
├─────────────────────────────────────────────┤
│ Rendering ScreenBuffer · Canvas · FrameRenderer│  渲染層
├─────────────────────────────────────────────┤
│ Backends  ITerminalBackend ─┬ ConsoleBackend   │
│ Input     KeyEvent · KeyMap └ HeadlessBackend  │  平台層
│                               （+ VtScreen）    │
└─────────────────────────────────────────────┘
```

相依方向只往下。`Application` 只認識 `ITerminalBackend`，所以真實終端機與測試用的記憶體終端機走完全相同的程式碼。

### 一幀的流程（`Application.step`）

1. 套用佇列中的場景變更（push / pop / replace）。
2. 偵測終端機尺寸；變了就重建 buffer、通知場景、讓渲染器全量重畫。
3. `backend.pollKeys` 取出按鍵 → `KeyMap` 解析成 `GameAction` → 派送給頂層場景。
4. `scene.tick(dt)`：更新 widget 動畫（打字機等），然後 `onUpdate`。
5. 清空 frame buffer，由下往上渲染可見場景（透明場景會露出下層）。
6. `FrameRenderer` 與「終端機目前顯示的內容」做差異比較，產生最小的 ANSI 位元組流，**一次寫出**。

場景變更採**延遲套用**：場景可以在自己的 `onKey` / `onUpdate` 裡安全地呼叫 `pushScene`，不會在迭代中修改堆疊。

## 4. 儲存格模型與全形字

`Cell(glyph, style, isContinuation)`。

- `glyph` 是一個**字素叢集**（`e` + 組合重音符是一格，不是兩格）。
- 全形字佔兩格：左格存字形，右格是 `isContinuation = true` 的佔位格（同樣式）。

`ScreenBuffer.put` 維持兩個不變式：

1. 全形字後面**恰好**跟一個 continuation。
2. 不存在孤兒 continuation。

覆寫時要處理兩種「切半」：寫入落在某全形字的**右半** → 把它的左半清成空白；寫入蓋到某全形字的**左半**（且新字較窄）→ 把它的右半清成空白。真實終端機對這兩種情況的行為也是如此，所以模型與終端機一致。

`Canvas` 的裁切會遇到「全形字只有一半在裁切區內」：此時改畫空白，避免殘影。

### 寬度計算（`UnicodeWidth`）

- 寬字元：East Asian Width = W/F 的區間表（二分搜尋）＋ emoji 呈現區間。
- 寬度 0：組合記號（Mn/Me）、格式字元（Cf，含 ZWJ、零寬空格）、韓文 Jamo 中/終聲。
- 字素叢集寬度 = 其中字元寬度最大值；含 U+FE0F（emoji 呈現選擇符）的窄字元、以及成對區域指示符（旗幟）視為 2 欄。

**限制**：East Asian *Ambiguous* 字元（框線 `─│`、`●`、`…`）一律當 1 欄。若使用者的終端機設定為「ambiguous 當全形」（常見於部分 CJK locale），框線會錯位。對策：改用 `BorderStyle.ascii`。這是終端機生態本身的歧義，沒有可靠的偵測方法。

## 5. 差異渲染（`FrameRenderer`）

渲染器持有 `front`（它認為終端機現在顯示的內容）。每幀：

```
for 每個 cell（略過 continuation）:
    if cell == front[cell]: 跳過
    若游標不在此處: 輸出 CSI row;col H
    若樣式與「目前 SGR 狀態」不同: 輸出 SGR 變更
    輸出 glyph；front[cell] = cell；游標前進 1 或 2 欄
```

要點：

- **只在不連續時移動游標**：同列連續變動的 cell 共用一次定位。
- **SGR 最小化**：只有樣式改變才輸出；若有屬性要「關掉」就整組 `0` 重設後重新套用，否則只送增量（fg/bg/新增屬性）。
- **同步輸出**（DEC 2026：`CSI ? 2026 h/l`）包住整幀，支援的終端機會原子化顯示，避免撕裂；不支援的會忽略。
- **關閉自動換行**（`CSI ? 7 l`）：畫右下角那一格不會觸發捲動。離開時還原。
- 尺寸改變或 `invalidate()` 時，把 `front` 填成「永遠不等於任何真實 cell」的 sentinel，並先 `CSI 2J`，等於全量重畫。
- 畫面完全沒變時回傳空字串，**不產生任何 I/O**。

### 顏色降級

`ColorModeDetector` 依 `NO_COLOR` / `COLORTERM` / `TERM` 選擇 `TrueColor` / `Ansi256` / `Ansi16` / `None`。降級是在**輸出時**做（buffer 內永遠是 RGB），所以遊戲程式碼只需面對一種色彩模型：

- 256 色：在 6×6×6 色立方與 24 階灰階之間取距離最近者。
- 16 色：對 xterm 標準調色盤取歐氏距離最近者。

### 透明色

繪圖 API 的 `Color.transparent` 表示「沿用底下的」。解析發生在 `ScreenBuffer.put`（背景取自被覆寫的 cell；前景取終端機預設）。因此文字可以直接畫在 HP 條或視窗底色上，不必知道底色是什麼。儲存的 cell 永遠是已解析的。

## 6. 版面（兩段式）

仿 WPF / Flutter：

- `measure(available)`：回報期望尺寸。
- `arrange(rect)`：父容器給定最終矩形（父座標系）。
- `render(canvas)`：收到**已平移並裁切**到自己範圍的 `Canvas`，永遠從 (0,0) 畫起。

尺寸策略 `Length`：`Auto`（用量測值）、`Fixed(n)`、`Fill(weight)`（按權重瓜分剩餘空間）。`StackPanel` 沿主軸依此分配；Fill 以「先無條件捨去、剩餘的格子由前面的 Fill 子項逐格補上」分配，保證總和恰等於可用空間（有屬性測試）。交叉軸預設撐滿，除非子項要求 Fixed。

**取捨**：每幀都重新 measure/arrange，沒有髒標記。80×24 的樹成本可忽略；好處是 widget 內容改變不必通知任何人。若日後出現大型樹，再加快取。

## 7. 文字處理

- **`StyledText`**：不可變的 `StyledGlyph[]`（字素、寬度、樣式）。所有繪製、換行、打字機都以它為單位，所以顏色能跨換行、跨逐字顯示保留。
- **`Markup`**：`"獲得 [gold b]傳說之劍[/]！"`。`[/]` 彈出最近一層；顏色名、`#rrggbb`、`on:色`、`b i u dim rev strike blink`。**無法辨識的括號保持原樣**（`[Potion]` 直接顯示），遊戲字串不需跳脫；`[[` 為字面 `[`。
- **`TextLayout.wrap`**（貪婪斷行）：
  - 拉丁字在空白處斷；剛好填滿行尾的空白會「懸掛」並丟棄；超長單字硬斷。
  - CJK：任兩個字之間（只要其中一個是寬字）都可斷。
  - **禁則處理（kinsoku）**：`。，」）…` 不得出現在行首、`「（` 不得出現在行尾——若斷點違規，就把前一個字一起移到下一行。僅為最小實作，沒有「懸掛標點」等進階規則。

## 8. 輸入

- **輪詢而非執行緒**：`Console.KeyAvailable` + `ReadKey(intercept: true)`，在遊戲迴圈內每幀排空。單執行緒，沒有鎖。
- **`GameAction` 抽象**：widget 只看 `Confirm / Cancel / Up / …`，不看實體按鍵；`KeyMap` 集中綁定，可重新對應（預設：方向鍵、Enter/Space/Z 確認、Esc/Backspace/X 取消、Tab/M 選單）。
- **Ctrl+C 當成按鍵**（`TreatControlCAsInput`），由 `Application` 優雅結束；`ProcessExit` 再補一道還原，涵蓋 `SIGTERM`。
- **焦點與冒泡**：按鍵先給焦點 widget，沒處理（`handleInput` 回傳 false）就沿 `parent` 往上，最後落到 `scene.onKey`。`MenuList` 只在有人訂閱 `confirmed/cancelled` 時才吃掉對應按鍵，所以沒訂閱的取消鍵會自然冒泡給場景。
- **Modal**：`showModal` 疊在場景上並取得焦點，開著時下層收不到任何按鍵；`closeModal` 還原先前焦點。

## 9. 場景堆疊

`Scene` 有 `onEnter / onExit / onPause / onResume / onUpdate / onKey / onResize`。`isTransparent` 的場景（暫停選單、狀態面板）會讓下層場景仍被繪製（但不更新、不收輸入）。`replaceScene` 只觸發被換掉者的 `onExit` 與新場景的 `onEnter`，下層維持暫停——這點曾有 bug，見測試 `sceneLifecycleFollowsStackOperations`。

### 對話框（`DialogueBox`）

- 逐字顯示：以「累積時間 − 每字成本」推進，標點後額外停頓（`punctuationPause`）；`charsPerSecond <= 0` 為即時顯示。
- 分頁：依內框寬高換行後每 N 行一頁；視窗縮放時重新分頁並夾住進度。
- 確認鍵語意：打字中 → 直接顯示整頁；已顯示完 → 下一頁；最後一頁 → 觸發 `completed`。

## 10. 測試策略

`VtScreen` 是一個只懂渲染器會輸出的那幾種序列（游標定位、清屏、SGR、私有模式忽略）的迷你終端機模擬器。核心保證寫成測試：

> 對任意繪圖操作序列，每一幀之後，「只看過我們輸出的終端機」所顯示的內容，必須與該幀逐格相同。

`randomizedDiffOutputReproducesEveryFrame` 以固定種子的隨機繪圖驗證這一點：字形混用窄字、全形字、組合字元、emoji；顏色混用透明/不透明/預設；屬性、巢狀裁切、負座標；刻意使用 1×1、7×3、13×5 這類會讓全形字壓在右邊緣的尺寸。

開發過程中做過**變異驗證**：暫時拿掉孤兒處理，確認有 6 個測試（含隨機測試）變紅，之後還原。

其餘：Unicode 寬度、換行不變量（任何輸入、任何寬度下，每行寬度不超過上限、不遺失可見字元）、Markup、各 widget 行為、Scene 生命週期、焦點／modal、縮放、例外時終端機仍被還原。

`HeadlessBackend` 把這套機制開放給遊戲開發者：

```csharp
var backend = new HeadlessBackend(80, 24);
var app = new Application(backend);
app.pushScene(new TitleScene(state));
backend.queueKeys(Key.Down, Key.Enter);
app.step(1.0 / 30);
Assert.Contains("開始遊戲", backend.screenText);
```

Demo 的 `--snapshot` 參數用同樣方式在沒有 TTY 的環境印出整段遊戲流程的畫面。

## 11. 已知限制與後續

- 沒有滑鼠、沒有文字輸入框（取名畫面需要）、沒有 `Grid` 版面、沒有焦點的 Tab 切換。
- `ConsoleBackend` 在 Linux pty 上已實測（進入/離開備用螢幕、Ctrl+C、`SIGTERM`、顏色偵測）；**Windows 路徑（啟用 VT 處理的 P/Invoke）與 macOS 尚未實測**。
- `Console.ReadKey` 一次一個 `char`，輸入法組字（IME）與 surrogate pair（emoji）無法輸入。
- 尺寸變化以輪詢偵測（每幀讀 `Console.WindowWidth/Height`），而非 `SIGWINCH`；最多延遲一幀。
- 差異比較對整個 buffer 是 O(寬×高)/幀；80×24 下可忽略，超大視窗可改用髒區域追蹤。
- 後續可做：`TextInput`、`Grid`、轉場效果（淡入／橫掃）、tween/計時器、`Tilemap` widget（地圖探索）、存讀檔。

## 12. 命名慣例

依專案偏好，**變數、函式、屬性、欄位、參數一律 lowerCamelCase**（`drawText`、`selectedIndex`）；型別、命名空間、列舉成員維持 PascalCase。`.editorconfig` 以 `IDE1006` 在建置時檢查（`EnforceCodeStyleInBuild`），違規會出現警告。
