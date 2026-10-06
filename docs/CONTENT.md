# 內容撰寫指南（content/*.json）

遊戲的所有資料都在 `content/`，建置時複製到執行檔旁。啟動時 `ContentDb` 會載入並**驗證所有引用**：打錯 id、條件式語法錯誤、腳本指令寫錯，都會在啟動時一次列出，而不是玩到一半才當掉。`dotnet test` 裡的 `shippedContentLoadsAndValidates` 也會檢查同一件事。

| 檔案 | 內容 |
|---|---|
| `skills.json` | 技能（我方與敵方共用） |
| `items.json` | 道具、裝備、重要物品 |
| `characters.json` | 可加入隊伍的角色 |
| `enemies.json` | 敵人（含 ASCII 圖、AI 行動表、掉落物） |
| `locations.json` | 地點節點圖 |
| `shops.json` | 商店的商品清單 |
| `scripts.json` | 事件腳本：`{ "腳本id": [指令, ...] }` |
| `journal.json` | 冒險日誌：目前目標與大事紀 |
| `banter.json` | 夥伴閒聊 |
| `newGame.json` | 開局隊伍、道具、金錢、起始地點 |

文字欄位都支援行內標記：`[gold]…[/]`、`[red b]…[/]`、`[dim]…[/]`、`[#40c060]…[/]`；`\n` 換行。

---

## 數值公式（`DamageFormula`）

```
物理傷害 = 攻擊 × power% − 對方防禦 / 2        （1/16 機率爆擊 ×1.5）
魔法傷害 = 魔力 × power% − 對方防禦 / 4
最終傷害 = max(1, round(上式 × 0.85～1.15 × 屬性倍率))   防禦中再 ×0.5；弱點 ×1.5、抗性 ×0.5
治療量   = power + 魔力 × magicScaling%          （有 magicScaling 時 ±10%）
經驗曲線 = 升到下一級需要 round(20 × level^1.6)
逃跑機率 = clamp(0.5 + 0.05 × (我方平均速度 − 敵方平均速度), 0.25, 0.95)
```

角色能力值 = `baseStats` + `growth` × (等級 − 1) + 所有裝備的 `bonus`。

---

## 技能與道具

```json
{ "id": "fireball", "name": "火球術", "mpCost": 5, "target": "SingleEnemy",
  "effect": { "kind": "Magical", "power": 220 }, "useMessage": "詠唱[magenta]火球術[/]！" }
```

- `target`：`Self`、`SingleAlly`、`AllAllies`、`SingleEnemy`、`AllEnemies`（以使用者的角度；敵人的 `SingleEnemy` 就是我方一人）
- `effect.kind`：`Physical`、`Magical`、`Heal`（可加 `magicScaling`）、`RestoreMp`、`Status`（不改 HP/MP，只附加或解除狀態）
- `effect.element`（只限傷害）：`Fire`、`Ice`、`Thunder`、`Holy`
- `effect.status`：`{ "kind": "Poison", "chance": 0.3, "turns": 3 }`，對每個目標各擲一次；傷害技能只有在目標還站著時才附加，失敗不會顯示訊息；`Status` 技能失敗會顯示「沒有效果」
- `effect.cures`：`[ "Poison" ]`，解除這些狀態（解毒草、薄荷葉）

### 狀態異常

| `kind` | 效果 |
|---|---|
| `Poison` | 每次輪到自己行動**之後**損失最大 HP 的 1/10（至少 1） |
| `Sleep` | 無法行動；受到傷害會醒來 |
| `Paralysis` | 每回合 50% 機率無法行動 |
| `AttackUp` / `DefenseUp` | 攻擊／防禦 ×1.5 |
| `AttackDown` / `DefenseDown` | 攻擊／防禦 ×0.75；和對應的提升互相抵消 |

