# OmokAI — 오목(Gomoku) AI 시연 데모 + 재사용 가능한 엔진 라이브러리
오목 AI 엔진을 **독립 라이브러리**(`OmokEngine`)로 분리하고,
MonoGame DesktopGL 기반의 데스크톱 데모(`OmokGame`)에서 1~20단계 실력을 조절하며
시연·검증할 수 있게 한 프로젝트.

엔진은 **UI 의존성이 0**이므로 향후 온라인 게임 서버(소켓/HTTP/gRPC)에서 그대로 재사용할 수 있다.

---

## 1. 주요 기능

### 데모 게임 (OmokGame)

- **20단계 실력** 슬라이더 조절 (Lv1 왕초보 ~ Lv20 신)
- **사람 vs AI** / **AI vs AI** 두 모드 지원
- 흑/백 색상 선택 (사람이 흑일 때만 렌주 금수 33/44/장목 적용)
- **힌트** 버튼: 항상 Lv20 강도로 다음 수 추천 (펄스 마커)
- **무르기** (PvA에서 자기 + AI 두 수 되돌림)
- **항복**, **저장/불러오기** (`saves/quicksave.json`)
- **수순 번호 표시** 토글, **마지막 착수 마커**
- **테마 전환** (밝은 나무 / 짙은 나무)
- **효과음** (착수 클릭, 승리 차임 — 외부 WAV 없이 PCM 합성)
- 5목 완성 시 **승리 라인 강조**, 페이드 인 애니메이션
- 1024×800 창, 좌 70% 보드 / 우 30% 설정 패널
- 고DPI 모니터 흐림 방지 (`SetProcessDPIAware` P/Invoke)

### 엔진 (OmokEngine)

- 15×15 보드, Negamax + α-β + Transposition Table (Zobrist 해시)
- **반복 심화**(iterative deepening) — 시간 초과 시 마지막 완료 결과 반환
- **VCF**(Victory by Continuous Four) 탐색 (Lv17부터 활성)
- **렌주 룰 검사** (33/44/장목)
- 패턴 평가기로 후보 5개 추출 후 확률적 선택 (Optimal/Good/Mistake 비율)
- 동기 API(`RequestMove`) + 비동기 래퍼(`RequestMoveAsync`)

---

## 2. 빌드 및 실행

### 요구 사항

- Windows 10/11
- .NET 10 SDK
- 한글 폰트(`malgun.ttf` 자동 탐지 또는 `Content/`에 동봉)

### 빌드

```bash
dotnet build OmokAI.slnx -c Debug
```

### 실행

```bash
dotnet run --project src/OmokGame
```

또는 빌드 산출물 직접 실행:
```bash
dotnet src/OmokGame/bin/Debug/net10.0/OmokGame.dll
```

---

## 3. 프로젝트 구조

```
devOmokAI/
├── OmokAI.slnx
├── omok_ai.md                          # 원본 엔진 명세
├── src/
│   ├── OmokEngine/                     # .NET 10 클래스 라이브러리, UI 의존성 0
│   │   ├── Core/                       # Stone, Position, GomokuBoard
│   │   ├── Evaluation/                 # Pattern, MoveEvaluator
│   │   ├── Search/                     # Minimax, VCF, Zobrist, TT
│   │   ├── Analysis/                   # RenjuRuleChecker
│   │   └── AI/                         # GomokuAI(facade), LevelProfile, Options
│   │
│   └── OmokGame/                       # MonoGame DesktopGL 데모
│       ├── Game1.cs                    # MonoGame 루프, single-flight Task.Run
│       ├── GameSession.cs              # UI 보드 사본 + 게임 상태 머신
│       ├── SoundManager.cs             # PCM 합성 SoundEffect
│       ├── UI/                         # BoardRenderer, SettingsPanel, 컨트롤
│       └── Content/                    # PNG 스프라이트 + TTF 폰트
└── tools/
    └── SvgToPng/                       # SVG → PNG 변환 콘솔 (수동 실행)
```

