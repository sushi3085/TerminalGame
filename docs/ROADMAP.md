# ROADMAP：從 Demo 到可玩 1 小時的 JRPG

目標：用 `TerminalGame.Tui` 做出一款**可完整遊玩約 60 分鐘**的文字 JRPG——多座村莊、分階裝備、多位夥伴、有冒險氛圍。

本文件同時是**跨 session 的工作交接單**：每完成一項就勾選，並在最後的「進度日誌」補一行。新 session 開始時，先讀本文件與 `docs/DESIGN.md`。

---

## 1. 內容規模（以 1 小時反推）

假設一場戰鬥 30–45 秒、對話與探索約佔 40% 時間：

| 項目 | 數量 | 備註 |
|---|---|---|
| 村莊／據點 | 3 | 各有商店、旅館、劇情 NPC；約每 20 分鐘換一次場景 |
| 野外／迷宮區域 | 4–5 | 村莊之間的路 + 最終迷宮 |
| 一般敵人 | 15–20 種 | 每區 3–4 種；後期可用強化版（換色＋改名＋新數值） |
| Boss | 3–4 | 每章結尾一隻，最終 Boss 兩階段 |
| 夥伴 | 3（含主角 4 人） | 每章加入一位，各有戰術定位 |
| 裝備 | 4 部位 × 4–5 階 | 每座村莊商店升一階；迷宮寶箱給「下一階」 |
| 技能 | 每人 4–6 個 | 依等級解鎖 |
| 戰鬥次數 | 約 60–80 場 | |

原則：**內容可以重複利用，系統不要每章重寫。**

### 故事骨架（暫定）

| 章 | 據點 | 區域 | 新夥伴 | Boss |
|---|---|---|---|---|
| 1 | 晨曦鎮 | 低語森林 | 琳（弓手／速度型） | 森林之主・巨蕈 |
| 2 | 河港村 | 沉船洞窟 | 巴爾（重盾／坦克） | 深潭水蛇 |
| 3 | 砂岩城 | 熾熱遺跡 | 小雪（治癒／輔助） | 遺跡守衛 |
| 終 | — | 魔王城 | — | 魔王（兩階段） |

每章節奏：村莊（劇情、補給）→ 野外（練功、寶箱）→ 迷宮（分岔／簡單機關）→ Boss → 新夥伴／新地點。每 5–10 分鐘要有「新東西」。

---

## 2. 架構

```
src/
  TerminalGame.Tui/     既有：純 UI 框架，不認識「遊戲」
  TerminalGame.Rpg/     遊戲規則，純 C#，不引用 Tui → 可單元測試、可 Monte Carlo 模擬
    Data/      *Def：唯讀定義（ItemDef, EnemyDef, SkillDef, CharacterDef…）與 ContentDb
    State/     可變狀態（PartyMember, Inventory, Flags, GameSession, SaveData）
    Battle/    BattleEngine（事件式）、BattleEvent、DamageFormula、敵人 AI
    World/     MapGraph、EncounterTable（M1）
    Script/    對話／事件腳本直譯器（M1）
  TerminalGame.Demo/    Scenes：把 Rpg 的狀態畫出來（日後可改名 TerminalGame.Game）
content/                JSON 內容檔（建置時複製到輸出目錄）
tests/
  TerminalGame.Tui.Tests/
  TerminalGame.Rpg.Tests/  規則與內容驗證測試
  TerminalGame.Demo.Tests/ 無頭終端的遊玩流程測試
tools/
  TerminalGame.Sim/        平衡模擬（M2）
```

### 關鍵設計決策

