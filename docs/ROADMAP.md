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
  content/ 或 內嵌資源   JSON 內容檔
tests/
  TerminalGame.Tui.Tests/
  TerminalGame.Rpg.Tests/  規則與內容驗證測試
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

### M0 架構重構（行為不變）

- [ ] 新增 `TerminalGame.Rpg` 類別庫與 `TerminalGame.Rpg.Tests`，加入 sln
- [ ] `StatBlock`（maxHp, maxMp, attack, defense, magic, speed）與運算
- [ ] Def 型別：`CharacterDef`、`EnemyDef`、`ItemDef`、`SkillDef`（含 `EquipSlot`、`ItemKind`、`TargetKind`）
- [ ] `ContentDb`：從 JSON 載入（System.Text.Json），提供 `get*` 查詢與 `validate()` 交叉引用檢查
- [ ] 內容 JSON 以內嵌資源打包（`content/*.json`），重現目前 Demo 的史萊姆、勇者、傷藥、火球術
- [ ] State：`PartyMember`（level、exp、hp/mp、裝備）、`Inventory`、`GameSession`（隊伍、背包、金錢、旗標、亂數）
- [ ] 經驗值曲線與升級（`Progression`）
- [ ] `BattleEngine`：多對多、依速度排行動順序、攻擊／技能／道具／逃跑、敵人簡單 AI、勝敗判定與戰利品
- [ ] `BattleEvent` 階層與 `DamageFormula`
- [ ] Demo 的 `BattleScene`／`TownScene`／`StatusScene` 改用 Rpg 層，玩起來與重構前一致
- [ ] 測試：傷害公式、行動順序、道具消耗、勝敗、升級、內容驗證
- [ ] `--snapshot` 仍可執行

### M1 垂直切片：第 1 章完整（目標 10–15 分鐘且好玩）

- [ ] 世界：節點圖地圖（`MapGraph`），晨曦鎮 ↔ 低語森林（3 區塊）↔ 森林深處
- [ ] 遭遇表與隨機遇敵
- [ ] 旗標 + 對話／事件腳本（條件、效果：setFlag、giveItem、joinParty、startBattle、choice）
- [ ] 商店（買／賣）、旅館（回復＋存檔）
- [ ] 裝備畫面（顯示換裝前後數值差）、道具畫面
- [ ] 存檔／讀檔（JSON，含 `version`）；標題畫面加「繼續」
- [ ] 夥伴琳加入；隊伍戰鬥 UI（多人 HP/MP、選目標）
- [ ] Boss：森林之主
- [ ] 區域配色（Theme）與抵達新地點的 ASCII 圖／描述

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

## 4. 進度日誌

| 日期 | 內容 |
|---|---|
| 2026-10-05 | 建立 ROADMAP |
