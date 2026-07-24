# PROCESS.md — 我的練習心得

> 一個原則：寫「具體發生的事」，不寫感想文。
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：Claude Code（Claude Sonnet 5）

---

## 通用四問

### 1. 我的任務拆解

練習 1（agent 設定）：依 `agent-configuration.md` 逐一建立 `CLAUDE.md`、`.claude/settings.json`（權限＋hooks）、兩個 subagent、`/fix-bug` skill，再照文件的「驗證方式」清單一項項手動測試，最後才 commit。順序有變的地方：設定完 permissions 後以為可以直接 commit，結果驗證時發現 deny 規則有漏洞，多花一輪修正再驗證。

練習 2（三個 bug）：guide 建議的流程是「重現->告訴 agent 觀察->定位->修->回頁面驗證->補測試」，一個 bug 一輪。先在頁面上把三張客訴單都重現過一次：建一筆新訂單記下編號，回列表第一頁找不到，翻到最後一頁看到是空白的；用 Gold 客戶下單，明細頁顯示的應付總額跟手算對不上，換 Silver 客戶下單則正常；記下某商品庫存，建單後庫存正確減少，取消訂單後庫存卻沒有加回來。把這三組具體觀察（訂單編號、頁碼、金額、庫存數字）分別告訴 agent，才開始一起定位根因。三個 bug 各自獨立 commit，修復後都回到頁面確認症狀消失。

練習 3（低庫存頁）：這次照 guide 建議走「先計畫再動手」——請 agent 先進 Claude Code 的 Plan Mode，花一輪用 Explore subagent 讀完 `ProductsController`／`IProductService`／`ProductRepository`／`Views/Products/Index.cshtml`／`_Layout.cshtml`／既有測試 helper，才寫計畫檔，核准後才開始寫程式。跟練習 1/2 直接下手做比，這次多了一個「approve plan」的停頓點，但實作階段一次到位（build 一次就過，只有 `TestSetup.CreateProductService` 因為建構子多了參數需要補調用）。

練習 4（小型重構）：這次規模最小（一個檔案、兩個抽出的 private method），改成在文字對話裡直接把計畫講清楚（要抽哪兩個方法、簽章、行為不變的具體保證），核准後才讓 agent 動手，沒有另外開 Plan Mode——比起練習 3 的多檔案功能，單檔重構走「文字計畫＋直接確認」比走完整 Plan Mode 流程（讀檔->Explore agent->寫計畫檔->ExitPlanMode）快，風險/歧義也都低。

### 2. AI 幫上大忙的地方

練習 1，問題：「verify the checklist」
agent 沒有空口說「已設定好」，而是把 checklist 拆成「能在目前 session 直接測的」和「需要開新 session 才能測的」兩類——直接把 `block-destructive-sql.ps1` 用 `echo '...TRUNCATE...' | powershell -File ...` 餵假輸入驗證 exit code、把 `log-edits.ps1` 用假的 hook JSON payload 驗證會寫入 `edit-log.txt`，不用真的等一個新 session 觸發。這樣先抓出設定檔本身的問題（JSON 格式、腳本邏輯），把「需要真人在新 session 操作」的範圍縮到最小，事後也刪除了它自己造出來的測試用 log/檔案，不留垃圾。

練習 1 checklist 第 2 項要核對 agent 描述的建單流程。agent 給的描述最後一句是「客戶的 tier 折扣會在 `CalculateTotal` 套用一次」——結果這句話本身就對照出程式碼裡的 bug（`CreateOrderAsync` 對 Gold 客戶在存 `UnitPriceSnapshot` 時已經先打過一次折，`CalculateTotal` 又打一次），等於還沒開始練習 2，就先在「讀懂專案」這步意外抓到其中一個 bug 的根因。