1. **Def 與 State 分離**：定義從 JSON 載入、全遊戲共用；狀態只存 id 與數值。存檔 = 序列化 State。
2. **戰鬥引擎事件式**：`BattleEngine.execute(action)` 回傳 `IReadOnlyList<BattleEvent>`；Scene 只負責收集輸入與「播放」事件（訊息、HP 條）。引擎不知道畫面存在。
3. **亂數可注入**：引擎吃 `Random`（或 `IRandom`），測試與模擬可固定 seed，結果可重現。
4. **內容驗證測試**：所有交叉引用（掉落物、技能、地圖出口、腳本中的 id）在測試中檢查，內容越多越重要。
5. **平衡靠模擬**：引擎可脫離 UI 執行，用 Monte Carlo 統計勝率、平均回合、剩餘 HP 分布。

---

## 3. 里程碑與待辦

### M0 架構重構（行為不變）✅

- [x] 新增 `TerminalGame.Rpg` 類別庫與 `TerminalGame.Rpg.Tests`，加入 sln
- [x] `StatBlock`（maxHp, maxMp, attack, defense, magic, speed）與運算
- [x] Def 型別：`CharacterDef`、`EnemyDef`、`ItemDef`、`SkillDef`（含 `EquipSlot`、`ItemKind`、`TargetKind`）
- [x] `ContentDb`：從 JSON 載入（System.Text.Json），提供查詢與交叉引用檢查（錯誤一次全部列出）
- [x] 內容 JSON 放在 repo 根目錄 `content/`，建置時複製到輸出目錄（Demo 與測試皆同），重現 Demo 的史萊姆、勇者、傷藥、火球術
- [x] State：`PartyMember`（level、exp、hp/mp、裝備）、`Inventory`、`Flags`、`GameSession`（隊伍、背包、金錢、旗標、亂數）
- [x] 經驗值曲線與升級（`Progression`，升級時學會技能）
- [x] `BattleEngine`：多對多、依速度排行動順序、攻擊／技能／道具／防禦／逃跑、敵人加權隨機 AI、勝敗判定與戰利品
- [x] `BattleEvent` 階層與 `DamageFormula`
- [x] Demo 的 `BattleScene`／`TownScene`／`StatusScene` 改用 Rpg 層（新增技能／道具子選單、多目標選擇、戰敗劇情）
- [x] 測試：傷害公式、行動順序、道具消耗、勝敗、升級、內容驗證、同 seed 可重現、200 場自動對戰
- [x] `TerminalGame.Demo.Tests`：用無頭終端按鍵打完勝利／戰敗流程
- [x] `--snapshot` 仍可執行

### M1 垂直切片：第 1 章完整（目標 10–15 分鐘且好玩）

- [x] 世界：節點圖地圖（`LocationDef` + `WorldRules`）：晨曦鎮 → 低語森林（入口／小徑／林間空地）→ 森林深處，另有獵人小屋
- [x] 遭遇表與隨機遇敵（抵達時依 `encounterRate`；「四處探索」必定遇敵）
- [x] 旗標 + 條件式（`Condition`）+ 事件腳本直譯器（`ScriptRunner`）
- [x] 商店（買／賣）、旅館（回復＋設復活點＋存檔）
- [x] 隊伍選單：道具、治療技能、裝備（換裝前後數值預覽）、狀態（逐人分頁）
- [x] 存檔／讀檔（`SaveSystem`／`SaveStore`，JSON 含 `version`）；標題畫面「繼續冒險」
- [x] 夥伴琳加入（救援事件）；多人時戰鬥隊伍面板改為每人一行＋目前行動者標記
- [x] Boss：森林之主（+2 隻毒孢菇，會再生、會全體攻擊）
- [x] 區域配色（`Palettes`）與每個地點的 ASCII 圖、抵達描述
- [x] 第 1 章結局劇情，並預告第 2 章
- [x] `docs/CONTENT.md`：內容 JSON 撰寫指南
- [x] 自動化通關測試：用真實 UI 按鍵從標題玩到第 1 章結局
- [ ] **尚待人工試玩**：實際遊玩時間、難度手感（目前數值只經過粗估，見下方交接）

### M2 系統補齊

