# IMUNROK UI — 완성된 VR 화면 한 벌

제3사건(견우)에서 만든 UI를 **화면째로** 뽑아낸 것이다.
대화창·소지품 판·안내 문구·퍼즐 얼개가 **이미 완성된 채로** 들어 있다.

> **이 꾸러미의 목적은 팀 전체의 화면을 같아 보이게 하는 것이다.**
> 각자 만들면 스타일이 갈라진다. **가져다 쓰고, 내용만 갈아 끼워라.**

이 문서 하나만 읽고 쓸 수 있게 썼다.

---

## 0. 세 걸음이면 끝난다

1. `IMUNROK_UI.unitypackage` 임포트
2. `Window ▸ TextMeshPro ▸ Import TMP Essential Resources` (한 번)
3. `Tools ▸ 이문록 ▸ UI 예제 씬 만들기` → 생긴 씬을 열고 **Play**

그러면 대화창·소지품·퍼즐 화면이 **아무것도 구현하지 않은 채로** 뜬다.
글꼴(조선 궁서체)도 이미 들어 있어 **첫 화면부터 견우와 똑같이 보인다.**

Play 중 조작: **좌클릭**(바라보고 누르기) · **I**(소지품) · **F8**(PC ↔ VR 전환) · **WASD**(걷기)

---

## 1. 왜 이렇게 만들었나 (먼저 읽을 것)

VR에서 글을 띄우는 방법은 셋인데 **둘은 못 쓴다.**

| 방법 | HMD에서 | 왜 |
|---|---|---|
| `OnGUI` (IMGUI) | ❌ **아예 안 보인다** | 화면 버퍼에 직접 그린다. 스테레오 경로를 안 탄다 |
| Screen Space – Overlay 캔버스 | ❌ **아예 안 보인다** | 같은 이유. 카메라를 거치지 않는다 |
| **World Space 캔버스** | ✅ | 판이 실제로 눈앞 공간에 떠 있다 |

그런데 월드 판을 그냥 쓰면 PC 화면에서 자리를 다시 다 잡아야 하고, VR에서는 고개를 돌릴 때마다
판이 시야 밖으로 나가고, 벽에 파묻힌다. 이 꾸러미가 그 셋을 대신 풀어 준다.

- **자리·크기는 픽셀로 말한다** — `UiTuning` 이 *캔버스 1단위 = PC 화면 1픽셀* 이 되게 배율을 잡는다
- **PC는 화면 붙박이, VR은 느슨한 추종** — 죽은 구간 7° + 지연 0.16초로 멀미를 막는다
- **벽 회피** — 판의 가운데와 네 귀퉁이로 광선을 쏴 막힌 만큼 당기고, **배율도 함께 줄여** 보이는 각을 지킨다

---

## 2. 무엇이 들어 있나

임포트하면 `Assets/IMUNROK_UI/` 하나가 생긴다(글꼴만 `_Project/_Common/Art/Fonts/` 로 간다).

### 완성된 화면 — 그대로 쓰는 것

| | 파일 | 무엇 |
|---|---|---|
| **대화창** | `DialogueUI` | **화면 아래 가로 바** (확정안). 이름패·대사 굴려 읽기·글쇠 칸·말하기·증거 제시. PC·VR 각각의 치수 포함 |
| **소지품 판** | `InventoryUI` | 목록(칸 미리보기) · 상세(3D 회전 + 본문 + 사용하기) |
| **전체 화면 조사** | `InventoryInspect` | 어두운 막 위에 물건 하나만 크게 |
| **3D 미리보기** | `InventoryPreview` | 렌더텍스처로 물건을 돌려 본다 |
| **안내 문구** | `DebugToast` · `ToastPanel` | 위에 잠깐 / 아래 고정 |
| **퍼즐 얼개** | `FocusInteractable` · `DebugFocusRig` | 눈앞으로 당기기 · 비네트 · 문제 글 · 조작 안내 |
| **조준점** | `AimPanel` · `DebugInteractor` | 겨눈 것 이름·행동 문구 |

### 뼈대와 차림새 — 건드리지 않는 것

`VrPanel`(판 밑절미) · `UiTuning`(배율·거리) · `UiMode`·`UiModeWatcher`(PC/VR·F8) ·
`UiPointer`(마우스/컨트롤러 광선) · `UiWords`(조작 이름) ·
`UiSkin`(글꼴·색) · `InventorySkin`(한지·목재 절차 텍스처) · `NotePanel` · `FocusHintPanel`

