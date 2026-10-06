# 開發環境與踩過的坑

給之後在雲端容器（或新機器）上接手的人。ROADMAP 第 5 節是「遊戲做到哪」，這份是「環境怎麼弄、哪裡容易繞遠路」。

## 1. 安裝 .NET 10

雲端容器**預設沒有 .NET**。

| 做法 | 結果 |
|---|---|
| 官方安裝腳本／`builds.dotnet.microsoft.com` | ✗ 被網路政策擋下（不要再試） |
| `apt-get install -y dotnet-sdk-10.0`（Ubuntu 24.04 套件庫） | ✓ 約 1–2 分鐘；必要時先 `apt-get update` |
| NuGet 還原（xunit 等） | ✓ 可正常連線 |

```bash
which dotnet || (apt-get update -qq && apt-get install -y -qq dotnet-sdk-10.0)
dotnet --version   # 10.0.1xx
```

安裝很慢時可以放到背景執行，同時先讀程式碼。

## 2. 驗證指令

```bash
dotnet build                                            # 要 0 警告
dotnet test                                             # 三個測試專案；首次建置約 20 秒
dotnet run --project src/TerminalGame.Demo -- --snapshot
dotnet run -c Release --project tools/TerminalGame.Sim  # 平衡報告；用 Release 快很多
```

`dotnet test` 第一次含建置要 30 秒以上，指令逾時請設長一點。

## 3. 踩過的坑

### 環境與 git

- **`TerminalGame.sln` 換行**：`dotnet sln add` 會把整個檔案改成 LF。加完專案後轉回 CRLF：
  `sed -i 's/\r\{0,1\}$/\r/' TerminalGame.sln`
- **PR 只合併了一部分**：分支的 PR #1 是在較早的 commit 被合併的，之後的 commit 沒有進 `main`。接手時先 `git fetch` 並用
  `git log --oneline origin/main..HEAD` 檢查；若 PR 已合併、分支上還有未合併的 commit，就 `git rebase origin/main` 後
  `git push --force-with-lease`。

### 平衡比較

- 想知道「改動前」的模擬數字時，不必 stash：用 worktree 在舊 commit 跑模擬器，跑完再移除。
  ```bash
  git worktree add /tmp/base HEAD
  (cd /tmp/base && dotnet run -c Release --project tools/TerminalGame.Sim)
  git worktree remove --force /tmp/base
  ```

### 程式

- **JSON 的 `required` 屬性**：`System.Text.Json` 對缺少 `required` 欄位的物件直接丟例外，`ContentDb` 會把它列成
  「missing required properties」，而且**整個檔案**都讀不進來，接著出現一堆「unknown skill」的連鎖錯誤。新增「某些情況下可以省略」的欄位時不要標 `required`（例：`EffectDef.power` 對 `Status` 效果沒有意義）。看到大量連鎖錯誤時先找第一行。
- **戰鬥引擎只會自動跑「失去的回合」**（睡眠、麻痺）。一般的敵人回合仍要呼叫端 `execute(decideEnemyAction())`；
  寫測試時用「我方行動 → 跑完敵人回合直到再輪到我方」的小幫手（見 `StatusTests.heroRound`）。
- **流程測試依選單順序按鍵**：`GameFlowTests` 用「按 ↓ 幾次」或「等到 `▶ 某選項`」操作。地點新增／刪除選項（spot、出口）
  會改變順序，優先用 `act("選項文字")` 這種按文字找的寫法。
- **看實際畫面**：無頭終端 `HeadlessBackend.screenText` 可以直接印出畫面。需要目視檢查版面時，可以暫時寫一個測試把畫面寫進檔案，
  看完再刪掉。

## 4. 可以再自動化的部分

每次新 session 都要手動裝 .NET。若想省掉，可在 `.claude/settings.json` 加一個 SessionStart hook 執行第 1 節的安裝指令。