- [ ] 狀態異常（毒、睡、麻痺、攻擊↑、防禦↑）與屬性弱點（火、冰、雷、聖）
- [ ] 冒險日誌（目前目標、大事紀）
- [ ] NPC 流言（依旗標換台詞）、夥伴閒聊
- [ ] `tools/TerminalGame.Sim` 平衡模擬；第 1 章數據達標（一般戰勝率 > 98%、平均剩 60% HP；Boss 預期等級勝率約 70%）

### M3 內容量產

- [ ] 第 2 章：河港村、沉船洞窟、巴爾、深潭水蛇
- [ ] 第 3 章：砂岩城、熾熱遺跡、小雪、遺跡守衛
- [ ] 終章：魔王城、魔王兩階段
- [ ] 全流程約 60 分鐘

### M4 打磨

- [ ] 轉場、戰鬥演出（受擊閃爍等）
- [ ] 難度微調、試玩回饋
- [ ] 試玩者不需要問「接下來去哪」

---

## 4. 給下一個 session 的交接

- **環境**：雲端容器預設沒有 .NET。`builds.dotnet.microsoft.com` 被網路政策擋下，請改用 Ubuntu 套件：`apt-get install -y dotnet-sdk-10.0`（必要時先 `apt-get update`）。NuGet 可正常連線。
- **驗證**：`dotnet build`（0 警告）、`dotnet test`（三個測試專案）、`dotnet run --project src/TerminalGame.Demo -- --snapshot`。
- **sln 換行**：`dotnet sln add` 會把 `TerminalGame.sln` 改成 LF，加完專案後請轉回 CRLF（`sed -i 's/\r\{0,1\}$/\r/' TerminalGame.sln`）。
- **M0 留下的設計決定**
  - 戰鬥是「逐人行動」制（每回合依速度±10% 排序，輪到誰誰就行動），不是 DQ 式「先全員下指令再結算」。
  - 勝利時存活的成員各自拿到全額經驗值；倒下的成員拿不到。
  - 爆擊只發生在物理攻擊（1/16，×1.5）；防禦狀態傷害減半，持續到自己下次行動。
  - 道具只能由我方使用；目前沒有復活效果（M2 可加 `Revive` EffectKind）。
  - 敘述文字（`attackMessage`、`useMessage`）放在內容 JSON，讓每隻怪物／技能有自己的台詞。
- **M1 留下的設計決定與已知缺口**
  - 腳本中的 `battle` 只有勝利才會繼續；戰敗由 `LocationScene` 統一處理（失去一半金錢、回到復活點）。
  - 選項按取消 = 選最後一項（慣例上是「不要／離開」）。
  - 任何角色都能裝備任何武器（還沒有職業限制）；可在 M2 加 `ItemDef.equippableBy`。
  - 沒有復活道具；倒下的成員要靠旅館或小屋休息恢復。
  - **平衡未驗證**：只會普攻的機器人在 Lv9 打不過 Boss（會用技能和道具的玩家應該沒問題）。M2 的模擬器要用「會放技能、會補血」的 AI 來量化各區域的勝率。
  - 戰鬥中多人隊伍面板沒有 HP 條，只有文字；4 人時寬度剛好。
- **下一步**：M2——先做 `tools/TerminalGame.Sim` 平衡模擬（給 BattleEngine 一個簡單策略 AI），量第 1 章各遭遇與 Boss 的勝率，再調 `content/enemies.json`；接著做狀態異常與屬性。

---

## 5. 進度日誌

| 日期 | 內容 |
|---|---|
| 2026-10-05 | 建立 ROADMAP |
| 2026-10-05 | 完成 M0：`TerminalGame.Rpg`（資料／狀態／戰鬥引擎）、`content/` JSON、Demo 改用新架構、179 個測試全過 |
| 2026-10-05 | 完成 M1（待人工試玩）：世界／腳本／商店／旅館／存檔／隊伍選單、第 1 章完整內容與通關測試；230 個測試全過 |