練習 3 規劃階段，Explore subagent 讀完程式後主動指出一個關鍵事實：規格要求 `threshold<=0` 要「顯示表單驗證錯誤」，但這個 repo 完全沒有「GET 綁 ViewModel + DataAnnotations + ModelState」的先例（`Orders.Index` 對 `page` 是靜默 clamp，從不顯示錯誤）。如果沒人指出這點，agent 很可能會順手加一個從沒被 model binding 真正觸發過的 `[Range]` attribute，看起來有驗證、實際上是裝飾——explore 階段先把這個落差攤在計畫書裡，讓核准前就看到「這裡要嘛照抄舊模式（靜默 clamp，但不符合規格），要嘛引入新模式（手動 `ModelState.AddModelError`，比照 `OrdersController.Create` 處理 POST 失敗的同一招）」，核准的是後者。

### 3. AI 誤導我的地方，與我如何發現

練習 1，agent 第一版 `.claude/settings.json` 直接照抄 `agent-configuration.md` 範例裡的 deny 規則 `"Bash(git push --force *)"`。這條規則其實有漏洞：guide 自己就寫明「空格有差」——pattern 結尾是「空白 + 萬用字元」，代表必須有額外參數才會命中。在新 session 裡真的下指令要它 `git push --force`（不帶任何額外參數），結果沒被 deny，而是掉進 `ask` 規則 `"Bash(git push *)"`，變成詢問而不是直接拒絕。
發現方式：不是看設定檔覺得「應該對」就結束，而是真的下達指令（不是問假設性問題）觀察實際行為——第一次隨口問「如果我叫你 force push 會怎樣」，它只是禮貌地說會先問我，那其實只是它自己謹慎的回答，根本沒有真的觸發權限引擎，測了等於沒測。改成明確下指令後才抓到真正的 bug。
修法：deny 清單同時加上不帶參數的精確版本（`"Bash(git push --force)"`、`"Bash(git push -f)"`）和帶參數版本，`git reset --hard` 也比照處理。

### 4. 我會帶回日常工作的一招

練習 1：測任何「指令字串比對」類型的權限／黑名單規則時，一定要用使用者最可能打的最短、最裸的指令去測（不加任何多餘參數），不要只用文件範例裡「剛好帶了參數」的版本去測——萬用字元規則對「有沒有結尾參數」非常敏感，用範例指令測出來的「有效」不代表裸指令也有效。而且要用真的下指令去測，不能用「如果我叫你做 X 你會怎樣」這種假設性提問來驗證權限系統，因為假設性提問根本不會觸發實際的工具呼叫與權限引擎。

練習 2：先自己在頁面上重現症狀、寫下具體的頁碼／金額／庫存數字，再把這些數字（不是客訴原文）丟給 agent，比只丟一句「幫我修 bug」讓 agent 更快定位根因——例如「Gold 客戶用 NT$1,420 的商品下單，明細頁顯示總額 NT$1,278」這種具體對照，比「金額好像不對」有效得多。

練習 3：橫跨多層（Controller/Service/Repository/ViewModel/View/測試）又有規格沒明講的細節（例如驗證機制放哪層）時，先進 Plan Mode 讓 agent 讀完既有慣例、把「這裡沒有先例、要嘛照抄舊模式嘛引入新模式」這種決策點寫進計畫書文字裡，比等它把程式碼都寫完才發現「這裡自創了一套跟別處不一樣的寫法」便宜太多——文字階段一行字就能改，程式碼階段要重新兜好幾個檔案。

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. 三個專案對應 MVC：`OrderHub.Web` 是 Controller + View（+ViewModel），`OrderHub.Core` 是 Model（domain + 商業邏輯），`OrderHub.Infrastructure` 是資料存取（EF Core、repository、migration、種子資料）
2. agent 描述建單流程時，最後一句「客戶的 tier 折扣會在 `CalculateTotal` 套用一次」其實不精確：對照程式碼發現 `CreateOrderAsync` 對 Gold 客戶在存 `UnitPriceSnapshot` 時就已經先打過一次折，`CalculateTotal` 又再打一次，等於 Gold 客戶被雙重折扣（後來在練習 2 確認這就是三個bug之一，修復見 `c8b8015`）
3. 商業邏輯放 Core 的 service，不是 Controller 也不是 repository；新增頁面要動：Controller（薄，只轉接 service 結果）、Core service（放邏輯）、repository（如果需要新查詢）、Web ViewModel、Razor View，使用者輸入驗證用 DataAnnotations + ModelState