### 갈아 끼우는 자리 — 인터페이스

`IUiItem` · `IUiItemSource` · `IUiItemUse` · `IDialogueBackend` · `IDialogueSpeaker` · `IVoiceTranscriber`

### 더미 — 아무것도 구현 안 해도 도는 것

`DummyItem` · `DummyItemSource`(물건 넷) · `DummyNpc`(정해진 대꾸) · `DummyProp`(퍼즐 얼개 시연)

### 글꼴 — 이미 들어 있다

`ChosunCentennial_ttf.ttf`(한글 궁서체) · `tkFangSong.ttf`(한자) ·
**구워 둔 TMP 에셋 두 벌**. 그래서 굽는 단계 없이 첫 화면부터 같아 보인다.

### 안 들어 있는 것

나침반(혼천의 전용) · 사건 상태(`GyeonuCase`) · 소지품 저장소 · 물건 정의 ·
대화 내용과 Gemini 연동 · 줍기(`ItemPickup`) — **전부 인터페이스 뒤로 물러났다.**

---

## 3. 필요한 것

| 패키지 | 왜 |
|---|---|
| **Input System** (`com.unity.inputsystem`) | 마우스·키보드·F8 |
| **TextMeshPro** | 모든 글자. Unity 6에서는 `com.unity.ugui` 에 딸려 온다 |
| **URP** (`Unity.RenderPipelines.Universal.Runtime`) | 3D 미리보기가 카메라 설정을 만진다 |

> **XR 쪽은 아무것도 필요 없다.** 컨트롤러는 유니티 내장 `UnityEngine.XR.InputDevices` 로 읽는다 —
> Meta SDK·OVR 플러그인에 기대지 않는다. VR을 안 켜면 마우스 쪽만 돈다.

---

## 4. 자기 시스템 잇기

**전부 선택이다.** 안 이으면 더미가 답하고 화면은 그대로 돈다.

### ① 소지품 — 물건과 저장소

```csharp
using IMUNROK.Ui;

// 물건: 판이 묻는 것은 이 아홉 가지뿐이다
public class 내물건 : ScriptableObject, IUiItem
{
    public string 이름, 설명;
    public GameObject 실물;

    public string Key => name;
    public string DisplayName => 이름;
    public string Description => 설명;
    public GameObject ModelPrefab => 실물;      // null 이면 조사 화면이 빈 막으로 뜬다
    public Vector3 PreviewEuler => new Vector3(15f, 25f, 0f);
    public float PreviewZoom => 1f;
    public bool ShowUseButton => false;
    public string UseLabel => "";
    public string UseNotReadyHint => "아직 여기서 쓸 수 없다.";
    public bool AutoShowOnPickup => true;
}

// 저장소: 목록과 두 이벤트만 있으면 된다
public class 내소지품 : IUiItemSource
{
    readonly List<IUiItem> 가진것 = new List<IUiItem>();
    public IReadOnlyList<IUiItem> Items => 가진것;
    public event Action Changed;
    public event Action<IUiItem> Added;

    public void 넣기(IUiItem it)
    {
        가진것.Add(it);
        Added?.Invoke(it);        // 획득 연출이 뜬다
        Changed?.Invoke();        // 판이 다시 그린다
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void 꽂기() { UiItems.Source = new 내소지품(); }   // ← 이 한 줄이 전부다
}
```

> ⚠️ **꽂는 일은 `RuntimeInitializeOnLoadMethod` 로 할 것.** 이 프로젝트는 도메인 리로드가
> 꺼져 있어 정적 값이 플레이 세션을 넘겨 살아남는다. 꾸러미가 세션 시작에 자리를 비우므로
> **세션마다 다시 꽂지 않으면 두 번째 Play 부터 소지품이 통째로 비어 보인다.**

### ② 「사용하기」 단추 — 안 이으면 눌러도 아무 일 없다

```csharp
public class 내쓰임 : IUiItemUse
{
    public string LabelFor(IUiItem item) => item.UseLabel;   // 상황따라 바뀌면 여기서 갈라라
    public bool Try(IUiItem item, Action closePanel)
    {
        if (item.Key != "지도") return false;                 // false → 물건의 안내 문구가 뜬다
        closePanel?.Invoke();                                 // 화면을 덮는 연출로 갈 때만
        지도펴기(); return true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void 꽂기() { UiItems.Use = new 내쓰임(); }
}
```