---

## 4. 코드 분석

### 4.1 엔진 아키텍처

| 계층 | 역할 | 핵심 클래스 |
|------|------|------------|
| Core | 보드 상태/좌표 | `GomokuBoard`, `Position`, `Stone` |
| Evaluation | 정적 평가 | `PatternAnalyzer`, `MoveEvaluator` |
| Search | 게임 트리 탐색 | `MinimaxEngine`, `VCFEngine`, `ZobristHasher`, `TranspositionTable` |
| Analysis | 룰 검사 | `RenjuRuleChecker` |
| AI | 진입점 퍼사드 | `GomokuAI`, `GomokuAiOptions`, `LevelProfile` |

### 4.2 `GomokuAI` 퍼사드 설계

원본 명세(`omok_ai.md`)의 `AdaptiveGomokuAI`에는 두 가지 결함이 있다:

1. **SearchDepth가 dead code** — `currentConfig.SearchDepth`가 `MinimaxEngine.FindBestMove`로 전달되지 않음
2. **VCF가 무조건 실행** — 옵션과 무관하게 매 호출마다 VCF 탐색

`GomokuAI` 퍼사드는 **엔진 코드를 수정하지 않고 우회**하여 위 결함을 회피한다:

- `MoveEvaluator.EvaluateAllMoves`로 후보 10개 추출
- 즉시 승리 / 4목 방어는 항상 최우선
- `LevelProfile.VcfDepth > 0`인 단계만 `VCFEngine.FindVCFSequence` 호출
- `MinimaxEngine.FindBestMove(stone, depth, timeMs)` 직접 호출 + 반복 심화
- 후보를 `OptimalProb` / `GoodProb` / `MistakeProb` 비율로 확률적 선택 → 단계별 체감 차이 보장
- 렌주 활성화 시 후보에서 금수 위치 사전 필터링

### 4.3 단일 비행(single-flight) AI 호출 패턴
엔진은 단일 스레드 동기 구조이며 깊이 5+VCF에서 수 초 소요 가능. UI 멈춤 방지 + 스레드 안전 위해
다음 규칙을 강제한다:

1. **UI는 자체 `GomokuBoard` 사본 보유** — 엔진 내부 보드와 절대 공유 금지
2. **새 AI 요청은 직전 요청이 끝난 후에만 시작** — `_currentGeneration` 카운터로 stale 결과 폐기
3. **`Random` 단일 인스턴스는 thread-unsafe** — `RequestMove`를 동시에 두 번 호출 금지
4. AI 차례마다 `Task.Run(() => ai.RequestMove())` 시작, `Task.IsCompleted` 매 프레임 폴링

힌트도 별도 `_hintRequestId` 카운터로 관리되어, 무르기/항복/플레이어 착수 시 stale 결과 폐기.

### 4.4 데모 측 추가 견고성 (B1~B12 픽스)

- `_silent` 플래그로 Load 중 효과음 억제
- `EndGameWon`/`EndGameDraw`/`Surrender`에서 `ForbiddenCells.Clear()`
- 게임 진행 중 모드/색상/렌주 변경 잠금 (`CanChangeSettings`)
- `Checkbox`/라디오에 mouse press → release **edge-detection**으로 잠금 경계 race 차단
- `_aiTask.Result`/`_hintTask.Result` 접근 try/catch — 엔진 예외가 게임 다운으로 이어지지 않게
- `SoundManager` 생성자도 try/catch — 오디오 디바이스 없는 환경에서도 게임 계속

---

## 5. 라이브러리로 사용하기 — 소켓 서버 예제
`OmokEngine`은 UI 의존성이 0이므로 다음과 같이 서버 측에서 그대로 사용 가능하다.

### 5.1 기본 사용 패턴