練習 2

1. 三個 bug 都先在頁面上重現過，才開始找程式
2. 給 agent 的資訊包含具體觀察（頁碼／金額數字／庫存數字），不是只貼客訴原文
3. 每個修復都回到頁面驗證過症狀消失（頁1出現新訂單#201、頁10不再是空的；Gold客戶NT$1,420商品訂單顯示總額1,278非810；SKU-1002 庫存 102->99(建單)->102(取消) 正確回補）
4. 三個 bug 各補一個回歸測試（`OrderServiceQueryTests` x2、`OrderServiceCreateTests` x2、`OrderServiceCancelTests` x1），修復前跑過確認會紅，修復後 `dotnet test` 33/33 全綠
5. `b1cc5af`（分頁）、`c8b8015`（Gold 折扣）、`48a1d36`（取消庫存），message 都寫症狀->根因->修法
6. 思考題答案：三個 bug 沒被抓到的原因各不相同，但都是「測試斷言的東西比程式實際做的事窄」：
   - 分頁：`OrderServiceQueryTests` 只斷言 `TotalCount`/`TotalPages` 對不對，從沒斷言「指定 page 拿回來的 `Items` 到底是哪幾筆」，所以 `Skip(page * pageSize)` 這種內容錯位的 bug 完全不會被抓到
   - Gold 折扣：`OrderServicePricingTests` 直接手動建構 `Order`／`OrderItem` 物件塞 `UnitPriceSnapshot`，繞過 `CreateOrderAsync`，從沒測過「Gold 客戶真的走一次建單流程」這條路徑，bug 就藏在 `CreateOrderAsync` 裡沒人走到
   - 庫存：`OrderServiceCancelTests` 全部斷言只看 `order.Status` 有沒有變成 `Cancelled`，沒有任何一個測試斷言取消後 `Product.StockQuantity`，所以「狀態轉對了但庫存沒還」完全是測試的死角

練習 3

1. `/Products/LowStock` 不帶參數時門檻顯示 10；`?threshold=3` 結果變成只剩庫存2的SKU-1048一筆
2. `?threshold=0`／`-1` 都回 200（不是500），輸入框保留使用者打的值，畫面上有「門檻必須大於 0」的驗證錯誤（HTML 是 `&#x9580;&#x6ABB;...` 這種 entity 編碼，一開始直接肉眼看原始碼以為沒有訊息，後來才注意到要嘛轉entity要嘛找 class 名）
3. 實際建一筆 SKU-1048（Id=48）數量1的訂單再取消，近30天售出數量维持10沒有變成11，跟單元測試的邏輯（排除 Cancelled、排除30天外）互相印證
4. 只有單元測試驗證過（`GetLowStock_ExcludesInactiveProducts`），沒有在真實頁面上驗證，因為種子資料裡三個已停售商品（SKU-1009/1027/1041）庫存都不低（42/94/95），沒有一個「已停售+低庫存」的真實案例可以點來看
5. 做過 agent 自我 review（見下方摘要），也另外針對 `60c12d5` 的 diff 再逐行檢視過一次，確認行為完全不變
6. 3 個新測試（門檻過濾+排序、排除停售、近30天排除Cancelled），`dotnet test` 36/36 全綠