### ③ 대화 — 말 상대와 화자

```csharp
// 말 상대: LLM이든 대사표든 상관없다. 화면은 이 일곱만 안다
public class 내대화 : IDialogueBackend
{
    public string SpeakerName => "주모";
    public string CurrentLine { get; private set; } = "무슨 일이오?";
    public bool Busy { get; private set; }
    public event Action Changed;

    public void Ask(string 물음)
    {
        Busy = true; Changed?.Invoke();                    // 화면이 「…」를 띄우고 입력을 막는다
        내LLM.보내기(물음, 답 => { CurrentLine = 답; Busy = false; Changed?.Invoke(); });
    }
    public bool Present(IUiItem item)                      // false → 「이건 내밀 것이 못 된다」
    {
        CurrentLine = "…그것을 어디서."; Changed?.Invoke(); return true;
    }
    public IReadOnlyList<IUiItem> Presentables() => UiItems.Source.Items;
}

// 화자: 씬에 놓는 NPC
public class 내NPC : FocusInteractable, IDialogueSpeaker
{
    public DialogueLayout Layout => DialogueLayout.A_좌측판;
    public bool VoiceAutoSend => true;
    // FocusPoint 는 FocusInteractable 이 이미 준다

    public override string Prompt => "말 걸기";
    public override void HandleDrag(Vector2 delta) { }
    public override void Interact(GameObject actor) => DialogueUI.Ensure().Open(this, new 내대화());
}
```

### ④ 음성 입력 — 안 이으면 **음성 안내가 저절로 숨는다**

견우 구현이 이렇게 생겼다. 그대로 따라 하면 된다:

```csharp
public class 내받아쓰기 : IVoiceTranscriber
{
    public void Transcribe(MonoBehaviour host, byte[] wav,
                           Action<string> onText, Action<string> onError)
    {
        // wav = 16비트 PCM 모노. 녹음은 꾸러미의 VoiceInput 이 이미 해 준다.
        host.StartCoroutine(내STT에보내기(wav, onText, onError));
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void 꽂기() { UiDialogue.Voice = new 내받아쓰기(); }
}
```

안 꽂으면 대화창이 **「왼쪽 Ctrl — 누르고 말하기」 안내와 입력칸 안내글에서 목소리 이야기를 통째로 뺀다.**
있지도 않은 조작을 하라고 적어 두지 않는다.

> ⚠️ **VR + 받아쓰기 없음** 은 물을 방법이 아예 없는 조합이다(VR에는 칠 칸이 없다).
> VR을 쓸 생각이면 받아쓰기는 사실상 필수다.

### ⑤ 퍼즐 화면

`FocusInteractable` 을 상속하면 눈앞으로 당기기·비네트·조작 안내가 딸려 온다.
`DummyProp.cs` 가 가장 짧은 예다 — 그걸 본떠라.

---

## 5. 스타일 — 어디를 고치고 어디를 두는가

### 대화창 — 확정된 모습 (2026-08-27)

여느 게임처럼 **화면 아래에 가로로 눕힌 바**다. 어두운 바탕에 높은 투명도라 **NPC를 안 가린다.**

```
┌────────────────────────────────────────────────────────────────┐
│  ▌주모▐                                                     ×  │  ← 이름패 · 닫기 (창 안쪽)
│  ───────────────────────────────────────────────────────────  │  ← 구분선
│  아이고, 처음 뵙는 얼굴이네. 한양서 오셨다고?                  │  ← 대사 3줄
│                                                                │
│  [ 묻고 싶은 것을 치거나… ]      [마이크] [묻 기] [증거 제시]  │  ← 입력줄
│      Enter — 묻기   왼쪽 Ctrl — 누르고 말하기   Esc — 끝내기    │  ← 안내
└────────────────────────────────────────────────────────────────┘
```

| | PC | VR |
|---|---:|---:|
| 판 치수 | **2900 × 461** | **1500 × 535** |
| 거리 | 화면 붙박이 | **1.5 m** |
| 보이는 각 | — | 가로 53° · 세로 20° |
| 눈에서 아래로 | 화면 밑변에서 46px | **−16°** |
| 대사 / 이름 / 입력 / 안내 | 42 / 32 / 28 / 19 | 46 / 36 / 34 / 34 |