- `turns` 是「持有者自己的回合數」，在持有者的回合結束時扣 1；在自己的回合對自己施加的狀態，那一回合不算。重複施加會取較長的剩餘回合。
- 所有狀態都只存在於戰鬥中，戰鬥結束就消失（不寫進存檔）。
- `useMessage` 接在使用者名字後面，例如「艾倫」+「詠唱火球術！」

道具 `kind`：`Consumable`（要有 `effect`）、`Equipment`（要有 `slot`：`Weapon`／`Body`／`Head`／`Accessory`，和 `bonus`）、`Key`（重要物品，不能賣）。`price` 0 表示不販售；賣價是半價。

## 敵人

```json
{ "id": "wolf", "name": "野狼", "level": 4,
  "stats": { "maxHp": 52, "attack": 15, "defense": 6, "speed": 11 },
  "exp": 44, "gold": 16,
  "actions": [ { "weight": 3 }, { "skillId": "bite", "weight": 1 } ],
  "drops": [ { "itemId": "potion", "chance": 0.2 } ],
  "attackMessage": "猛撲而來！",
  "art": [ "[gray] /\\_/\\ [/]", "..." ] }
```

`actions` 是加權隨機表；沒有 `skillId` 的那一項是普通攻擊；MP 不夠的技能、以及自己身上已經有的增益技能會被略過。`art` 每一行一個字串；反斜線要寫成 `\\`。

屬性與抗性：`"weakTo": [ "Fire" ]`（×1.5，戰鬥中顯示「效果拔群！」）、`"resists": [ "Ice" ]`（×0.5）、`"immuneTo": [ "Sleep", "Paralysis" ]`（Boss 通常免疫控制類狀態）。

## 地點

```json
{ "id": "woodsPath", "name": "低語森林・小徑", "kind": "Field", "palette": "forest",
  "description": "到達時顯示的一段描述。", "art": [ "..." ],
  "encounterRate": 0.45,
  "encounters": [ { "enemies": [ "forestBat", "forestBat" ], "weight": 3 } ],
  "onEnter": [ { "if": "flag:sawTracks == 0", "script": "tracksEvent" } ],
  "spots":   [ { "label": "調查倒下的樹幹", "script": "chestLog" } ],
  "exits":   [ { "to": "woodsClearing", "label": "前往林間空地", "if": "flag:metRin" } ] }
```

- `kind`：`Town`（安全，不遇敵）、`Field`、`Dungeon`
- `palette`：視窗配色，目前有 `town`、`forest`、`deepForest`、`cave`（見 `src/TerminalGame.Demo/Palettes.cs`）
- `encounterRate`：抵達時遇敵的機率；選單裡的「四處探索」則必定遇敵
- `onEnter`：抵達時，第一個條件成立的腳本會執行（用旗標避免重複觸發）
- `spots`、`exits` 的 `if` 不成立時不會出現在選單裡
- 戰敗時損失一半金錢，回到最後住過旅館的地點（一開始是 `newGame.location`）

## 條件式

用在 `if`（腳本、出口、地點、選項）：

```
flag:名稱          旗標的值（沒設定過就是 0）
item:道具id        持有數量
party:角色id       在隊伍裡為 1，否則 0
level:角色id       該角色等級（不在隊伍為 0）
gold               持有金錢
比較：== != >= <= > <      邏輯：&& || ! ( )      單獨一個值代表「不等於 0」
```

例：`flag:metRin && !flag:bossDefeated`、`gold >= 100 || item:forestKey`。

## 腳本指令

每個指令**剛好一個**動作欄位：