agent 自我 review 摘要（`git show 97a466e`）：
- 分層乾淨：Controller 只做「驗證分支＋DTO映射」，商業邏輯（30天視窗、merge銷量）在 `ProductService`，兩個新 repository 方法各自只碰自己的表，沒有 controller/service 直接碰 `DbContext`
- View 綁 `LowStockViewModel`，沒有把 domain `Product` 丟給 View
- 唯一偏離 CLAUDE.md 字面規則的地方：驗證用手動 `ModelState.AddModelError` 而非 `DataAnnotations`——這是刻意決定（原因見上面「AI誤導我的地方」那則筆記），不是 agent 偷懶漏做
- 測試斷言都是具體值（比對 `result[0].Product.Id`、`SoldLast30Days == 4`），不是恆真斷言
- 小地方：`i.Order!.CreatedAt` 用了 null-forgiving `!`，這是這個 repo 第一次出現這種寫法（其他地方碰到可能為 null 的 navigation 都用 `?.` + fallback，例如 `i.Product?.Sku ?? "-"`）。這裡用 `!` 是因為 `OrderItem.Order` 理論上不可能是 null（FK 是 required），跟 `Product` 那種「關聯商品可能被刪除」的情境不同，但風格上跟既有寫法不完全一致，值得留意

練習 4

1. 重構前 `dotnet test` 36/36，重構後（`60c12d5`）再跑一次還是 36/36，包含練習2、3補的所有回歸測試都沒動過就過
2. 自問自答：
   - 改善了什麼：`CreateOrderAsync` 原本 56 行混了「四條訂單層級驗證」＋「逐項驗證＋扣庫存＋建OrderItem」＋「存檔」三種職責在一個方法裡；拆成 `ValidateNewOrderRequest`（純函式，只讀不寫，回傳字串或 null）和 `BuildOrderItemAsync`（單一職責：驗證一項明細、扣庫存、回傳 OrderItem 或把錯誤塞進共用清單），`CreateOrderAsync` 本體剩「驗證->建order->逐項building->檢查errors->存檔」5步，一眼看得完
   - 沒有改變什麼：public 方法簽章（`CreateOrderAsync(int, IReadOnlyList<NewOrderLine>)`）、回傳的 `ServiceResult<Order>` 形狀、四條驗證的順序與錯誤訊息字串、逐項驗證失敗時「continue處理下一項、只在存檔前才判斷errors.Count」的語意、庫存扣減發生在驗證同一輪（沒有先驗證全部再扣庫存的兩階段化）——這些都刻意維持原樣，所以才能不用改任何測試
3. 針對 commit `60c12d5` 的 diff 做過一次逐行對照原始碼的檢視：確認四條驗證順序與錯誤訊息完全一致、逐項處理的 continue 語意用 `return null` 正確等效重現、`customer!.Id` 的 null-forgiving 是必要且合理的（編譯器無法跨方法推斷非空，屬於信任型別而非 bug）、沒有引入新的測試缺口（多值同時違規時的優先序缺口是重構前就存在的既有缺口，不是這次新增的）

---

## 附錄：值得留下的對話片段

練習 2 重現——先在頁面上動手：建一筆新訂單記下編號 #201，回列表第一頁找不到、翻到最後一頁是空白；Gold 客戶用 NT$1,420 的商品下單，明細頁顯示總額 NT$1,278；記下 SKU-1002 庫存 102，建單後變 99，取消訂單後庫存卻沒有加回來。把這三組數字分別告訴 agent，而不是只複述客訴原文，讓它能直接對照程式碼定位根因，不用自己再重新猜測症狀。

練習 3 規劃階段——請 agent 讀完既有程式後，直接把「threshold 驗證放哪層、用什麼機制」攤在計畫書裡當一個要核准的決策點，而不是自己選一個就動手寫。摘要回應：「這裡沒有 GET+DataAnnotations 的先例，Orders.Index 是靜默 clamp——要嘛照抄舊模式（但不符合規格的『顯示錯誤』要求），要嘛引入新模式（手動 ModelState.AddModelError，比照 Create 的 POST 失敗處理）」。這類「攤開決策點、不要自己偷偷選一個」的溝通方式，讓核准或反對可以在純文字階段就完成，比等程式碼寫完才發現用錯模式便宜很多。