- **바탕은 먹빛 `#101116` · 투명도 65%** 로 굳었다 (`DialogueUI.BackRgb` · `BackAlpha`)
- **VR에서 글자를 키운 건 멋이 아니라 하한이다** — 1.5m 앞의 34단위 글씨가 **1.30°** 로,
  VR에서 읽히는 최소각이다. PC 값(19)을 그대로 쓰면 0.73° 라 **못 읽는다**
- **VR은 바를 좁힌다** (2900 → 1500). 2900을 1.5m 앞에 두면 가로 **88°** 로 시야를 통째로 덮는다
- **긴 대사는 잘라 버리지 않고 굴려 읽는다** — 3줄이 넘으면 휠·스틱으로 굴린다.
  대사 상자 높이는 **줄 높이의 정수배**로 잡는다(`LineBox`). 안 그러면 마지막 줄이 반만 보인다
- 마이크는 **글꼴 글리프가 아니라 그린 그림**이다 (`InventorySkin.Mic_`) — 🎤 는 두 글꼴 어디에도 없다

> ⚠️ **모드를 바꾸면 판을 다시 짓는다.** PC와 VR은 치수가 아예 다르다(2900↔1500).
> F8로 갈아탈 때 다시 안 지으면 PC 치수의 판이 VR 자리에 서서 시야를 덮는다.
> `DialogueUI` 가 `builtForVr` 로 알아서 한다 — **직접 부를 일은 없다.**

### 고쳐도 되는 것 (내 사건에 맞추는 값)

| 무엇 | 어디 |
|---|---|
| 판마다의 글·문구 | 각 화면의 문자열 |
| 미리 구울 글자 모음 | `UiFontBaker.ScanRoots` — **자기 폴더로 고칠 것** |
| 대화창 배치안 | `IDialogueSpeaker.Layout` — **기본값 `하단바_확정` 그대로 둘 것**. 나머지는 지난 시안이다 |

### 🚫 고치지 말 것 (통일해야 하는 것)

| 무엇 | 어디 | 왜 |
|---|---|---|
| **글꼴** | `UiSkin.Font` · `FontScale` | 사건마다 글씨체가 다르면 한 게임으로 안 보인다 |
| **색** | `UiSkin` · `InventorySkin` 의 색 상수 | 한지·먹·주칠이 이 게임의 색이다 |
| **절차 텍스처** | `InventorySkin` | 한지결·나뭇결이 판의 인상을 만든다 |
| **판 배율·거리** | `UiTuning` | VR에서 읽히는 크기를 실측으로 맞춘 값이다 |
| **판 치수** | `DialogueUI.GeomOf` · `InventoryUI` 상수 | 시야각 계산으로 잡은 값이다 |

**정말 바꿔야 하면 혼자 고치지 말고 팀에 알릴 것.** 고친 것은 다음 꾸러미에 반영해야 한다.

---

## 6. 함정 (전부 실제로 물린 것들)

### IMGUI · Overlay 캔버스는 HMD에 안 보인다
에디터에서 잘 보인다고 안심하지 말 것. 게임 뷰에서 멀쩡하던 `OnGUI` 안내가 헤드셋에서는 하나도 없었다.

### Interactable 은 콜라이더가 있는 오브젝트에 **직접** 붙여야 한다
부모에 붙이고 콜라이더는 자식에 있으면 `hit.collider.GetComponent<…>()` 가 **null** 이다.
꾸러미는 `GetComponentInParent` 로 찾지만, **콜라이더와 같은 오브젝트에 붙이는 것을 기본으로 삼을 것.**

### 정적(static) 값은 플레이 세션을 넘겨 살아남는다
도메인 리로드가 꺼진 프로젝트다. **텍스처 내용은 죽는데 참조는 남아** 두 번째 Play에서 판이
흰 사각형이 된다. 꾸러미는 `[RuntimeInitializeOnLoadMethod]` 로 세션마다 버린다.
**꽂는 쪽도 세션마다 다시 꽂아야 한다** (§4).

### MonoBehaviour 는 한 파일에 하나, 파일 이름 = 클래스 이름
안 지키면 씬에 붙인 것이 **「missing script」로 떨어져 나간다.** 예제 씬을 만들다 실제로 물렸다.