| 指令 | 說明 |
|---|---|
| `{ "say": "文字", "speaker": "[cyan]長老[/]" }` | 對話（`speaker` 可省略 = 旁白） |
| `{ "if": "條件", "then": [...], "else": [...] }` | 分支 |
| `{ "choice": [ { "label": "是", "if": "條件", "then": [...] } ] }` | 選項；按取消會選最後一項 |
| `{ "cases": [ { "if": "條件", "then": [...] }, { "then": [...] } ] }` | 執行**第一個**條件成立的分支（沒有 `if` = 一定成立）；NPC 依旗標換台詞用這個 |
| `{ "oneOf": [ { "if": "條件", "then": [...] }, ... ] }` | 在條件成立的分支中**隨機**選一個（流言、閒聊） |
| `{ "setFlag": "x", "value": 2 }` / `{ "addFlag": "x", "value": 1 }` | 設定／增加旗標（`value` 預設 1） |
| `{ "giveItem": "potion", "count": 2 }` / `{ "takeItem": "key" }` | 給予／拿走道具（會顯示訊息） |
| `{ "giveGold": 50 }` / `{ "takeGold": 10 }` | 金錢 |
| `{ "join": "rin", "level": 4 }` | 角色加入（已在隊伍則略過） |
| `{ "battle": ["wolf", "wolf"], "canEscape": false }` | 戰鬥；**只有勝利才會繼續執行後面的指令** |
| `{ "shop": "dawnItems" }` | 開啟商店 |
| `{ "inn": 8 }` | 旅館：付費、全員恢復、設為復活點、詢問是否存檔 |
| `{ "travel": "locationId" }` | 腳本結束後移動到該地點 |
| `{ "run": "otherScript" }` | 執行另一個腳本（可共用片段） |
| `{ "restore": true }` | 全員恢復（不存檔） |
| `{ "end": true }` | 立即結束腳本 |

寶箱的慣用寫法（用旗標記住已經打開）：

```json
"chestLog": [
  { "if": "flag:chestLog",
    "then": [ { "say": "樹幹下只剩下泥土。" } ],
    "else": [ { "say": "倒下的樹幹下壓著一個舊皮袋。" },
              { "giveItem": "ether" }, { "giveGold": 40 }, { "setFlag": "chestLog" } ] }
]
```

NPC 依劇情換台詞的慣用寫法：

```json
"townWoman": [ { "cases": [
  { "if": "flag:bossDefeated", "then": [ { "say": "森林裡又有鳥叫聲了呢。", "speaker": "[cyan]婦人[/]" } ] },
  { "if": "flag:metRin",       "then": [ { "say": "琳回來了？太好了……", "speaker": "[cyan]婦人[/]" } ] },
  { "then": [ { "say": "聽說森林裡的史萊姆最近會成群出沒……", "speaker": "[cyan]婦人[/]" } ] }
] } ]
```

---

## 冒險日誌（`journal.json`）

```json
{ "objectives": [ { "text": "去長老家。" }, { "if": "flag:introDone", "text": "進入低語森林，尋找琳。" } ],
  "chronicle":  [ { "if": "flag:metRin", "text": "在林間空地救出了琳。" } ] }
```

- `objectives` 依劇情順序排列，**最後一個**條件成立的就是「目前目標」（顯示在隊伍選單和日誌裡）。條件寫成累加的（後面的旗標隱含前面的）就不用寫 `== 0`。
- `chronicle` 中所有條件成立的項目都會依序列出。
- 日誌完全由旗標推導，不佔存檔空間；修改文字後舊存檔也會顯示新文字。

## 夥伴閒聊（`banter.json`）

```json
[ { "id": "rescue", "at": [ "woodsClearing" ], "if": "flag:metRin", "script": "banterRescue" },
  { "id": "smallTalk", "once": false, "script": "banterSmallTalk" } ]
```

- 隊伍有兩人以上，且有閒聊可說時，地點選單會出現「隊伍閒聊」；有沒聽過的對話時標上 `!`。
- 先依檔案順序播放還沒聽過的一次性閒聊（`once` 預設 `true`，聽過後設旗標 `banter_<id>`）；都聽過了，就從可重複的（`"once": false`）隨機挑一段。
- `at` 是地點 id 清單，省略 = 任何地方。