```csharp
using GomokuEngine.AI;
using GomokuEngine.Core;

// 1) 게임 세션 시작 (방 생성 시점)
var ai = new GomokuAI(new GomokuAiOptions
{
    Level    = 15,           // 1~20
    UseRenju = true,
    AiStone  = Stone.White
});

// 2) 사람의 수 적용
ai.ApplyMove(new Position(7, 7), Stone.Black);   // H8

// 3) 룰 검사 (선택)
var (ok, reason) = ai.CanPlace(new Position(8, 8), Stone.Black);
if (!ok) { /* 클라이언트에 거부 응답 */ }

// 4) AI 다음 수 계산
var move = await ai.RequestMoveAsync();
if (move != null)
{
    ai.ApplyMove(move.Position, Stone.White);
    bool aiWon = ai.CheckWin(move.Position, Stone.White);
}

// 5) 게임 종료 시
ai.Reset();
```

### 5.2 동시성 규칙 (서버 환경에서 반드시 준수)

| 규칙 | 이유 |
|------|------|
| **방 1개당 `GomokuAI` 인스턴스 1개** | `GomokuBoard` mutable + `Random` thread-unsafe |
| **같은 인스턴스에 `RequestMove` 동시 호출 금지** | 단일 비행. 클라이언트 입력 처리 직렬화 필요 |
| 별도 방은 별도 인스턴스 → **수평 스케일 안전** | 인스턴스 간 공유 상태 없음 |
| 시간 제한은 `LevelProfile.TimeLimitMs`로 결정 | Lv20에서 최대 ~9초. 서버 타임아웃 산정에 반영 |

### 5.3 ASP.NET Core + WebSocket 골격 예제

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RoomManager>();
var app = builder.Build();

app.UseWebSockets();
app.Map("/ws/{roomId}", async (HttpContext ctx, string roomId, RoomManager rooms) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await rooms.HandleAsync(roomId, ws, ctx.RequestAborted);
});

app.Run();

// RoomManager.cs
public class GameRoom
{
    public GomokuAI Ai { get; }
    public Stone HumanStone { get; }
    // 같은 방에서 동시 처리를 막는 락
    public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);

    public GameRoom(int level, Stone humanStone, bool useRenju)
    {
        HumanStone = humanStone;
        Ai = new GomokuAI(new GomokuAiOptions
        {
            Level    = level,
            UseRenju = useRenju,
            AiStone  = humanStone == Stone.Black ? Stone.White : Stone.Black
        });
    }
}

