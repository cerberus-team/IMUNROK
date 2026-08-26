using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 대화창 배치안. <b>A로 확정</b>되었다 (2026-08-25). B·C는 견주어 본 안으로 남겨 둔다 —
    /// 나중에 다시 저울질할 일이 생기면 <c>Tools ▸ 이문록 ▸ 대화 검증</c> 으로 갈아 끼워 볼 수 있다.
    /// </summary>
    public enum DialogueLayout
    {
        /// <summary>
        /// <b>확정안</b> — 화면 아래 가로 바 (2026-08-27 확정).
        /// PC 2900×461 · VR 1500×535 · 먹빛 65%.
        /// 이름패 · 구분선 · 대사 · 입력줄 · 안내, 닫기 ×는 <b>창 안</b> 우측 상단.
        ///
        /// ⚠️ <b>번호가 0인 데 뜻이 있다.</b> 씬에 이미 놓인 NPC 스무 명이 전부 0으로 굳어 있다.
        ///    확정안에 0을 주면 <b>씬을 한 곳도 안 고치고</b> 전부 확정안으로 선다.
        ///    지난 시안들을 1부터 다시 매긴 것도 그래서다 — 어느 씬도 1~5를 쓰지 않아 안전하다.
        /// </summary>
        하단바_확정 = 0,

        // ── 지난 시안 (2026-08-25~26) ────────────────────────────
        //   확정 전에 눈으로 견주던 것들이다. 지우지 않고 남긴 이유는 하나 —
        //   「그때 그 배치가 나았나」를 다시 보려면 코드가 남아 있어야 한다.
        //   <b>새 NPC에 골라 붙일 것이 아니다.</b>

        /// <summary>A — 판을 왼쪽 아래에 세우고 NPC를 오른쪽에 둔다.</summary>
        A_좌측판 = 1,
        /// <summary>B — 화면 아래에 얇은 띠. NPC 전신이 거의 다 보인다.</summary>
        B_하단띠 = 2,
        /// <summary>C — NPC 대사는 얼굴 옆 말풍선, 내 입력·제시만 아래 작은 채팅바.</summary>
        C_말풍선 = 3,
        /// <summary>E — 하단 바 <b>얇게</b>. 3000×300, 대사 2줄. 화면을 가장 덜 막는다.</summary>
        E_하단바_얇게 = 4,
        /// <summary>F — 하단 바 <b>여유</b>. 2760×440, 대사 4줄. 긴 대사가 잘 읽힌다.</summary>
        F_하단바_여유 = 5,
    }

    /// <summary>
    /// 하단 바의 색 한 벌 (2026-08-27).
    ///
    /// ■ 왜 색까지 바꾸나
    ///   투명도만 올리면 <b>한지의 밝은 결이 그대로 비쳐</b> 뒤가 보이는데도 답답하다.
    ///   여느 게임의 대사창처럼 <b>어두운 바탕 + 높은 투명도 + 밝은 글씨</b>로 간다.
    ///   조선 배경이니 검정 대신 <b>먹빛·짙은 갈색</b>을 쓰고, 목재 테두리는 남긴다.
    ///
    /// ■ 글이 읽히는 선
    ///   어두운 바탕에서는 글씨를 밝게 뒤집어야 한다. 밝은 배경(낮 야외) 앞에서도
    ///   바탕이 충분히 어두워야 밝은 글씨가 뜬다 — 그래서 투명도의 하한이 생긴다.
    /// </summary>
    public struct BarPalette
    {
        public Color back;       // 뒤판 (알파 포함)
        public Color border;     // 테두리
        public Color text;       // 대사
        public Color dim;        // 조작 안내·녹음 문구
        public Color slotBack;   // 글쇠 칸 안쪽
        public Color slotText;   // 글쇠 칸에 친 글
        public Color slotHint;   // 글쇠 칸 안내글
        /// <summary>한지결을 남길지 — false 면 민판(단색)이라 가장 깔끔하다.</summary>
        public bool paperGrain;
    }

    /// <summary>
    /// 하단 바 한 벌의 수치 (2026-08-26 시안 → 08-27 확정). PC와 VR이 값만 다르다.
    /// 배치 코드는 하나뿐이고, 변형은 <b>이 값들만</b> 다르다 — 그래야 견주기가 공정하다.
    /// </summary>
    public struct BottomStyle
    {
        /// <summary>대사 자리의 높이(단위). <see cref="DialogueUI.LineBox"/> 가 줄 수로 계산해 준다.</summary>
        public float lineBoxH;
        public int lineSize;      // 대사 글자
        public int nameSize;      // 이름패 글자
        public int inputSize;     // 글쇠 칸·단추 글자
        public int footSize;      // 아래 조작 안내
        public float inputH;      // 입력줄 높이

        // ── 여백 (2026-08-27) ───────────────────────────────────────────────
        //   처음엔 자리마다 숫자를 흩어 놓았더니 <b>어디가 좁은지 눈으로만 알 수 있고</b>
        //   하나를 고치면 아래가 다 밀렸다. 이제 <b>사이 간격을 이름 붙여</b> 두고,
        //   판 높이(<see cref="DialogueUI.GeomOf"/>)를 이 값들의 <b>합으로 계산</b>한다.
        //   그래서 여백을 키우면 판이 딱 그만큼만 커진다 — 배치가 어긋나지 않는다.

        public float padX;          // 좌우 테두리 ↔ 내용
        public float padTop;        // 위 테두리 ↔ 이름패
        public float nameToRule;    // 이름패 ↔ 구분선
        public float ruleH;         // 구분선 두께
        public float ruleToLine;    // 구분선 ↔ 대사
        public float lineToInput;   // 대사 ↔ 글쇠 칸
        public float inputToFoot;   // 글쇠 칸 ↔ 아래 조작 안내
        public float footToEdge;    // 조작 안내 ↔ 아래 테두리
        public float footH;         // 조작 안내 줄 높이

        /// <summary>이름패 높이 — 글자에 위아래 여유를 붙인 것. 닫기 ×도 같은 높이로 맞춘다.</summary>
        public float NameH { get { return nameSize + 18f; } }

        /// <summary>
        /// 여백까지 더한 판 높이. 배치 코드와 <see cref="DialogueUI.GeomOf"/> 가 같은 값을 쓴다.
        /// 위에서 아래로 <b>머리(이름패·구분선) → 대사 → 입력줄 → 안내</b> 순으로 쌓은 합이다.
        /// </summary>
        public float TotalHeight
        {
            get
            {
                return padTop + NameH + nameToRule + ruleH + ruleToLine
                     + lineBoxH + lineToInput + inputH + inputToFoot + footH + footToEdge;
            }
        }
    }

    /// <summary>
    /// 대화창 (2026-08-25 개편) — 배치안 셋을 갈아 끼울 수 있는 한지 판.
    ///
    /// ■ 판은 <b>화면과 나란히</b> 선다 (2026-08-25 수정)
    ///   전에는 판을 18° 눕혀 놨더니 원근 때문에 사다리꼴로 일그러져 글을 읽기 불편했다.
    ///   렌즈 퍼즐이 같은 이유로 판 면에 수직으로 서듯(<see cref="LensPuzzle"/>),
    ///   여기서는 <b>판을 카메라 회전 그대로</b> 세우고 자리만 카메라의 위·오른쪽 축으로 밀어낸다.
    ///   그러면 판 면이 화면과 평행해져 어디에 놓든 <b>반듯한 직사각형</b>으로 보인다.
    ///   ⚠️ 자리를 '각도로 회전한 방향 × 거리'로 잡으면 안 된다 — 판 면까지의 거리가 달라져
    ///      가장자리 판이 작아 보인다. <c>forward*dist + up*tan(θ)*dist</c> 로 밀어야 한다.
    ///
    /// ■ 목소리로 묻기
    ///   <b>왼쪽 Ctrl</b> 을 누르고 있는 동안 녹음, 떼면 받아 적어 보낸다.
    ///   글쇠 칸이 잡혀 있으므로 <b>글자가 찍히는 키는 쓸 수 없다</b>(V를 쓰면 입력칸에 'v'가 남는다).
    ///   Ctrl은 글자를 만들지 않고, 왼손으로 누른 채 있기 편하며, 이 프로젝트에서 비어 있다.
    ///
    /// ■ 증거 제시는 <b>소지품 판</b>이 맡는다
    ///   전에는 대화창 안에 이름만 나열했다. 지금은 <see cref="InventoryUI.OpenForPresent"/> 로
    ///   소지품 판을 그대로 열어 물건을 보고 고른다. 정보 단서는 <see cref="DialogueSession"/> 이
    ///   임시 소지품으로 지어 함께 올린다.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        /// <summary>한 배치안의 판 치수와 화면상 자리. 각도는 시야 중심에서의 어긋남(°).</summary>
        public struct Geom
        {
            public float w, h, scale, dist, rightDeg, upDeg;
            public Geom(float w, float h, float scale, float dist, float rightDeg, float upDeg)
            { this.w = w; this.h = h; this.scale = scale; this.dist = dist; this.rightDeg = rightDeg; this.upDeg = upDeg; }
        }

        //  세로 화각 60°(±30°), 16:9 기준 가로 ±45.8° 안에 들어가게 잡은 값들.
        static Geom GeomOf(DialogueLayout l)
        {
            switch (l)
            {
                // 확정안 — 1.50×0.62m 판을 왼쪽 20°·아래 11°로 밀어 둔다.
                //   가로 −40.8°~+7.8° / 세로 −22.7°~+0.7° = **화면 왼쪽 아래 한 귀퉁이**.
                //   ⚠️ 처음엔 세로 1.00m(37°)였는데 판이 화면 왼쪽 절반을 통째로 차지해
                //      "상자가 너무 크다"는 지적을 받았다. 지금은 23°로 줄여 위쪽·오른쪽을 다 비운다.
                //      대신 대사가 길면 잘리므로 **틀 안에서 휠로 굴려 읽는다**(LineViewport).
                case DialogueLayout.A_좌측판: return new Geom(1500f, 620f, 0.001f, 1.5f, -20f, -11f);
                // 2.20×0.40m 띠 → 세로 −27.8°~−14.6°. 화면 아래 1/4만 덮는다.
                case DialogueLayout.B_하단띠: return new Geom(2200f, 400f, 0.001f, 1.5f, 0f, -21.5f);
                // 1.90×0.26m 채팅바 → 세로 −28.5°~−20.3°. 대사는 말풍선이 맡는다.
                case DialogueLayout.C_말풍선: return new Geom(1900f, 260f, 0.001f, 1.5f, 0f, -24.5f);

                // ── 하단 가로 바 (2026-08-26) ────────────────────────────
                //   1.5 m 앞, 세로 화각 60° 기준으로 화면 반높이가 866단위, 16:9 반너비가 1540단위다.
                //   바를 화면 <b>아래 끝에서 46단위 띄워</b> 눕히려면 중심 y = −(866 − 높이/2 − 46).
                //   그 y를 각도로 옮긴 것이 upDeg = atan(y·0.001 / 1.5) 다.
                //   너비는 화면 폭(3080)의 90~97% — "거의 전체 폭"이라는 요구를 그렇게 잡았다.
                //   높이는 <see cref="BottomStyle.TotalHeight"/> 가 여백까지 더해 계산한다 —
                //   여백을 벌리면 판이 딱 그만큼만 커지고, 아래 끝에서 띄우는 거리는 그대로다.
                case DialogueLayout.하단바_확정: return BarGeom(2900f, DialogueLayout.하단바_확정);
                case DialogueLayout.E_하단바_얇게: return BarGeom(3000f, DialogueLayout.E_하단바_얇게);
                default: return BarGeom(2760f, DialogueLayout.F_하단바_여유);
            }
        }

        /// <summary>
        /// 하단 바의 치수를 짓는다 — 높이는 여백의 합, 자리는 <b>화면 아래 끝에서 46 띄운 곳</b>.
        ///
        /// 1.5 m 앞, 세로 화각 60° 기준으로 화면 반높이가 866단위다. 바 중심을
        /// <c>−(866 − 높이/2 − 46)</c> 에 두면 아래 여백이 늘 46으로 일정하다.
        /// 여백을 벌려 판이 커져도 <b>바닥에서 뜬 거리는 안 변한다</b> — 위로만 자란다.
        /// </summary>
        /// <summary>
        /// VR에서 하단 바의 <b>폭</b> (단위). PC의 2900을 그대로 쓰면 안 된다 (2026-08-27).
        ///
        /// 2.9 m 짜리 판이 1.5 m 앞에 서면 <b>가로 88°</b> 다 — 양 끝을 보려고 고개를 돌려야 한다.
        /// 1500 이면 1.5 m → <b>53°</b> 로, 눈만 굴려 훑을 수 있는 범위에 들어온다.
        /// (좌측 판이 VR에서 68°였고 그것도 "겨우"였다 — 그보다 넉넉하게 잡았다.)
        ///
        /// 폭이 줄면 한 줄에 담기는 글자가 줄어 <b>줄 수를 늘려</b> 메운다 (아래 VR 수치 참고).
        /// </summary>
        const float VrBarWidth = 1500f;

        /// <summary>
        /// VR에서 하단 바가 눕는 높이(도). 판 반높이가 약 10°이므로 −16°면 위끝 −6°, 아래끝 −26° —
        /// 아래를 보되 고개를 숙일 정도는 아닌 자리다. PC(−23°)보다 조금 올려 잡았다:
        /// HMD는 아래쪽 시야가 화면보다 좁다.
        /// </summary>
        const float VrBarUpDeg = -16f;

        static Geom BarGeom(float w, DialogueLayout l)
        {
            float h = StyleOf(l).TotalHeight;

            // VR — 좁게 짓고 각도로 눕힌다 (판 자체를 VR용 치수로 만드는 것이 요점이다.
            //      PC 판을 배율로 키우거나 줄이면 글자와 판의 비율이 어긋난다.)
            if (UiModes.IsVr) return new Geom(VrBarWidth, h, 0.001f, 1.5f, 0f, VrBarUpDeg);

            // PC — 화면 아래 끝에서 46 띄운 자리. 여백을 벌려 판이 커져도 이 거리는 안 변한다.
            float y = -(866f - h * 0.5f - 46f);                      // 단위
            float upDeg = Mathf.Atan2(y * 0.001f, 1.5f) * Mathf.Rad2Deg;
            return new Geom(w, h, 0.001f, 1.5f, 0f, upDeg);
        }

        /// <summary>
        /// <b>대사 상자를 줄 높이의 정수배로</b> 잡는다 (2026-08-27).
        ///
        /// ⚠️ 안 맞추면 틀(<see cref="RectMask2D"/>)이 <b>줄 한복판을 가로로 잘라</b> 글자가
        ///    반토막 난 채 남는다 — 고장난 것처럼 보인다 (긴 대사에서 실측).
        ///
        /// 한 줄이 차지하는 높이 = 글자크기 × (<b>1.25</b> + <b>0.28</b>) 이다.
        ///   · 1.25 — 조선 궁서체의 줄 높이 비율 (얼굴 지표 60 ÷ 표본 48)
        ///   · 0.28 — 우리가 얹은 줄 간격 (<c>UiSkin.LineSpacing(1.28f)</c> 은 글자크기의 28%를 더한다)
        /// 둘을 더해야 한다 — 줄 간격은 줄 높이에 <b>곱하는 것이 아니라 더하는 것</b>이다.
        /// (여기를 곱셈으로 잘못 잡아 3줄짜리 상자가 2.5줄만 담고 있었다.)
        /// </summary>
        static float LineBox(int lineSize, int lines)
        {
            const float PerLine = 1.25f + 0.28f;
            return Mathf.Ceil(lineSize * PerLine * lines) + 6f;   // 6 = 첫 줄 윗여유
        }

        /// <summary>배치안·모드별 수치. 얼개는 하나, 값만 다르다.</summary>
        static BottomStyle StyleOf(DialogueLayout l)
        {
            switch (l)
            {
                // 대사 자리 높이 = 글자크기 × 1.25(궁서체 줄높이) × 줄 수 + 여유 6
                case DialogueLayout.E_하단바_얇게:   // 2줄
                    return new BottomStyle { lineBoxH = LineBox(38, 2), lineSize = 38,
                                             nameSize = 28, inputSize = 26, footSize = 18, inputH = 56f,
                                             padX = 76f, padTop = 12f, nameToRule = 8f, ruleH = 3f,
                                             ruleToLine = 20f, lineToInput = 28f,
                                             inputToFoot = 18f, footToEdge = 16f, footH = 22f };
                case DialogueLayout.F_하단바_여유:   // 4줄
                    return new BottomStyle { lineBoxH = LineBox(46, 4), lineSize = 46,
                                             nameSize = 34, inputSize = 31, footSize = 21, inputH = 70f,
                                             padX = 100f, padTop = 18f, nameToRule = 12f, ruleH = 3f,
                                             ruleToLine = 30f, lineToInput = 42f,
                                             inputToFoot = 28f, footToEdge = 24f, footH = 26f };
                case DialogueLayout.하단바_확정 when UiModes.IsVr:
                    // ── VR 확정안 (2026-08-27) ───────────────────────────────────────
                    //   구성·색·투명도는 PC와 <b>똑같다</b>. 달라지는 것은 <b>치수</b>뿐이다.
                    //
                    //   ⚠️ PC 글자 크기를 그대로 쓰면 안 된다. 1.5 m 앞에서 19단위 안내줄은
                    //      <b>0.73°</b> 인데, VR에서 편히 읽히는 하한이 약 1.3° 다 —
                    //      헤드셋에서는 "글자가 있다"는 것만 알고 못 읽는 크기다.
                    //      그래서 작은 글씨부터 키웠다: 안내 19→34 (1.30°), 입력 28→34,
                    //      이름 32→36, 대사 42→46 (1.76°).
                    //   ⚠️ 판이 좁아져(2900→1500) 한 줄에 담기는 글자가 줄었다 — 줄 수는 3줄 그대로 두되
                    //      글자가 커진 만큼 상자도 커진다. 긴 대사는 PC보다 더 자주 굴려 읽게 된다.
                    //   ⚠️ <b>헤드셋으로 검증하지 못했다.</b> 계산으로 잡은 값이다.
                    return new BottomStyle { lineBoxH = LineBox(46, 3), lineSize = 46,
                                             nameSize = 36, inputSize = 34, footSize = 34, inputH = 76f,
                                             padX = 60f, padTop = 18f, nameToRule = 12f, ruleH = 3f,
                                             ruleToLine = 28f, lineToInput = 40f,
                                             inputToFoot = 26f, footToEdge = 24f, footH = 36f };

                default:                             // D — 보통, 3줄 · 확정안 (PC)
                    // 2026-08-27 (2차): <b>좌측 판(A안)의 구성을 그대로 옮겼다.</b>
                    //   A안 세로 배열: 이름패(창 안 좌상) → 구분선 → 대사 → 입력줄 → 안내,
                    //   닫기 ×는 창 <b>안</b> 우상. 그 순서와 켜 나눔을 여기 그대로 가져온다.
                    //
                    //   ⚠️ A안의 간격을 <b>숫자 그대로</b>는 못 옮긴다. A안 판은 620이 세로인데
                    //      바는 461이다 — 그대로 넣으면 530이 넘어 안내줄이 판 밖으로 나간다.
                    //      그래서 <b>순서와 비율은 지키고 값만 눌러</b> 담았다 (아래 합이 정확히 461).
                    //      14 + 50 + 10 + 3 + 24 + 199 + 34 + 62 + 22 + 24 + 19 = 461
                    return new BottomStyle { lineBoxH = LineBox(42, 3), lineSize = 42,
                                             nameSize = 32, inputSize = 28, footSize = 19, inputH = 62f,
                                             padX = 90f, padTop = 14f, nameToRule = 10f, ruleH = 3f,
                                             ruleToLine = 24f, lineToInput = 34f,
                                             inputToFoot = 22f, footToEdge = 19f, footH = 24f };
            }
        }

        // ── 확정된 색 (2026-08-27) ────────────────────────────────────────────
        //
        // 시안 때는 색조·투명도를 정적 손잡이로 빼 두고 아홉 조합을 견주었다.
        // <b>먹빛 65% 로 확정되어 손잡이는 지웠다</b> — 코드에 남겨 두면 나중에 누가 건드린다.
        // 다시 견주어야 할 일이 생기면 아래 두 상수만 바꾸면 된다.

        /// <summary>뒤판 불투명도. 낮은 쪽이 많이 비치지만 낮 야외에서 글이 흐려진다 —
        /// 0.50 이 하한, 0.80 이면 거의 안 비친다. 두 조명에서 재어 0.65 로 정했다.</summary>
        const float BackAlpha = 0.65f;

        /// <summary>먹빛 — 푸른 기가 도는 검정. 낮 야외·밤 실내 양쪽에서 밝은 글씨가 뜬다.</summary>
        static readonly Color BackRgb = new Color(0.062f, 0.066f, 0.086f);

        /// <summary>확정된 색 한 벌.</summary>
        public static BarPalette Palette()
        {
            const float a = BackAlpha;
            var back = new Color(BackRgb.r, BackRgb.g, BackRgb.b, a);

            return new BarPalette
            {
                back = back,
                border = new Color(0.44f, 0.35f, 0.25f, Mathf.Clamp01(a + 0.22f)),
                text = new Color(0.945f, 0.925f, 0.870f),
                dim = new Color(0.82f, 0.79f, 0.73f, 0.78f),
                // 글쇠 칸은 뒤판보다 <b>조금 더 짙고 조금 더 불투명</b>하게 — 칠 수 있는 자리임을 알린다
                slotBack = new Color(back.r * 0.55f, back.g * 0.55f, back.b * 0.55f, Mathf.Clamp01(a + 0.16f)),
                slotText = new Color(0.945f, 0.925f, 0.870f),
                slotHint = new Color(0.945f, 0.925f, 0.870f, 0.42f),
                paperGrain = false,   // 한지결은 투명해도 답답해 보여 뺐다 (시안에서 견주어 확인)
            };
        }

        static bool IsBottomBar(DialogueLayout l)
        {
            return l == DialogueLayout.하단바_확정
                || l == DialogueLayout.E_하단바_얇게
                || l == DialogueLayout.F_하단바_여유;
        }

        /// <summary>말풍선 치수 (C안). 캔버스 단위 × 0.001 = m.</summary>
        const float BubbleW = 900f, BubbleH = 420f, BubbleScale = 0.001f;

        /// <summary>
        /// VR에서 판 전체에 곱하는 배율 (2026-08-26).
        ///
        /// 왜 필요한가: A안의 <b>안내 줄과 「더 있음」 표시가 22단위 = 0.84°</b> 다.
        /// VR에서 편히 읽히는 하한이 약 1.3°이고 1°는 사실상 못 읽는 크기다.
        /// 글자만 키우면 줄 높이를 넘쳐 배치가 깨지므로 <b>판을 통째로</b> 키운다 —
        /// 글자·상자·여백이 함께 커져 A안 배치가 그대로 유지된다.
        ///
        /// 1.35 를 고른 이유: 판이 1.5 m 앞에서 53°×24° → <b>68°×32°</b> 가 된다.
        /// 가운데에 두면 좌우 ±34° 로, 눈만 굴려 훑을 수 있는 범위 안에 겨우 들어온다.
        /// 1.5 로 올리면 76°가 되어 모서리를 보려고 고개를 돌려야 한다.
        /// ⚠️ 계산으로 잡은 값이다 — 헤드셋에서 22단위 줄이 실제로 읽히는지 확인할 것.
        /// </summary>
        const float VrScale = 1.35f;

        public static DialogueUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public bool IsOpen { get; private set; }
        public DialogueHotspot Hovered { get; private set; }
        public DialogueLayout Layout { get; private set; } = DialogueLayout.하단바_확정;

        /// <summary>소지품 판(증거 고르기)이 떠 있는가 — Esc가 어디로 갈지를 이 값이 정한다.</summary>
        public bool IsPresenting => InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;

        readonly InventorySkin skin = new InventorySkin();
        readonly VoiceInput voice = new VoiceInput();

        IDialogueSpeaker owner;
        IDialogueBackend session;
        Transform eye;

        /// <summary>말 상대의 <b>보이는 몸</b>. 판을 그 앞으로 당길 때 쓴다 — <see cref="ApplyPose"/> 참고.
        /// 프레임마다 찾지 않도록 <see cref="Open"/> 에서 한 번만 모은다.</summary>
        Renderer[] speakerBody;
        Geom geom;
        bool built;
        /// <summary>지을 때의 모드. 하단 바는 PC와 VR의 <b>치수가 아예 다르므로</b>
        /// F8로 모드를 바꾸면 다시 지어야 한다 (2026-08-27).</summary>
        bool builtForVr;

        TMP_FontAsset font;
        RectTransform root;                    // 판 본체 (캔버스 = 이 오브젝트)
        TextMeshProUGUI nameText, lineText, hintText, voiceText, overflowHint;
        RectTransform cursor, voiceBar, voiceBarFill;
        RectTransform lineViewport, lineRect;   // 대사 — 틀 안에서 굴려 읽는다
        string shownLine = "";
        bool following;                 // VR — 판이 지금 시야를 따라가는 중인가
        float lineScroll;
        float scrollReadyAt;
        TMP_InputField field;
        TextMeshProUGUI placeholder;              // 칸이 비었을 때 뜨는 안내 — 모드에 따라 문구가 달라진다
        DialogueHotspot askSpot, presentSpot, closeSpot;
        TextMeshProUGUI askLabel;

        // 하단 바의 「말하기」 단추 — 누르고 있는 동안 녹음한다 (다른 배치안에는 없다)
        DialogueHotspot micSpot;
        TextMeshProUGUI micLabel;
        RectTransform micIcon;

        // C안 — 말풍선 (자기 캔버스를 따로 가진다: 월드에서 NPC 곁에 떠 있어야 한다)
        GameObject bubbleGo;
        RectTransform bubbleRoot, bubbleTail;
        TextMeshProUGUI bubbleName, bubbleLine;

        bool eventSystemMine;
        bool prevNavigation;
        bool voiceBusy;

        // ─────────────────────────────────────────────────────────
        public static DialogueUI Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("대화_판");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DialogueUI>();
            go.SetActive(false);
            return Instance;
        }

        public void Open(IDialogueSpeaker npc, IDialogueBackend s)
        {
            owner = npc;
            session = s;
            // 말 상대의 몸 — 판이 그 뒤로 들어가지 않게 (ApplyPose)
            var speakerGo = npc as Component;
            speakerBody = speakerGo != null ? speakerGo.GetComponentsInChildren<Renderer>() : null;
            eye = Camera.main != null ? Camera.main.transform : null;
            if (eye == null) { Debug.LogError("[대화] Camera.main이 없다 — 대화창을 세울 수 없다."); return; }

            // ⚠️ 모드가 바뀌었으면 다시 짓는다 — 하단 바는 PC와 VR의 <b>치수가 아예 다르다</b>
            //    (폭 2900↔1500, 글자 크기도 다르다). 안 다시 지으면 F8로 모드를 바꿨을 때
            //    PC 치수의 판이 VR 자리에 그대로 서서 시야를 통째로 덮는다.
            if (!built || Layout != npc.Layout || builtForVr != UiModes.IsVr) Rebuild(npc.Layout);

            gameObject.SetActive(true);
            if (bubbleGo != null) bubbleGo.SetActive(Layout == DialogueLayout.C_말풍선);
            ApplyPose();
            IsOpen = true;

            session.Changed += Refresh;
            EnsureEventSystem();
            if (EventSystem.current != null)
            {
                prevNavigation = EventSystem.current.sendNavigationEvents;
                // ⚠️ 방향키·WASD가 선택을 옮기면 글을 치다 말고 칸이 풀린다. 대화 중에는 끈다.
                EventSystem.current.sendNavigationEvents = false;
            }

            ApplyInputRowMode();
            hintText.text = HintLine;
            shownLine = "";              // 지난 대화의 마지막 줄을 물려받지 않게
            lineScroll = 0f;
            Refresh();
            field.text = "";
            ActivateField();
        }

        /// <summary>글쇠 칸을 잡는다. VR에는 칠 칸이 없으므로 아무 일도 하지 않는다.</summary>
        void ActivateField()
        {
            if (UiModes.IsPc && field != null && field.gameObject.activeInHierarchy)
                field.ActivateInputField();
        }

        public void Close()
        {
            if (!IsOpen) return;
            voice.Cancel();
            voiceBusy = false;
            if (session != null) session.Changed -= Refresh;
            if (field != null) { field.text = ""; field.DeactivateInputField(); }
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = prevNavigation;
            IsOpen = false;
            Hovered = null;
            owner = null;
            session = null;
            speakerBody = null;
            bodySampleCount = 0;
            bodyBoxAt = -999f;
            if (bubbleGo != null) bubbleGo.SetActive(false);
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (eventSystemMine && EventSystem.current != null) Destroy(EventSystem.current.gameObject);
            skin.Dispose();
            if (bodyScratch != null) { Destroy(bodyScratch); bodyScratch = null; }
            if (Instance == this) Instance = null;
        }

        // ── 자리 ─────────────────────────────────────────────
        /// <summary>
        /// 판을 <b>화면과 나란히</b> 세운다. 회전은 카메라 그대로, 자리만 카메라 축으로 민다 —
        /// 그래야 어디에 놓아도 반듯한 직사각형으로 보인다.
        /// </summary>
        void ApplyPose()
        {
            if (eye == null) return;

            // ── 판이 서는 방향 ──
            // PC : 카메라 회전 그대로 = 화면에 붙박이. 지금까지의 사용감을 그대로 둔다.
            // VR : 죽은 구간 + 지연으로 시야를 느슨히 따라간다.
            //   ⚠️ VR에서 붙박이로 두면 안 된다 — 판이 시야 왼쪽 −20°에 **영원히 붙어 있어**
            //      고개를 돌려도 정면으로 가져올 수가 없다. 눈만 굴려 읽어야 하는 자리다.
            //      데드존을 두면 판이 시야 가운데로 천천히 따라와, 보려고 하면 정면에 온다.
            //      (소지품 판이 같은 이유로 이미 이 방식을 쓴다)
            Quaternion rot;
            if (UiModes.IsVr)
            {
                float off = Quaternion.Angle(transform.rotation, eye.rotation);
                if (off > UiTuning.VrFollowDeadZone) following = true;
                else if (off < UiTuning.VrFollowDeadZone * 0.35f) following = false;
                rot = following
                    ? Quaternion.Slerp(transform.rotation, eye.rotation,
                                       1f - Mathf.Exp(-Time.unscaledDeltaTime / UiTuning.VrFollowLag))
                    : transform.rotation;
                if (!IsOpen) rot = eye.rotation;      // 막 열렸으면 즉시 정면에
            }
            else rot = eye.rotation;

            // ── 화면 안에서의 자리 ──
            //
            // VR은 가로 치우침을 없앤다 — 렌즈 주변부는 흐리고 눈을 크게 굴려야 한다.
            //
            // ⚠️ 세로 치우침을 −12°로 <b>묶던 줄을 하단 바에서는 풀었다</b> (2026-08-27 확정).
            //    그 묶음은 좌측 판(A안, −11°)을 위한 것이었다. 하단 바는 −23° 언저리에 눕는 것이
            //    설계인데 −12°로 끌어올리면 <b>화면 한가운데로 올라와 NPC를 가린다</b> —
            //    "아래에 낮게 깔아 NPC를 안 가린다"는 이 배치의 이유가 통째로 사라진다.
            //    바는 VR에서도 제 각도로 눕히고, 대신 <b>판을 좁게</b> 만들어(BarGeom) 시야에 담는다.
            bool bar = IsBottomBar(Layout);
            float rightDeg = UiModes.IsVr ? 0f : geom.rightDeg;
            float upDeg = UiModes.IsVr && !bar ? Mathf.Max(geom.upDeg, -12f) : geom.upDeg;
            float scaleMul = UiModes.IsVr && !bar ? VrScale : 1f;   // 바는 치수 자체를 VR용으로 짓는다

            float dist = geom.dist;

            // ── 앞을 막은 것 피하기 ──
            //
            // ⚠️ 예전에는 <b>가운데로 광선 하나</b>만 쐈다. 좌측 판(1500)에서는 그럭저럭 맞았지만
            //    폭 2900짜리 하단 바에서는 <b>옆에 있는 담·가구를 통째로 못 본다</b> —
            //    실제로 낮 마을에서 앞의 돌담이 바 왼쪽을 잘라 대사가 가려졌다.
            //    판의 네 귀퉁이까지 재고 가장 가까운 것에 맞춘다 (VrPanel 이 쓰는 것과 같은 수법).
            //    당겨 온 만큼 배율도 함께 줄이므로 보이는 각은 그대로다.
            Vector3 ahead = rot * Vector3.forward;
            float halfW = geom.w * 0.5f * geom.scale * scaleMul;
            float halfH = geom.h * 0.5f * geom.scale * scaleMul;
            float upOff = geom.dist * Mathf.Tan(upDeg * Mathf.Deg2Rad);
            float rightOff = geom.dist * Mathf.Tan(rightDeg * Mathf.Deg2Rad);
            for (int i = 0; i < 5; i++)
            {
                float sx = i == 0 ? 0f : (i == 1 || i == 3 ? -1f : 1f);
                float sy = i == 0 ? 0f : (i <= 2 ? 1f : -1f);
                Vector3 local = new Vector3(rightOff + sx * halfW, upOff + sy * halfH, geom.dist);
                Vector3 dir = (rot * local).normalized;
                RaycastHit h2;
                if (!Physics.Raycast(eye.position, dir, out h2, geom.dist * 1.3f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                // 비스듬한 광선의 거리를 판 면까지의 **수직** 거리로 환산한다
                float along = h2.distance * Vector3.Dot(dir, ahead);
                dist = Mathf.Min(dist, along - 0.08f);
            }

            // ── 말 상대의 몸 앞으로 ──
            //
            // ⚠️ 위의 광선만으로는 <b>말 상대 자신</b>을 못 피한다 (2026-08-27 어머니에서 실측).
            //    까닭이 둘이다.
            //      ① 광선이 맞히는 것은 <b>조준 캡슐</b>인데 그 캡슐은 보이는 몸보다 훨씬 작다.
            //         앉은 어머니는 치마·무릎이 앞으로 0.5 m 나와 있는데 캡슐(반지름 0.40)의
            //         앞면은 1.30 m 였다 — 캡슐을 피해도 무릎에 가린다.
            //      ② 폭 2900짜리 하단 바는 귀퉁이 광선이 ±44°로 <b>옆으로 날아가</b> 눈앞의
            //         사람을 아예 지나친다. 가운데 광선은 −23°로 발밑을 짚는다.
            //    실측: 눈에서 어머니 몸 앞면까지 0.50 m 인데 판은 1.454 m 에 섰다 —
            //    <b>말을 거는 상대보다 1 m 뒤</b>다. 그래서 치마·툇마루·댓돌·기둥이 바를 잘랐다.
            //
            //    보이는 몸의 AABB 앞면까지로 깊이를 묶는다. 조준 캡슐이 아니라 <b>렌더러</b>를
            //    보므로 자세가 어떻든 따라온다. 서 있는 사람은 몸이 얇아 거의 안 물린다.
            //
            // ⚠️ 이것은 <b>배치가 아니라 깊이</b>만 바꾼다. 아래 <c>k</c> 가 자리·배율을 같은
            //    비율로 줄이므로 화면에 보이는 크기·자리는 <b>한 픽셀도 달라지지 않는다.</b>
            //    (당겨 온 만큼 배율을 줄이는 것은 이 판이 원래 쓰던 수법이다)
            float body = SpeakerFrontDepth(eye.position, ahead);
            if (body > 0f) dist = Mathf.Min(dist, body - BodyClearance);

            dist = Mathf.Max(0.35f, dist);

            float k = dist / geom.dist;
            Vector3 pos = eye.position
                        + ahead * dist
                        + (rot * Vector3.up) * (upOff * k)
                        + (rot * Vector3.right) * (rightOff * k);

            transform.SetPositionAndRotation(pos, rot);
            transform.localScale = Vector3.one * geom.scale * k * scaleMul;

            if (bubbleGo != null && bubbleGo.activeSelf) PlaceBubble();
        }

        /// <summary>말 상대의 몸과 판 사이에 두는 틈(m). 자세가 흔들려도 파고들지 않을 만큼.</summary>
        const float BodyClearance = 0.12f;

        Mesh bodyScratch;
        readonly List<Vector3> bodyRaw = new List<Vector3>();
        Vector3[] bodySamples;
        int bodySampleCount;
        float bodyBoxAt = -999f;

        /// <summary>몸을 다시 재는 간격(초). 대화 중 자세는 거의 안 변하니 이 정도면 넉넉하다.</summary>
        const float BodyRefresh = 0.5f;

        /// <summary>몸에서 뽑아 둘 점의 수. 프레임마다 이만큼만 내적하면 되니 값이 싸다.
        /// ⚠️ 256으로는 <b>가장 앞으로 나온 꼭짓점을 놓친다</b> — 아이01에서 실측 오차 0.14 m 라
        ///    여유(0.12)를 다 까먹고 몸이 판보다 2 cm 앞에 섰다. 1024면 오차가 3 cm 안쪽이다.</summary>
        const int BodySampleMax = 1024;

        /// <summary>
        /// 말 상대의 <b>보이는 몸</b>이 시선 방향으로 얼마나 앞에서 시작하는가(m).
        /// 잴 몸이 없으면 0 (묶지 않는다).
        ///
        /// ⚠️ <see cref="Renderer.bounds"/> 를 쓰면 안 된다 (2026-08-27 실측). 이 모델들의
        ///    렌더러 AABB는 <b>바인드 자세 기준이라 터무니없이 크다</b> — 키 1.7 m 인 주모가
        ///    2.38 × 2.22 × 2.26 m 로 잡힌다. 그 상자로 앞면을 구하면 값이 <b>음수</b>가 되어
        ///    (주모 −0.29, 아이03 −0.32) 묶는 일이 통째로 없던 일이 된다 —
        ///    가장 필요한 순간에 조용히 꺼지는 종류의 결함이다.
        ///    <see cref="SkinnedMeshRenderer.BakeMesh"/> 로 <b>지금 자세</b>를 구워서 잰다.
        ///
        /// ⚠️ 구운 것의 <b>AABB</b> 로도 안 된다. 서 있는 주모의 구운 상자가 1.77 × 1.84 × 1.75 —
        ///    폭이 1.8 m 다. 축에 정렬된 상자라 몸보다 한참 부풀어, 앞면이 0.21 m 로 나온다
        ///    (실제 몸은 그보다 훨씬 뒤에 있다). 그러면 아무 이유 없이 판을 바닥값까지 당긴다.
        ///    그래서 <b>구운 꼭짓점 자체</b>를 (솎아서) 들고 시선축 최소값을 낸다.
        /// </summary>
        float SpeakerFrontDepth(Vector3 from, Vector3 ahead)
        {
            if (Time.unscaledTime - bodyBoxAt > BodyRefresh) { bodyBoxAt = Time.unscaledTime; RefreshSpeakerBody(); }
            if (bodySampleCount == 0) return 0f;

            float near = float.MaxValue;
            for (int i = 0; i < bodySampleCount; i++)
            {
                Vector3 v = bodySamples[i] - from;
                float d = v.x * ahead.x + v.y * ahead.y + v.z * ahead.z;
                if (d < near) near = d;
            }
            return near;
        }

        /// <summary>지금 자세를 구워 <see cref="BodySampleMax"/> 개의 월드 점으로 솎아 둔다.</summary>
        void RefreshSpeakerBody()
        {
            bodySampleCount = 0;
            if (speakerBody == null) return;
            if (bodyScratch == null) bodyScratch = new Mesh { name = "대화_상대몸_임시" };
            if (bodySamples == null) bodySamples = new Vector3[BodySampleMax];

            for (int r = 0; r < speakerBody.Length && bodySampleCount < BodySampleMax; r++)
            {
                var skin = speakerBody[r] as SkinnedMeshRenderer;
                if (skin == null || !skin.enabled || skin.sharedMesh == null
                    || !skin.gameObject.activeInHierarchy) continue;

                // ⚠️ useScale 은 켠다 — 끄면 FBX 의 cm 단위 그대로라 100배가 된다.
                //    (스킨 트랜스폼 배율 100은 TransformPoint 가 마저 곱한다)
                skin.BakeMesh(bodyScratch, true);
                bodyScratch.GetVertices(bodyRaw);          // 목록을 돌려 써서 매번 배열을 만들지 않는다
                if (bodyRaw.Count == 0) continue;

                int step = Mathf.Max(1, bodyRaw.Count / Mathf.Max(1, BodySampleMax - bodySampleCount));
                var tr = skin.transform;
                for (int i = 0; i < bodyRaw.Count && bodySampleCount < BodySampleMax; i += step)
                    bodySamples[bodySampleCount++] = tr.TransformPoint(bodyRaw[i]);
            }
        }

        /// <summary>말풍선을 NPC 얼굴 <b>왼쪽 위</b>에 띄운다. 판과 같이 화면과 나란히 세운다.</summary>
        void PlaceBubble()
        {
            if (owner == null || eye == null) return;
            Vector3 head = owner.FocusPoint + Vector3.up * 0.22f;
            // NPC에서 화면 왼쪽으로 밀어낸다 — 얼굴을 가리지 않게
            Vector3 pos = head - eye.right * 0.62f;
            // 카메라와의 거리를 판보다 조금 뒤로 둬서 판과 겹쳐도 앞뒤가 헷갈리지 않게 한다
            bubbleGo.transform.SetPositionAndRotation(pos, eye.rotation);
            bubbleGo.transform.localScale = Vector3.one * BubbleScale;
            // 꼬리는 NPC 쪽(오른쪽 아래)으로 절반쯤 튀어나오게 둔다 — 안쪽에만 있으면 바탕에 묻힌다.
            if (bubbleTail != null)
                bubbleTail.anchoredPosition = new Vector2(BubbleW * 0.5f + 6f, -BubbleH * 0.5f + 56f);
        }

        void LateUpdate()
        {
            if (!IsOpen) return;
            ApplyPose();

            var kb = Keyboard.current;
            if (kb == null || session == null) return;

            // 소지품 판(증거 고르기)이 떠 있는 동안에는 입력을 통째로 그쪽에 내준다.
            if (IsPresenting)
            {
                if (field.isFocused) field.DeactivateInputField();
                voice.Cancel();
                return;
            }

            // 모드가 Play 중에 바뀔 수 있다 (F8) — 칸과 안내 줄을 그때그때 맞춘다
            ApplyInputRowMode();
            if (hintText != null && hintText.text != HintLine) hintText.text = HintLine;

            // ── 목소리 (PC: 왼쪽 Ctrl / VR: 그립 — 누르고 말하기) ──
            HandleVoice();

            // ── 글쇠 칸 되살리기 ──
            // 빈 곳 클릭 한 번으로 선택이 풀린다. 대화 화면에서는 늘 잡혀 있어야 한다.
            if (!voice.Recording && !voiceBusy && !field.isFocused && !session.Busy) ActivateField();

            // ── Enter — 묻기 ──
            //   InputField의 이벤트를 쓰지 않는다: 판이 선택을 잃을 때도 함께 발화하는 판이라
            //   되살리기와 부딪힌다. 글쇠를 직접 보면 규칙이 하나로 단순해진다.
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Ask();
        }

        // ── 목소리 ────────────────────────────────────────────
        /// <summary>
        /// 누르고 말하기. PC = 왼쪽 Ctrl, VR = 그립 — 어느 쪽인지는 <see cref="UiPointers"/> 가 안다.
        ///
        /// VR에서는 이것이 <b>묻는 유일한 길</b>이다 (글쇠를 칠 수 없으므로).
        /// ⚠️ 그립 버튼 배치와 마이크 잡힘은 헤드셋으로 확인하지 못했다.
        /// </summary>
        void HandleVoice()
        {
            var ptr = UiPointers.Get(eye);

            // 「말하기」 단추 위에서 누르고 있는 것을 <b>왼쪽 Ctrl 을 누른 것과 똑같이</b> 친다
            // (2026-08-26, 하단 바 시안). 녹음을 시작·끝내는 조건도, 받아 적은 글을 어떻게
            // 처리하는지도 하나도 안 달라졌다 — <b>손잡이가 하나 더 생겼을 뿐</b>이다.
            bool onMic = micSpot != null && Hovered == micSpot && micSpot.interactable;
            bool micHeld = onMic && ptr.PressHeld;
            bool micDown = onMic && ptr.PressDown;

            bool held = ptr.TalkHeld || micHeld;

            if (!voice.Recording && (ptr.TalkDown || micDown) && !session.Busy && !voiceBusy)
            {
                if (!VoiceInput.HasMicrophone) { DebugToast.Show("마이크를 찾지 못했다.", 2f); return; }
                // 받아쓰기를 맡은 곳이 없으면 애초에 여기까지 오지 않는다(단추가 숨는다).
                // 그래도 한 번 더 본다 — 실행 중에 뽑히는 경우가 있다.
                if (!UiDialogue.CanTranscribe) { DebugToast.Show("목소리를 글로 옮길 수단이 없다.", 2.5f); return; }
                if (voice.Begin())
                {
                    field.DeactivateInputField();
                    Refresh();
                }
                return;
            }

            if (voice.Recording)
            {
                voice.Tick();
                UpdateVoiceMeter();
                // ⚠️ <c>TalkUp</c>(Ctrl 뗌) 검사를 남겨 둔다 — 한 프레임에 눌렀다 떼면 held 가
                //    이미 false 라 놓칠 수 있어서 원래 있던 안전줄이다. 단, 단추를 쥐고 있는
                //    동안에는 무시한다(그때는 Ctrl 이 손잡이가 아니다).
                if (!held || (ptr.TalkUp && !micHeld)) EndVoice();
            }

            // 단추가 지금 듣고 있음을 색으로 알린다 — 그린 마이크가 주칠로 물든다
            if (micIcon != null)
            {
                var img = micIcon.GetComponent<Image>();
                var want = voice.Recording ? InventorySkin.Vermilion : InventorySkin.Hanji;
                if (img != null && img.color != want) img.color = want;
            }
        }

        void EndVoice()
        {
            var wav = voice.End();
            UpdateVoiceMeter();
            if (wav == null)
            {
                Refresh();
                ActivateField();
                return;
            }

            voiceBusy = true;
            Refresh();
            UiDialogue.Voice.Transcribe(this, wav,
                text =>
                {
                    voiceBusy = false;
                    field.text = text;
                    Refresh();
                    // 문서 요구: 떼면 곧바로 전달한다. 받아 적은 글은 대화 기록에 그대로 남아
                    // 무엇으로 전해졌는지 확인할 수 있다.
                    if (owner != null && owner.VoiceAutoSend) Ask();
                    else ActivateField();
                },
                err =>
                {
                    voiceBusy = false;
                    Refresh();
                    DebugToast.Show("받아 적지 못했다 — " + err, 2.5f);
                    ActivateField();
                });
        }

        void UpdateVoiceMeter()
        {
            if (voiceBar == null) return;
            bool on = voice.Recording;
            voiceBar.gameObject.SetActive(on);
            if (!on) return;
            float w = voiceBar.sizeDelta.x - 8f;
            voiceBarFill.sizeDelta = new Vector2(Mathf.Max(4f, w * voice.Level), voiceBar.sizeDelta.y - 8f);
            // ⚠️ 흐른 시간은 여기서 갱신한다. Refresh는 대사가 바뀔 때만 도는데, 녹음 중에는
            //    아무 대사도 오지 않아 "0.0초"에서 멈춰 있었다(2026-08-25 실측).
            if (voiceText != null)
                voiceText.text = "<color=#AA3728>●</color>  듣는 중  " + voice.ElapsedSeconds.ToString("F1") + "초";
        }

        // ── 갱신 ─────────────────────────────────────────────
        void Refresh()
        {
            if (session == null) return;
            string npcName = session.SpeakerName;
            string line = session.Busy ? "…" : session.CurrentLine;

            if (nameText != null) nameText.text = npcName;
            if (lineText != null && line != shownLine)
            {
                lineText.text = line;
                shownLine = line;
                lineScroll = 0f;          // 새 대사는 늘 첫 줄부터
                ApplyLineScroll();
            }
            if (bubbleName != null) bubbleName.text = npcName;
            if (bubbleLine != null) bubbleLine.text = line;

            if (voiceText != null)
            {
                if (voice.Recording) voiceText.text = "<color=#AA3728>●</color>  듣는 중  " + voice.ElapsedSeconds.ToString("F1") + "초";
                else if (voiceBusy) voiceText.text = "받아 적는 중…";
                else voiceText.text = "";
            }

            bool idle = !session.Busy && !voice.Recording && !voiceBusy;
            if (askSpot != null) { askSpot.interactable = idle; askLabel.text = idle ? "묻 기" : "…"; }
            if (presentSpot != null) presentSpot.interactable = idle;
        }

        // ⚠️ 점수가 움직였다는 표시를 화면에 그리지 않는다 (2026-08-25, 문서 「9. 설계 원칙」).
        //    전에는 여기서 "마음이 조금 열렸다" 같은 문구를 오른쪽 위에 띄웠다. 그러면
        //    플레이어가 **한 마디마다 표시를 보고 점수를 역산**하게 되고, 그 순간 대화가
        //    추리가 아니라 최적화가 된다. 신뢰도는 견우의 태도와 대사로만 느끼게 둔다.
        //    (등급 자체는 그대로 매겨져 GyeonuCase에 반영된다 — 감추는 것은 표시뿐이다.)

        /// <summary>
        /// 아래 조작 안내. <b>받아쓰기를 맡은 곳이 없으면 목소리 줄을 통째로 뺀다</b> (2026-08-26).
        /// 있지도 않은 기능을 누르라고 적어 두면 안 되기 때문이다 —
        /// <see cref="IVoiceTranscriber"/> 를 구현하지 않은 사건에서는 글쓰기 안내만 남는다.
        /// 견우는 <see cref="GyeonuVoice"/> 가 꽂혀 있어 예전과 똑같이 나온다.
        /// </summary>
        string HintLine
        {
            get
            {
                bool v = UiDialogue.CanTranscribe;
                if (UiModes.IsVr)
                    return (v ? "<color=#8E2C20>그립 — 누르고 말하기</color>     " : "")
                         + "트리거 — 누르기     B·Y — 대화 끝내기";
                return "Enter — 묻기     "
                     + (v ? "<color=#8E2C20>왼쪽 Ctrl — 누르고 말하기</color>     " : "")
                     + "좌클릭 — 누르기     Esc / 우클릭 — 대화 끝내기";
            }
        }

        // ── 행동 ─────────────────────────────────────────────
        void Ask()
        {
            if (session == null || session.Busy || field == null || voice.Recording || voiceBusy) return;
            string text = field.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            field.text = "";
            session.Ask(text);
            ActivateField();
        }

        /// <summary>증거 제시 — 소지품 판을 그대로 연다.</summary>
        void OpenPresentPanel()
        {
            if (session == null || eye == null) return;
            var input = eye.GetComponent<InventoryInput>();
            if (input == null) { DebugToast.Show("소지품 입력이 없다.", 2f); return; }
            field.DeactivateInputField();
            input.OpenPresent(() => session.Presentables(), OnPresentChosen);
        }

        void OnPresentChosen(IUiItem item)
        {
            if (item == null || session == null) return;
            var input = eye != null ? eye.GetComponent<InventoryInput>() : null;
            if (input != null) input.ClosePanel();
            // 정보 단서인지 물건인지 가르는 일은 **말 상대 쪽**이 한다 (2026-08-26에 내렸다).
            // 화면이 단서 코드표를 알면 안 된다 — 그건 사건의 것이다.
            if (!session.Present(item)) DebugToast.Show("이건 내밀 것이 못 된다.", 2f);
            ActivateField();
        }

        void RequestExit()
        {
            // 포커스 리그가 물러나기를 맡는다 — 카메라 복귀·잠금 해제가 거기 다 있다.
            var rig = eye != null ? eye.GetComponent<DebugFocusRig>() : null;
            if (rig != null) rig.ExitFocus();
        }

        // ── 포인터 ───────────────────────────────────────────
        public void PointAt(Ray ray)
        {
            if (!IsOpen) return;
            Hovered = null;
            var hits = Physics.RaycastAll(ray, 8f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var spot = h.collider.GetComponent<DialogueHotspot>();
                if (spot != null && spot.interactable && spot.gameObject.activeInHierarchy) { Hovered = spot; break; }
            }

            // 조준점은 콜라이더가 아니라 판 평면과의 교점으로 잡는다 — 판 밖을 겨눠도
            // 커서가 사라지지 않고 가장자리로 따라간다.
            var plane = new Plane(-transform.forward, transform.position);
            if (plane.Raycast(ray, out float dist))
            {
                var local = transform.InverseTransformPoint(ray.GetPoint(dist));
                float hw = geom.w * 0.5f, hh = geom.h * 0.5f;
                cursor.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -hw, hw), Mathf.Clamp(local.y, -hh, hh));
                cursor.gameObject.SetActive(true);
            }
            else cursor.gameObject.SetActive(false);

            askSpot.SetHovered(askSpot == Hovered);
            presentSpot.SetHovered(presentSpot == Hovered);
            closeSpot.SetHovered(closeSpot == Hovered);
        }

        public void ClickHovered()
        {
            if (!IsOpen || Hovered == null || !Hovered.interactable) return;
            switch (Hovered.kind)
            {
                case DialogueHotspot.Kind.묻기: Ask(); break;
                case DialogueHotspot.Kind.단서열기: OpenPresentPanel(); break;
                case DialogueHotspot.Kind.입력칸: ActivateField(); break;
                case DialogueHotspot.Kind.끝내기: RequestExit(); break;
            }
        }

        /// <summary>
        /// 휠 — 대사가 틀보다 길 때 굴려 읽는다.
        ///
        /// ⚠️ 포커스 리그는 휠 값을 <b>매 프레임 날것으로</b> 넘긴다(소지품 판처럼 한 칸씩 끊어
        ///    주지 않는다). 그대로 받으면 한 번 굴릴 때 수십 번 불려 글이 끝까지 튄다 —
        ///    소지품 상세에서 겪은 것과 같은 실패다. 여기서 짧은 쉼으로 끊는다.
        /// </summary>
        public void Scroll(float direction)
        {
            if (!IsOpen || lineViewport == null) return;
            if (Time.unscaledTime < scrollReadyAt) return;
            scrollReadyAt = Time.unscaledTime + 0.06f;
            lineScroll += direction > 0f ? -LineHeight() : LineHeight();
            ApplyLineScroll();
        }

        /// <summary>글줄 하나의 높이 — 굴림 한 칸이 딱 한 줄이 되게 실제로 잰다.
        /// 글꼴·글자크기가 바뀌어도 따라온다.
        ///
        /// ⚠️ TMP 로 옮기며 재는 곳이 바뀌었다. 레거시의 <c>cachedTextGenerator.lineCount</c> 는
        ///    <see cref="TMP_Text.textInfo"/> 의 <c>lineCount</c> 가 대신하는데, 이것은
        ///    <b>메시를 한 번 짠 뒤에만</b> 채워진다 — 안 짜고 읽으면 0이라 되돌림 값으로 새고,
        ///    그러면 휠 한 칸이 한 줄과 어긋난다. 그래서 먼저 <c>ForceMeshUpdate</c> 를 부른다.</summary>
        float LineHeight()
        {
            if (lineText == null) return 46f;
            lineText.ForceMeshUpdate();
            int n = lineText.textInfo != null ? lineText.textInfo.lineCount : 0;
            if (n > 0) return lineText.preferredHeight / n;
            // 되돌림 — TMP 의 lineSpacing 은 '더하는 여분(%)'이라 배수로 환산해서 곱한다
            return lineText.fontSize * (1f + lineText.lineSpacing * 0.01f);
        }

        /// <summary>굴린 만큼 글을 올리고, 아직 남았으면 알려 준다.</summary>
        void ApplyLineScroll()
        {
            if (lineViewport == null || lineRect == null || lineText == null) return;
            Canvas.ForceUpdateCanvases();
            float overflow = Mathf.Max(0f, lineText.preferredHeight - lineViewport.rect.height);
            lineScroll = Mathf.Clamp(lineScroll, 0f, overflow);
            lineRect.anchoredPosition = new Vector2(lineRect.anchoredPosition.x, lineScroll);
            // 굴릴 것이 있는지 모르면 아무도 굴리지 않는다 — 남았을 때만 화살표를 띄운다.
            if (overflowHint != null)
                overflowHint.gameObject.SetActive(overflow > 1f && lineScroll < overflow - 1f);
        }

        // ─────────────────────────────────────────────────────
        //  판 짓기 — 배치안마다 자리만 다르고 부품은 같다
        // ─────────────────────────────────────────────────────
        void Rebuild(DialogueLayout layout)
        {
            Layout = layout;
            geom = GeomOf(layout);

            // 있던 것을 통째로 비우고 새로 짓는다 (배치안을 바꿔 가며 견주어야 한다)
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
            if (bubbleGo != null) { DestroyImmediate(bubbleGo); bubbleGo = null; }
            nameText = lineText = hintText = voiceText = overflowHint = null;
            bubbleName = bubbleLine = null;
            micSpot = null; micLabel = null; micIcon = null;
            voiceBar = voiceBarFill = null;
            lineViewport = lineRect = null;
            shownLine = "";
            lineScroll = 0f;

            if (font == null) font = MakeFont();
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            }
            canvas.renderMode = RenderMode.WorldSpace;
            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(geom.w, geom.h);
            root.localScale = Vector3.one * geom.scale;

            // 목재 틀 ▸ 한지 ▸ 안쪽 테선 ▸ 한지 — 소지품 판과 같은 네 겹
            //
            // ⚠️ <b>투명도는 뒤판에만 먹인다</b> (2026-08-26, 하단 바 시안).
            //    글씨·이름패·단추는 이 알파를 안 탄다 — 배경만 비쳐야 뒤의 NPC가 보이면서도
            //    글은 그대로 읽힌다. 판 전체(캔버스 그룹)에 알파를 먹이면 글까지 흐려져
            //    "투명하게 했더니 안 읽힌다"가 된다.
            bool bar = IsBottomBar(layout);
            RectTransform paper;

            if (bar)
            {
                // ── 하단 바 — <b>한 겹만 깐다</b> (2026-08-26) ──────────────────────
                //
                // ⚠️ 여기서 한 번 헛디뎠다. 아래 A·B·C처럼 네 겹(틀·바탕·테선·속지)을 그대로 두고
                //    <b>각 겹에 알파 0.72</b>를 먹였더니 화면에서는 거의 불투명하게 나왔다.
                //    겹칠 때 알파는 곱이 아니라 <b>1−(1−a)ⁿ</b> 로 쌓인다 — 0.72 네 겹이면 0.994다.
                //    "투명하게 했는데 왜 안 비치지"의 답이 이것이다.
                //    그래서 바는 <b>한지 한 겹 + 겹치지 않는 얇은 테두리 넉 줄</b>로 짓는다.
                //    그러면 화면에 나오는 투명도가 정확히 style.alpha 다.
                // 2026-08-27: 밝은 한지 대신 <b>어두운 색 + 높은 투명도</b>로 바꿨다.
                //   한지결을 그대로 두면 투명해도 밝은 결이 비쳐 답답했다 (시안 1차에서 확인).
                //   한지결을 남기는 안도 견주었으나, 투명해도 답답해 보여 민판으로 정했다.
                var pal = Palette();
                paper = MakeImage(root, "바탕", pal.paperGrain ? skin.Hanji_ : UiSkin.White, pal.back);
                Stretch(paper, 0f);

                const float T = 5f;                      // 테두리 두께 (목재 느낌은 남긴다)
                float hw0 = geom.w * 0.5f, hh0 = geom.h * 0.5f;
                Place(MakeImage(root, "테_위", skin.Wood_, pal.border), new Vector2(0f, hh0 - T * 0.5f), new Vector2(geom.w, T));
                Place(MakeImage(root, "테_아래", skin.Wood_, pal.border), new Vector2(0f, -hh0 + T * 0.5f), new Vector2(geom.w, T));
                Place(MakeImage(root, "테_왼", skin.Wood_, pal.border), new Vector2(-hw0 + T * 0.5f, 0f), new Vector2(T, geom.h));
                Place(MakeImage(root, "테_오른", skin.Wood_, pal.border), new Vector2(hw0 - T * 0.5f, 0f), new Vector2(T, geom.h));
            }
            else
            {
                // 목재 틀 ▸ 한지 ▸ 안쪽 테선 ▸ 한지 — 소지품 판과 같은 네 겹 (A·B·C 그대로)
                Stretch(MakeImage(root, "틀", skin.Wood_, Color.white), 22f);
                paper = MakeImage(root, "바탕", skin.Hanji_, Color.white);
                Stretch(paper, 0f);
                Stretch(MakeImage(root, "테선", skin.Wood_, InventorySkin.Wood), -12f);
                Stretch(MakeImage(root, "속지", skin.Hanji_, Color.white), -16f);
            }

            var board = paper.gameObject.AddComponent<BoxCollider>();
            board.isTrigger = true;
            board.size = new Vector3(geom.w, geom.h, 2f);

            switch (layout)
            {
                case DialogueLayout.A_좌측판: BuildA(); break;
                case DialogueLayout.B_하단띠: BuildB(); break;
                case DialogueLayout.C_말풍선: BuildC(); break;
                default: BuildBottom(); break;
            }

            // 조준점 — 판 위에 찍히는 커서 (VR에서도 그대로 보인다)
            var dot = MakeImage(root, "조준", skin.Dot_, new Color(1f, 0.95f, 0.8f, 0.95f));
            Place(dot, Vector2.zero, new Vector2(34f, 34f));
            cursor = dot;
            cursor.SetAsLastSibling();

            built = true;
            builtForVr = UiModes.IsVr;
        }

        // ── A — 좌측 아래 판 (1500 × 620) · 확정안 ───────────
        //   세로 620 = 반 310. 위에서부터 쌓은 자리:
        //     이름패 264 / 구분선 228 / 대사틀 −51~207 / 입력줄 −195~−121 / 안내 −289~−263
        //   ⚠️ 속지가 −294~294 이므로 그 밖으로 나가면 글이 틀에 물린다.
        void BuildA()
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;
            Header(new Vector2(-hw + 215f, hh - 46f), 300f, 54f, 32,
                   new Vector2(hw - 380f, hh - 46f), 560f, 26, hh - 82f, geom.w - 80f);

            // ⚠️ 대사틀 윗변을 구분선(228) 바로 밑에 붙이면, 굴렸을 때 **반쯤 잘린 글줄**이
            //    구분선에 맞닿아 고장난 것처럼 보인다(2026-08-25 실측). 38단위를 띄운다.
            LineViewport(new Vector2(0f, 70f), new Vector2(geom.w - 100f, 240f), 36);

            InputRow(-158f, geom.w - 100f, 74f, 0.60f);
            Footer(22, -hh + 34f, 26f);
        }

        // ── B — 하단 얇은 띠 (2200 × 400) ────────────────────
        void BuildB()
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;
            Header(new Vector2(-hw + 230f, hh - 54f), 330f, 56f, 34,
                   new Vector2(hw - 470f, hh - 54f), 620f, 30, hh - 88f, geom.w - 70f);

            LineViewport(new Vector2(0f, 30f), new Vector2(geom.w - 90f, 116f), 34);

            // ⚠️ 띠가 얇아 입력줄과 안내줄이 겹치기 쉽다 — 아래 두 값은 함께 계산할 것.
            //    입력 −146~−70, 안내 −187~−161 (판 아래끝 −200, 틀 22 안쪽).
            InputRow(-108f, geom.w - 90f, 76f, 0.60f);
            Footer(20, -geom.h * 0.5f + 26f, 26f);
        }

        // ── 하단 가로 바 — 확정안 + 지난 시안 E·F (PC·VR 공용) ─────────────
        /// <summary>
        /// <b>화면 아래에 가로로 길게 눕히는 배치.</b> 여느 게임의 대화 바와 같은 꼴이다.
        ///
        /// ■ 무엇을 노렸나
        ///   ① <b>NPC를 안 가린다</b> — 화면의 아래 한 켜만 쓰고, 뒤판이 비쳐 뒤가 보인다.
        ///   ② <b>조선 느낌은 남긴다</b> — 한지결·나뭇결 텍스처와 주칠 이름패를 그대로 쓰고
        ///      투명도만 올렸다. 색을 바꾼 것이 아니라 <b>얇게 깐</b> 것이다.
        ///
        /// ■ 이름패가 윗변에 걸터앉는다
        ///   낮은 바에서 이름 줄을 따로 두면 대사 자리가 두 줄로 줄어든다. 이름패를 판 <b>윗변에
        ///   걸쳐</b> 절반을 밖으로 내보내면 그 한 줄이 통째로 대사 몫이 된다.
        ///   현판을 처마에 건 모양이라 조선 배경과도 맞다.
        ///
        /// ■ 긴 대사는 <b>굴려 읽는다</b> (페이지·자동확장이 아니라)
        ///   · 페이지 나누기 — 대사 길이를 LLM이 정하므로 마지막 장이 한 줄만 남는 일이 잦다.
        ///   · 자동 확장 — 바 높이가 대사마다 들썩여 <b>NPC를 도로 가린다</b>. 이 배치의 목적과 어긋난다.
        ///   · 굴려 읽기 — 높이가 고정이라 화면이 안 흔들리고, 이미 쓰던 방식이라 검증돼 있다.
        ///   그래서 굴리기를 그대로 두되, <b>「더 있음」 표시를 대사 오른쪽 끝</b>으로 옮겨
        ///   낮은 바에서도 눈에 띄게 했다.
        /// </summary>
        void BuildBottom()
        {
            var st = StyleOf(Layout);
            var pal = Palette();
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;

            // ── 머리 — 이름패 · 녹음 · 닫기 × 를 <b>모두 창 안에</b> (2026-08-27 2차) ──
            //
            // ⚠️ 1차 시안에서는 이름패와 ×를 <b>윗변에 걸터앉혀</b> 절반을 밖으로 내보냈다.
            //    대사 자리를 한 줄 벌려고 한 것인데, 창 밖으로 튀어나온 ×가 "창 범위를 벗어난다"는
            //    지적을 받았다. 좌측 판(A안)이 그랬듯 <b>머리를 창 안에 두고 구분선으로 나눈다</b> —
            //    창의 테두리가 곧 내용의 경계라야 어디까지가 창인지 한눈에 읽힌다.
            float nameW = 300f, nameH = st.NameH;
            float headY = hh - st.padTop - nameH * 0.5f;          // 머리 띠의 한가운데

            var plate = MakeImage(root, "이름패", skin.Wood_, InventorySkin.Vermilion);
            Place(plate, new Vector2(-hw + st.padX + nameW * 0.5f, headY), new Vector2(nameW, nameH));
            nameText = MakeText(plate, "이름", st.nameSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(nameText.rectTransform, -6f);
            nameText.fontStyle = FontStyles.Bold;

            // 닫기 × — 창 <b>안</b> 우측 상단. 이름패와 같은 띠에 앉혀 좌우 균형을 맞춘다 (A안과 같다).
            CloseButton(new Vector2(hw - st.padX * 0.5f - nameH * 0.5f, headY), nameH, st.nameSize + 2);

            // 녹음 상태 — 이름패 오른쪽. 폭은 ×까지 남는 만큼으로 잰다 (고정폭이면 판이 바뀔 때 겹친다).
            float voiceX = -hw + st.padX + nameW + 36f;
            VoiceMeter(new Vector2(voiceX + 65f, headY), 130f, 20f);
            float voiceTextL = voiceX + 150f;
            float voiceTextR = hw - st.padX - nameH - 20f;
            float voiceTextW = Mathf.Max(160f, voiceTextR - voiceTextL);
            voiceText = MakeText(root, "녹음", st.footSize, TextAnchor.MiddleLeft, pal.dim);
            Place(voiceText.rectTransform, new Vector2(voiceTextL + voiceTextW * 0.5f, headY),
                  new Vector2(voiceTextW, 30f));

            // ── 구분선 — 머리와 대사를 가른다 (A안의 그 줄) ──
            float ruleY = headY - nameH * 0.5f - st.nameToRule - st.ruleH * 0.5f;
            Place(MakeImage(root, "구분선", skin.Wood_, pal.border),
                  new Vector2(0f, ruleY), new Vector2(geom.w - st.padX * 2f, st.ruleH));

            // ── 대사 ──
            float lineTop = ruleY - st.ruleH * 0.5f - st.ruleToLine;
            float lineY = lineTop - st.lineBoxH * 0.5f;
            LineViewport(new Vector2(0f, lineY), new Vector2(geom.w - st.padX * 2f, st.lineBoxH), st.lineSize);

            // ⚠️ <see cref="LineViewport"/> 는 A·B·C 와 함께 쓰는 부품이라 먹빛 글씨로 짓는다.
            //    바는 바탕이 어두우므로 <b>지은 뒤에 색만 뒤집는다</b> — 그 부품을 고치면
            //    다른 배치안까지 바뀌기 때문이다. 아래 Footer 도 같은 사정이다.
            if (lineText != null) lineText.color = pal.text;

            // 「더 있음」 — 대사 상자 <b>아래</b> 오른쪽 (A안과 같은 자리). 대사와 입력줄 사이의 틈에 앉는다.
            if (overflowHint != null)
                Place(overflowHint.rectTransform,
                      new Vector2(hw - st.padX - 105f, lineY - st.lineBoxH * 0.5f - st.lineToInput * 0.5f),
                      new Vector2(210f, 22f));

            // ── 아래에서부터 쌓는다 ──
            //   테두리 → footToEdge → 안내 → inputToFoot → 입력줄
            //   위에서 내려오는 대사와 만나는 자리가 lineToInput 이고,
            //   판 높이를 그 합으로 잡았으므로 (BarGeom) 셋이 정확히 맞아떨어진다.
            float footY = -hh + st.footToEdge + st.footH * 0.5f;
            float inputY = footY + st.footH * 0.5f + st.inputToFoot + st.inputH * 0.5f;
            InputRowBottom(inputY, geom.w - st.padX * 2f, st.inputH, st.inputSize);

            Footer(st.footSize, footY, st.footH);
            if (hintText != null) hintText.color = pal.dim;
        }

        /// <summary>
        /// 하단 바의 입력줄 — <c>[글쇠 칸] [말하기] [묻기] [증거 제시]</c> 를 한 줄에 눕힌다.
        ///
        /// <see cref="InputRow"/> 와 나누지 않고 따로 둔 까닭: 여기에만 <b>말하기 단추</b>가 있고,
        /// 낮은 바라 폭 배분이 다르다. 기존 배치안(A·B·C)의 입력줄은 하나도 안 건드린다.
        /// </summary>
        void InputRowBottom(float y, float totalW, float h, int fontSize)
        {
            bool voiceOn = UiDialogue.CanTranscribe;

            float gap = 16f;
            float micW = voiceOn ? h : 0f;                 // 정사각 — 낮은 바에서 가장 안 튄다
            float askW = Mathf.Max(120f, totalW * 0.105f);
            float presentW = Mathf.Max(180f, totalW * 0.165f);
            int gaps = voiceOn ? 3 : 2;
            float fieldW = totalW - micW - askW - presentW - gap * gaps;

            float x = -totalW * 0.5f;

            var pal = Palette();

            // 글쇠 칸 — 뒤판보다 조금 짙게 파 넣은 자리로 보이게 한다
            //   ⚠️ 밝은 한지 칸을 그대로 두면 어두운 바에서 <b>거기만 하얗게 떠</b> 눈을 끈다.
            var box = MakeImage(root, "글쇠칸", UiSkin.White, pal.slotBack);
            Place(box, new Vector2(x + fieldW * 0.5f, y), new Vector2(fieldW, h));
            Stretch(MakeImage(box, "칸테", skin.Wood_, pal.border), 3f);

            var txt = MakeText(box, "글", fontSize, TextAnchor.MiddleLeft, pal.slotText);
            Place(txt.rectTransform, new Vector2(10f, 0f), new Vector2(fieldW - 36f, h - 16f));
            var ph = MakeText(box, "안내글", fontSize, TextAnchor.MiddleLeft, pal.slotHint);
            Place(ph.rectTransform, new Vector2(10f, 0f), new Vector2(fieldW - 36f, h - 16f));
            placeholder = ph;

            field = box.gameObject.AddComponent<TMP_InputField>();
            field.textComponent = txt;
            field.placeholder = ph;
            field.richText = false;          // ⚠️ 한글 조합 중 <u> 태그가 새는 것을 막는다 (InputRow 주석 참고)
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 120;
            field.customCaretColor = true;
            field.caretColor = pal.slotText;    // 어두운 칸에서는 커서도 밝아야 보인다
            field.selectionColor = new Color(0.67f, 0.22f, 0.16f, 0.35f);
            field.targetGraphic = box.GetComponent<Image>();
            field.transition = Selectable.Transition.None;
            AddSpot(box, DialogueHotspot.Kind.입력칸, new Vector2(fieldW, h), null, InventorySkin.Wood);
            x += fieldW + gap;

            // 말하기 — 누르고 있는 동안 녹음. 받아쓰기가 없으면 단추 자체가 안 선다.
            TextMeshProUGUI l;
            if (voiceOn)
            {
                micSpot = MakeButton(root, "말하기", new Vector2(x + micW * 0.5f, y), new Vector2(micW, h),
                                     DialogueHotspot.Kind.말하기, InventorySkin.Wood, fontSize + 2, out l);
                // 글자 대신 <b>그린 마이크</b>를 얹는다 (2026-08-27 확정).
                //   그림글자(🎤)는 두 글꼴 어디에도 없어 네모로 뜨고, 낱자 「말」로 대신했더니
                //   옆의 「묻 기」·「증거 제시」와 켜가 섞여 읽혔다.
                l.text = "";
                micIcon = MakeImage(micSpot.transform as RectTransform, "마이크그림", skin.Mic_, InventorySkin.Hanji);
                Place(micIcon, Vector2.zero, new Vector2(h * 0.56f, h * 0.56f));
                micLabel = l;
                x += micW + gap;
            }

            askSpot = MakeButton(root, "묻기", new Vector2(x + askW * 0.5f, y), new Vector2(askW, h),
                                 DialogueHotspot.Kind.묻기, InventorySkin.Vermilion, fontSize, out l);
            askLabel = l; askLabel.text = "묻 기";
            x += askW + gap;

            presentSpot = MakeButton(root, "증거제시", new Vector2(x + presentW * 0.5f, y), new Vector2(presentW, h),
                                     DialogueHotspot.Kind.단서열기, InventorySkin.Wood, fontSize, out l);
            l.text = "증거 제시";
        }

        // ── C — 말풍선 + 하단 채팅바 (1900 × 260) ────────────
        void BuildC()
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;

            // 채팅바에는 이름·대사가 없다 — 말풍선이 맡는다. 반응만 짧게 얹는다.

            voiceText = MakeText(root, "녹음", 26, TextAnchor.MiddleRight, InventorySkin.InkSoft);
            Place(voiceText.rectTransform, new Vector2(hw - 330f, hh - 44f), new Vector2(560f, 38f));

            CloseButton(new Vector2(hw - 46f, hh - 44f), 52f, 30);
            VoiceMeter(new Vector2(-hw + 120f, hh - 44f), 140f, 22f);

            InputRow(-4f, geom.w - 90f, 72f, 0.60f);
            Footer(20, -hh + 38f, 26f);

            BuildBubble();
        }

        void BuildBubble()
        {
            bubbleGo = new GameObject("대화_말풍선", typeof(RectTransform));
            bubbleGo.transform.SetParent(transform.parent, false);
            DontDestroyOnLoad(bubbleGo);
            var c = bubbleGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            bubbleGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            bubbleRoot = (RectTransform)bubbleGo.transform;
            bubbleRoot.sizeDelta = new Vector2(BubbleW, BubbleH);
            bubbleRoot.localScale = Vector3.one * BubbleScale;

            Stretch(MakeImage(bubbleRoot, "틀", skin.Wood_, InventorySkin.Wood), 12f);
            Stretch(MakeImage(bubbleRoot, "바탕", skin.Hanji_, Color.white), 0f);

            // 꼬리 — 네모를 45° 돌려 NPC 쪽을 가리키게 한다
            bubbleTail = MakeImage(bubbleRoot, "꼬리", skin.Hanji_, Color.white);
            Place(bubbleTail, new Vector2(BubbleW * 0.5f + 6f, -BubbleH * 0.5f + 56f), new Vector2(84f, 84f));
            bubbleTail.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var plate = MakeImage(bubbleRoot, "이름패", skin.Wood_, InventorySkin.Vermilion);
            Place(plate, new Vector2(-BubbleW * 0.5f + 130f, BubbleH * 0.5f - 52f), new Vector2(200f, 58f));
            bubbleName = MakeText(plate, "이름", 34, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(bubbleName.rectTransform, -6f);
            bubbleName.fontStyle = FontStyles.Bold;

            bubbleLine = MakeText(bubbleRoot, "대사", 40, TextAnchor.UpperLeft, InventorySkin.Ink);
            Place(bubbleLine.rectTransform, new Vector2(0f, -30f), new Vector2(BubbleW - 90f, 260f));
            bubbleLine.lineSpacing = UiSkin.LineSpacing(1.28f);
        }

        // ── 공통 부품 ────────────────────────────────────────
        /// <summary>
        /// 대사 자리 — <b>틀에 가두고 넘치면 굴려 읽는다</b> (2026-08-25).
        ///
        /// 판을 낮추면서 대사 자리가 다섯 줄 남짓으로 줄었다. 글자를 더 줄이면 VR에서 읽기 어렵고,
        /// 넘치는 만큼 잘라 버리면 말끝이 사라진다. 그래서 소지품 상세의 설명칸과 같은 방식으로
        /// <see cref="RectMask2D"/> 안에 넣고 휠로 굴린다 — 남은 글이 있을 때만 화살표가 뜬다.
        /// </summary>
        void LineViewport(Vector2 pos, Vector2 size, int fontSize)
        {
            lineViewport = MakeRect(root, "대사틀");
            Place(lineViewport, pos, size);
            lineViewport.gameObject.AddComponent<RectMask2D>();

            lineText = MakeText(lineViewport, "대사", fontSize, TextAnchor.UpperLeft, InventorySkin.Ink);
            lineRect = lineText.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 1f);
            lineRect.anchorMax = new Vector2(1f, 1f);
            lineRect.pivot = new Vector2(0.5f, 1f);
            lineRect.anchoredPosition = Vector2.zero;
            // ⚠️ 가로는 앵커가 늘려 준다. 여기 폭을 넣으면 두 배가 된다(소지품 판에서 실측).
            lineRect.sizeDelta = new Vector2(0f, size.y);
            lineText.overflowMode = TextOverflowModes.Overflow;
            lineText.lineSpacing = UiSkin.LineSpacing(1.28f);

            // ⚠️ 오른쪽 맞춤 글은 **rect의 오른쪽 끝**에 붙는다. 중심을 틀 오른쪽 끝에 두면
            //    글이 판 밖으로 삐져나간다(2026-08-25 실측). 중심을 폭의 절반만큼 당겨 온다.
            const float HintW = 300f;
            overflowHint = MakeText(root, "더있음", 22, TextAnchor.MiddleRight, InventorySkin.Vermilion);
            Place(overflowHint.rectTransform,
                  new Vector2(pos.x + size.x * 0.5f - HintW * 0.5f, pos.y - size.y * 0.5f - 16f),
                  new Vector2(HintW, 26f));
            overflowHint.text = "▼  휠을 굴려 더 보기";
            overflowHint.gameObject.SetActive(false);
        }

        /// <summary>이름패 · 반응 · 녹음 표시 · 닫기 ✕ · 구분선.</summary>
        void Header(Vector2 namePos, float nameW, float nameH, int nameSize,
                    Vector2 notePos, float noteW, int noteSize, float ruleY, float ruleW)
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;

            var plate = MakeImage(root, "이름패", skin.Wood_, InventorySkin.Vermilion);
            Place(plate, namePos, new Vector2(nameW, nameH));
            nameText = MakeText(plate, "이름", nameSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(nameText.rectTransform, -6f);
            nameText.fontStyle = FontStyles.Bold;

            VoiceMeter(new Vector2(namePos.x + nameW * 0.5f + 90f, namePos.y), 140f, 22f);

            // ⚠️ 녹음 문구의 폭은 **이름패와 반응 사이에 남는 만큼**으로 잰다. 고정폭으로 두었더니
            //    판이 좁아진 A안에서 반응 문구와 겹쳤다(2026-08-25). 판 치수가 바뀌어도 안 겹친다.
            float voiceLeft = namePos.x + nameW * 0.5f + 170f;
            float voiceRight = notePos.x - noteW * 0.5f - 20f;
            float voiceW = Mathf.Max(120f, voiceRight - voiceLeft);
            voiceText = MakeText(root, "녹음", noteSize, TextAnchor.MiddleLeft, InventorySkin.InkSoft);
            Place(voiceText.rectTransform, new Vector2(voiceLeft + voiceW * 0.5f, namePos.y), new Vector2(voiceW, 38f));

            // notePos·noteW 는 이제 **오른쪽 여백의 경계**로만 쓴다 — 점수 반응 문구를 없앴다.
            // (윗줄의 녹음 문구가 어디까지 늘어날 수 있는지를 이 값이 정한다.)

            var rule = MakeImage(root, "구분선", skin.Wood_, InventorySkin.Wood);
            Place(rule, new Vector2(0f, ruleY), new Vector2(ruleW, 3f));

            // ⚠️ 닫기는 **오른쪽 위**다 (2026-08-25). 창을 닫는 ✕는 어디서나 오른쪽 위에 있고,
            //    왼쪽에 두면 이름패와 붙어 이름의 일부처럼 읽힌다.
            CloseButton(new Vector2(hw - 52f, hh - 52f), 60f, 34);
        }

        void CloseButton(Vector2 pos, float size, int fontSize)
        {
            TextMeshProUGUI l;
            closeSpot = MakeButton(root, "끝내기", pos, new Vector2(size, size),
                                   DialogueHotspot.Kind.끝내기, InventorySkin.Wood, fontSize, out l);
            l.text = "×";
        }

        /// <summary>녹음 중 소리 크기 막대 — "듣고 있다"를 눈으로 보여 준다.</summary>
        void VoiceMeter(Vector2 pos, float w, float h)
        {
            voiceBar = MakeImage(root, "소리막대", skin.Wood_, InventorySkin.Wood);
            Place(voiceBar, pos, new Vector2(w, h));
            voiceBarFill = MakeImage(voiceBar, "채움", skin.Hanji_, InventorySkin.Vermilion);
            voiceBarFill.anchorMin = voiceBarFill.anchorMax = new Vector2(0f, 0.5f);
            voiceBarFill.pivot = new Vector2(0f, 0.5f);
            voiceBarFill.anchoredPosition = new Vector2(4f, 0f);
            voiceBarFill.sizeDelta = new Vector2(4f, h - 8f);
            voiceBar.gameObject.SetActive(false);
        }

        /// <summary>글쇠 칸 + [묻기] + [증거 제시] 한 줄. <paramref name="fieldFrac"/> 만큼을 칸이 쓴다.</summary>
        void InputRow(float y, float totalW, float h, float fieldFrac)
        {
            float left = -totalW * 0.5f;
            float fw = totalW * fieldFrac;
            float askW = totalW * 0.13f;
            float presentW = totalW - fw - askW - 40f;

            var box = MakeImage(root, "글쇠칸", skin.Slot_, Color.white);
            Place(box, new Vector2(left + fw * 0.5f, y), new Vector2(fw, h));
            Stretch(MakeImage(box, "칸테", skin.Wood_, InventorySkin.Wood), 4f);
            Stretch(MakeImage(box, "칸속", skin.Slot_, Color.white), 0f);

            int fs = Mathf.RoundToInt(h * 0.42f);
            var txt = MakeText(box, "글", fs, TextAnchor.MiddleLeft, InventorySkin.Ink);
            Place(txt.rectTransform, new Vector2(10f, 0f), new Vector2(fw - 40f, h - 18f));
            txt.richText = false;

            var ph = MakeText(box, "안내글", fs, TextAnchor.MiddleLeft, new Color(0.45f, 0.40f, 0.35f, 0.75f));
            Place(ph.rectTransform, new Vector2(10f, 0f), new Vector2(fw - 40f, h - 18f));
            ph.richText = false;
            placeholder = ph;

            field = box.gameObject.AddComponent<TMP_InputField>();
            field.textComponent = txt;
            field.placeholder = ph;

            // ⚠️ <b>글쇠 칸의 리치 텍스트는 반드시 칸(TMP_InputField) 쪽에서 꺼야 한다</b>
            //    (2026-08-26, 한글을 치면 화면에 「계셨&lt;u&gt;스&lt;/u&gt;」 처럼 태그가 그대로 보이던 문제).
            //
            //    TMP_InputField 는 <b>한글 조합 중인 글자에 밑줄을 그으려고</b> 그 글자를
            //    <c>&lt;u&gt;…&lt;/u&gt;</c> 로 감싼다 (TMP_InputField.UpdateLabel). 그런데 감쌀지 말지는
            //    <b>칸 자신의</b> richText 값(기본 true)을 보고 정하고, 그 태그를 <b>해석할지</b>는
            //    글 부품의 richText 값이 정한다. 이 둘을 맞춰 주는 SetTextComponentRichTextMode() 는
            //    richText <b>속성 설정자</b>와 OnValidate(에디터에서 인스펙터를 만질 때)에서만 불린다 —
            //    판을 코드로 짓는 우리에게는 <b>한 번도 불리지 않는다</b>.
            //    그래서 글 부품에 richText=false 만 넣어 두면 <b>칸은 태그를 넣고 글은 글자로 그리는</b>
            //    어긋난 상태가 된다. 아래 한 줄이 두 값을 함께 끈다 — 칸이 태그를 아예 안 만든다.
            //
            //    ⚠️ txt.richText=false 만 되돌려 놓지 말 것. 그러면 조합 밑줄은 살지만
            //       플레이어가 친 &lt;b&gt;·&lt;color&gt; 가 서식으로 먹어 버린다. 이 글은 Gemini에게 그대로 넘어간다.
            field.richText = false;

            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 120;
            field.customCaretColor = true;
            field.caretColor = InventorySkin.Ink;
            field.selectionColor = new Color(0.67f, 0.22f, 0.16f, 0.35f);
            field.targetGraphic = box.GetComponent<Image>();
            field.transition = Selectable.Transition.None;
            AddSpot(box, DialogueHotspot.Kind.입력칸, new Vector2(fw, h), null, InventorySkin.Wood);

            TextMeshProUGUI pl;
            askSpot = MakeButton(root, "묻기", new Vector2(left + fw + 20f + askW * 0.5f, y), new Vector2(askW, h),
                                 DialogueHotspot.Kind.묻기, InventorySkin.Vermilion, Mathf.RoundToInt(h * 0.40f), out pl);
            askLabel = pl; askLabel.text = "묻 기";

            presentSpot = MakeButton(root, "증거제시", new Vector2(left + fw + 40f + askW + presentW * 0.5f, y),
                                     new Vector2(presentW, h), DialogueHotspot.Kind.단서열기,
                                     InventorySkin.Wood, Mathf.RoundToInt(h * 0.38f), out pl);
            pl.text = "증거 제시";
        }

        void Footer(int fontSize, float y, float h)
        {
            hintText = MakeText(root, "안내", fontSize, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Place(hintText.rectTransform, new Vector2(0f, y), new Vector2(geom.w - 60f, h));
        }

        DialogueHotspot MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size,
                                   DialogueHotspot.Kind kind, Color baseColor, int fontSize, out TextMeshProUGUI label)
        {
            var btn = MakeImage(parent, name, skin.Wood_, baseColor);
            Place(btn, pos, size);
            label = MakeText(btn, "글", fontSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(label.rectTransform, -6f);
            label.text = name;
            var spot = AddSpot(btn, kind, size, btn.GetComponent<Image>(), baseColor);
            spot.label = label;
            return spot;
        }

        static DialogueHotspot AddSpot(RectTransform target, DialogueHotspot.Kind kind, Vector2 size,
                                       Image frame, Color baseColor)
        {
            var box = target.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, size.y, 2f);
            box.center = new Vector3(0f, 0f, -6f);   // 바탕보다 앞 — 광선이 먼저 맞는다
            var spot = target.gameObject.AddComponent<DialogueHotspot>();
            spot.kind = kind;
            spot.frame = frame;
            spot.idleColor = baseColor;
            spot.hoverColor = Color.Lerp(baseColor, InventorySkin.Gold, 0.55f);
            return spot;
        }

        /// <summary>
        /// 글쇠 칸(<see cref="InputField"/>)을 살릴 EventSystem — <b>PC 모드에서만</b> 만든다.
        ///
        /// ■ 왜 EventSystem이 여기 하나 남아 있나
        ///   이 프로젝트의 <b>가리키기·누르기는 전부 콜라이더 광선</b>이다(조사에서 확인).
        ///   EventSystem이 필요한 곳은 오직 <b>글자를 받아 적는 칸</b> 하나뿐이다 —
        ///   한글 IME 조합을 UGUI가 대신 처리해 주기 때문이다. 직접 글쇠를 읽는 방식으로 바꾸면
        ///   <b>한글을 못 치게 된다</b>. PC로 계속 플레이해야 하므로 그럴 수 없다.
        ///
        /// ■ VR에서는 만들지 않는다
        ///   HMD를 쓴 채로는 물리 글쇠를 칠 수 없다. VR에서는 <b>목소리</b>(그립 유지)가
        ///   묻는 길이고, 글쇠 칸 자체를 감춘다 — <see cref="ApplyInputRowMode"/>.
        /// </summary>
        void EnsureEventSystem()
        {
            if (UiModes.IsVr) return;
            if (EventSystem.current != null) return;
            var go = new GameObject("대화_EventSystem", typeof(EventSystem));
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            DontDestroyOnLoad(go);
            eventSystemMine = true;
        }

        /// <summary>
        /// 글쇠 칸을 모드에 맞게 손본다 (2026-08-26).
        ///
        /// PC — 지금까지 그대로. 치거나 왼쪽 Ctrl로 말한다.
        /// VR — <b>칸은 그대로 두되 칠 수는 없게</b> 한다.
        ///   칸을 통째로 감춰 봤더니 <b>받아 적힌 말이 어디에도 안 보였다</b> —
        ///   무엇으로 전해지는지 확인하지 못한 채 던지게 된다. 칸을 남겨 두면
        ///   말한 것이 글로 떠서 눈으로 확인하고 「묻 기」를 누를 수 있다.
        /// ⚠️ VR 쪽은 헤드셋으로 확인하지 못했다. 목소리 경로 자체는 PC에서 이미 도는 것이다.
        /// </summary>
        void ApplyInputRowMode()
        {
            if (field == null) return;
            bool typing = UiModes.IsPc;
            if (!field.gameObject.activeSelf) field.gameObject.SetActive(true);
            field.interactable = typing;
            field.readOnly = !typing;
            // ⚠️ 받아쓰기를 맡은 곳이 없으면 목소리 이야기를 꺼내지 않는다 (2026-08-26).
            //    안내 줄과 같은 규칙이다 — 있지도 않은 조작을 하라고 적어 두면 안 된다.
            //    VR + 받아쓰기 없음은 <b>물을 방법이 아예 없는</b> 조합이라 그렇게 적어 준다
            //    (VR에는 칠 칸이 없다). 견우는 GyeonuVoice 가 꽂혀 있어 예전 문구 그대로 나온다.
            if (placeholder != null)
            {
                bool v = UiDialogue.CanTranscribe;
                placeholder.text = typing
                    ? (v ? "묻고 싶은 것을 치거나, 왼쪽 Ctrl을 누르고 말하시오…" : "묻고 싶은 것을 치시오…")
                    : (v ? "그립을 누르고 말하시오…" : "물을 수단이 없다 — 받아쓰기를 붙일 것");
            }
        }

        // ── 작은 도구들 (소지품 판과 같은 것들) ──────────────────
        /// <summary>본문 글꼴 — 판마다 따로 만들지 않고 <see cref="UiSkin.Font"/> 하나를 함께 쓴다.
        /// 2026-08-26 에 OS 글꼴(맑은 고딕)에서 TMP 폰트 에셋(조선 궁서체)으로 옮겼다.</summary>
        static TMPro.TMP_FontAsset MakeFont() { return UiSkin.Font; }

        static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;   // UGUI 이벤트는 안 쓴다 — 판정은 콜라이더 광선이 한다
            return (RectTransform)go.transform;
        }

        TextMeshProUGUI MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = UiSkin.Dress(go.GetComponent<TextMeshProUGUI>(), size, anchor, color);
            t.textWrappingMode = TextWrappingModes.Normal;
            // ⚠️ 레거시의 Truncate 를 TMP 의 Truncate 로 그대로 옮기면 안 된다 (2026-08-26 실측).
            //    TMP 는 상자 높이에 <b>온전히 들어가지 않는 줄을 통째로 버린다</b>. 조선 궁서체는
            //    줄 높이가 글자 크기의 1.25배라 맑은 고딕(약 1.18배)보다 높은데, IMGUI 시절 숫자로
            //    잡아 둔 상자들이 그만큼의 여유가 없다 — 대화창 아래 조작 안내(22px 글, 26px 상자)가
            //    <b>한 줄 통째로 사라졌다</b>. 1.5px 모자란 것이 원인이라 화면에서는 원인이 안 보인다.
            //    레거시가 실제로 그리던 모습은 Overflow 쪽이다 — 여러 줄 글은 어차피 판(RectMask2D)이
            //    잘라 주므로 여기서 버릴 이유가 없다.
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-inset, -inset);
            rt.offsetMax = new Vector2(inset, inset);
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