### TMP 의 `Truncate` 는 줄을 통째로 버린다
상자 높이에 온전히 안 들어가는 줄을 아예 안 그린다. 궁서체는 줄 높이가 글자 크기의 1.25배라
**1.5px 모자라 안내 줄이 통째로 사라졌다.** 판 글자 기본값은 `Overflow` 다 — 바꾸지 말 것.

### 한글 줄바꿈은 코드에서 켠다
TMP 기본값은 한글을 낱자 단위로 끊는다(`초점` / `을`). `UiSkin` 이 실행 중에
`TMP Settings` 의 `Use Modern Hangul Line Breaking Rules` 를 켠다 — 설정 파일에 기대지 않는다.

### 글꼴에 없는 글자가 많다
조선 궁서체·방송체 **어디에도** 없는 것: `✕ ▸ ‹ › ✓ ⚠ ✂ ↺ ≈ ‖` 와 **이모지 전부.**
화면용은 `× ▶ ◀` 와 낱말로 바꿨다. **새 기호를 쓰기 전에** 굽기 메뉴 로그의
「두 글꼴 어디에도 없는 글자」 줄을 볼 것.

### 아틀라스에 없던 글자는 첫 프레임에 네모로 번쩍인다
글꼴은 Dynamic 아틀라스다. 미리 안 구운 글자가 처음 뜨면 그 자리에서 굽는데, **굽는 한 프레임 동안
네모 덩어리로 번쩍인다.** 자기 사건 글자를 미리 구우려면 `UiFontBaker.ScanRoots` 를 고치고
`Tools ▸ 이문록 ▸ 글꼴 ▸ TMP 폰트 에셋 굽기` 를 누를 것.

### 후처리(톤매핑)는 꾸러미가 통일하지 못한다
월드 판은 톤매핑 **전에** 섞여 함께 눌린다. 그래서 **씬의 후처리 설정이 다르면 같은 판이 다른
밝기로 보인다.** UI 밖의 일이라 꾸러미로는 못 맞춘다 — 화면이 견우보다 어둡거나 밝으면
글꼴이 아니라 **씬의 Volume 설정**을 먼저 볼 것.

### 판 밑에 매달 때 부모 배율을 조심할 것
부모 스케일이 1이 아니면 판이 엉뚱한 크기로 뜬다. `VrPanel` 은 `lossyScale` 로 상쇄한다.

---

## 7. 눈(카메라)을 어디서 찾는가

`VrPanel` 과 `DebugToast` 가 이 순서로 찾는다:

1. `FindFirstObjectByType<DebugWalkController>()` → 그 `eye`
2. 없으면 **`Camera.main`**

**그래서 꾸러미의 워커를 안 써도 된다** — 자기 리그의 카메라에 `MainCamera` 태그만 있으면 돈다.
다만 `DebugWalkController.cs` **파일은 지우지 말 것** (타입 참조가 있어 컴파일이 깨진다).

워커를 쓰면 눈에 조준(`DebugInteractor`)과 소지품 입력(`InventoryInput`)이 저절로 붙는다.
그 밖에 붙일 것이 있으면 `DebugWalkController.EyeSetup` 에 등록하면 된다.

---

## 8. 알아 둘 것

- **예제 씬은 `Build Settings` 에 넣지 않는다.**
- 예제 씬은 `Assets/IMUNROK_UI/Sample/` 에 생긴다. 그 폴더는 꾸러미를 다시 구울 때 지워지므로,
  **씬을 고쳤다면 다른 곳으로 옮겨 둘 것.**
- 이 꾸러미를 **견우 프로젝트에 도로 임포트하지 말 것.** 사본은 이름 공간이 `IMUNROK.Ui` 라
  원본(`IMUNROK.Gyeonu`)과 나란히 서면 같은 이름의 클래스가 두 벌이 된다.
- 글꼴 원본 `.ttf` 는 저장소에 커밋하지 않는다(LFS 사용량 0 방침). **이 꾸러미가 곧 글꼴 전달본이다** —
  임포트하면 GUID까지 팀 전체가 같아진다.
- 고칠 것이 생기면 견우 담당자에게 알릴 것. 꾸러미는
  `Tools ▸ 이문록 ▸ UI 패키지 내보내기` 로 언제든 다시 구워진다.