public class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public async Task HandleAsync(string roomId, WebSocket ws, CancellationToken ct)
    {
        var room = _rooms.GetOrAdd(roomId,
            _ => new GameRoom(level: 15, humanStone: Stone.Black, useRenju: true));

        var buf = new byte[4096];
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(buf, ct);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var json = Encoding.UTF8.GetString(buf, 0, result.Count);
            var msg = JsonSerializer.Deserialize<ClientMove>(json);

            await room.Lock.WaitAsync(ct);
            try
            {
                // 1) 룰 검사 + 사람 수 적용
                var pos = new Position(msg!.Row, msg.Col);
                var (ok, reason) = room.Ai.CanPlace(pos, room.HumanStone);
                if (!ok) { await Send(ws, new { type = "reject", reason }, ct); continue; }

                room.Ai.ApplyMove(pos, room.HumanStone);
                if (room.Ai.CheckWin(pos, room.HumanStone))
                {
                    await Send(ws, new { type = "gameover", winner = "human" }, ct);
                    _rooms.TryRemove(roomId, out _);
                    break;
                }

                // 2) AI 응수 계산 (방 락은 유지: 같은 방은 직렬화)
                var aiMove = await room.Ai.RequestMoveAsync();
                if (aiMove == null)
                {
                    await Send(ws, new { type = "gameover", winner = "draw" }, ct);
                    _rooms.TryRemove(roomId, out _);
                    break;
                }

                room.Ai.ApplyMove(aiMove.Position, room.Ai.Options.AiStone);
                bool aiWon = room.Ai.CheckWin(aiMove.Position, room.Ai.Options.AiStone);

                await Send(ws, new
                {
                    type = "move",
                    row = aiMove.Position.Row,
                    col = aiMove.Position.Col,
                    score = aiMove.Score,
                    moveType = aiMove.Type.ToString()
                }, ct);

                if (aiWon)
                {
                    await Send(ws, new { type = "gameover", winner = "ai" }, ct);
                    _rooms.TryRemove(roomId, out _);
                    break;
                }
            }
            finally { room.Lock.Release(); }
        }
    }

    private static Task Send(WebSocket ws, object payload, CancellationToken ct) =>
        ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(payload),
                     WebSocketMessageType.Text, true, ct);

    private record ClientMove(int Row, int Col);
}
```

**핵심 포인트:**
- `SemaphoreSlim`으로 **방 단위 직렬화** → 단일 비행 규칙 준수
- 방 인스턴스를 `ConcurrentDictionary`로 관리 → 다른 방은 진정 병렬 실행
- 게임 종료/타임아웃 시 `_rooms.TryRemove`로 정리 (메모리 누수 방지)
- `RequestMoveAsync`는 내부적으로 `Task.Run`이므로 워커 스레드 풀 사용 — Lv20 동시 다발 호출 시
  ThreadPool 사이즈 모니터링 필요

### 5.4 인스턴스 재사용 (객체 소멸/생성 없이 다음 게임)

`GomokuAI`는 내부 보드·Minimax 캐시·Random 인스턴스를 모두 보존하므로,
**한 번 생성한 객체를 게임 종료 후에도 계속 재사용**할 수 있다. 매 게임마다 new로 만드는 것보다
TT 캐시 워밍, GC 부담 감소, Random 다양성 유지 측면에서 유리.

```csharp
// 한 번만 생성 (방 또는 사용자 단위로 보관)
var ai = new GomokuAI(new GomokuAiOptions
{
    Level    = 15,
    UseRenju = true,
    AiStone  = Stone.White
});

// ── 게임 1 ──
ai.ApplyMove(new Position(7, 7), Stone.Black);
var move1 = await ai.RequestMoveAsync();
// ... 게임 진행 ...

// 같은 옵션으로 다음 게임 시작 — 보드/캐시만 비움, 인스턴스 유지
ai.Reset();

// ── 게임 2 (옵션 변경) ──
// 레벨/색/렌주를 바꿔서 새 게임 시작
ai.Reset(new GomokuAiOptions
{
    Level    = 10,
    UseRenju = false,
    AiStone  = Stone.Black     // AI가 흑(선공)
});

// 사용자가 백이므로 AI가 먼저 둠
var firstMove = await ai.RequestMoveAsync();
ai.ApplyMove(firstMove!.Position, Stone.Black);
```

**주의 사항:**

| 항목 | 규칙 |
|------|------|
| `Reset()` 호출 시점 | 진행 중인 `RequestMove`가 없는 상태에서만. in-flight면 대기 후 호출 |
| 옵션 변경 시 | `Reset(newOptions)` 사용 — `_minimax.ClearCache()`도 같이 일어남 |
| `Random` 인스턴스 | 재사용 시 시드가 자연스럽게 진행 → 매 게임 다른 초반 수 보장 |
| TT 캐시 | `Reset()` 안에서 `ClearCache` 호출 → 이전 게임의 위치 평가가 다음 게임 오염시키지 않음 |
| 서버에서 풀(pool)링 | 사용자별/방별로 1개씩 보관, 게임 종료 시 `Reset()` 후 풀에 반환 |

**서버에서 풀링 패턴:**

```csharp
public class GomokuAiPool
{
    private readonly ConcurrentBag<GomokuAI> _pool = new();

    public GomokuAI Rent(GomokuAiOptions options)
    {
        if (_pool.TryTake(out var ai))
        {
            ai.Reset(options);   // 옵션 갱신 + 보드/캐시 리셋
            return ai;
        }
        return new GomokuAI(options);
    }

    public void Return(GomokuAI ai)
    {
        ai.Reset();              // 다음 사용 전 미리 청소
        _pool.Add(ai);
    }
}
```

이렇게 하면 객체 생성 비용을 분산할 수 있고, Lv20 같은 무거운 인스턴스도 재활용된다.
단 풀에서 꺼낸 인스턴스의 `_options`는 직전 사용자 값이 남아있을 수 있으므로 **반드시
`Reset(options)`로 옵션 명시 갱신** 후 사용할 것.

### 5.5 상태 직렬화 (저장/복원)
서버 재시작이나 게임 이어하기를 지원하려면 `_history` 리스트만 보존하면 된다:

```csharp
// 저장
var moves = ai.GetBoardSnapshot();   // 또는 사용자가 history를 별도 보관
// 직렬화하여 DB/Redis에 저장

// 복원
var ai = new GomokuAI(options);
foreach (var (pos, stone) in savedHistory)
    ai.ApplyMove(pos, stone);
```

데모의 `GameSession.Save`/`Load`가 동일 패턴(JSON history 배열)을 사용한다.

### 5.6 권장 운영 설정

| 항목 | 권장값 | 이유 |
|------|--------|------|
| 서버 타임아웃 | `TimeLimitMs * 1.3` | 반복 심화 + VCF 합산 |
| 방당 최대 동시 요청 | 1 | 단일 비행 |
| Lv20 이상 동시 게임 수 | 워커 스레드 수의 1/2 | CPU bound |
| `GomokuAI` 인스턴스 재사용 | 게임 종료까지 유지 | TT/캐시 보존 → 후속 호출 가속 |
| 로그 수집 | `EvaluatedMove.Score`/`Type` | 난이도 튜닝/디버깅에 유용 |

---

## 6. AI 실력 단계 (전체 20단계)

| Lv | 이름 | Depth | OptimalProb | VCF | TimeMs |
|----|------|-------|-------------|-----|--------|
| 1  | 왕초보       | 1 | 0.05 | -  | 300 |
| 2  | 입문         | 1 | 0.10 | -  | 400 |
| 3  | 초급         | 1 | 0.20 | -  | 500 |
| 4  | 초급+        | 1 | 0.35 | -  | 600 |
| 5  | 중급         | 2 | 0.50 | -  | 800 |
| 6  | 중급+        | 2 | 0.62 | -  | 1000 |
| 7  | 중급++       | 2 | 0.72 | -  | 1500 |
| 8  | 고급         | 2 | 0.82 | -  | 2000 |
| 9  | 고급+        | 2 | 0.90 | -  | 2500 |
| 10 | 고급++       | 3 | 0.92 | -  | 3000 |
| 11 | 전문         | 3 | 0.94 | -  | 3500 |
| 12 | 전문+        | 3 | 0.96 | -  | 4000 |
| 13 | 전문++       | 3 | 0.98 | -  | 4500 |
| 14 | 마스터       | 4 | 0.98 | -  | 5000 |
| 15 | 마스터+      | 4 | 0.99 | -  | 5500 |
| 16 | 마스터++     | 4 | 1.00 | -  | 6000 |
| 17 | 그랜드마스터 | 4 | 1.00 | 8  | 6500 |
| 18 | 고수         | 5 | 1.00 | 10 | 7000 |
| 19 | 최강         | 5 | 1.00 | 12 | 8000 |
| 20 | 신           | 5 | 1.00 | 14 | 9000 |

---

## 7. 알려진 제약 / 향후 개선

- 오프닝 북 미적용 — 첫 5수는 무작위성 큼
- VCT(Victory by Continuous Threat) 미구현 — VCF만 지원
- MCTS 미구현 — 명세에 있는 MCTS 클래스는 추출 제외
- 1024×800 고정 창 — 리사이즈 미지원
- 사운드는 PCM 합성 단순 톤 — WAV/OGG 자산 사용 안 함

---

## 8. 라이선스 / 크레딧
- 폰트: 맑은 고딕(`malgun.ttf`) — Windows 시스템 폰트
- MonoGame DesktopGL 3.8.4.1, FontStashSharp 1.5.5
