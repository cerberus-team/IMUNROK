using TMPro;
using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 판들이 함께 쓰는 글꼴·스프라이트 (2026-08-26).
    ///
    /// ⚠️ 정적 캐시는 이 프로젝트에서 위험하다 — 도메인 리로드가 꺼져 있어 정적 참조가
    ///    플레이 세션을 넘겨 살아남는데 <b>텍스처 내용은 죽는다</b>(비네트·소지품 판에서
    ///    실측). 그래서 <see cref="ResetStatics"/> 로 세션마다 반드시 버린다.
    ///    이 규칙을 어기면 두 번째 Play에서 판이 흰 사각형이 된다.
    /// </summary>
    public static class UiSkin
    {
        /// <summary>
        /// <b>IMGUI 시절 알파를 그대로 써도 되는가</b> — 재어 보고 답을 낸 자리다 (2026-08-26).
        ///
        /// 처음엔 "이 프로젝트는 리니어 색공간이니 IMGUI(감마 합성) 알파를
        /// <c>1−(1−a)^2.2</c> 로 올려야 한다"고 보고 그렇게 고쳤다. <b>재어 보니 그게 아니었다.</b>
        /// 같은 배경(밝은 하늘) 위 안내 상자(검정)의 화면 밝기:
        ///   IMGUI(α 0.55)        → <b>0.314</b>
        ///   월드 판 α 0.834      → 0.130
        ///   월드 판 α 0.55       → 0.142   ← <b>알파를 절반 가까이 내려도 거의 안 변한다</b>
        ///
        /// 즉 차이의 원인은 합성 공간이 아니라 <b>후처리 순서</b>다.
        /// IMGUI는 톤매핑이 <b>끝난 뒤</b> 화면에 덧그려졌고, 월드 판은 톤매핑 <b>전에</b> 섞인 뒤
        /// 함께 눌린다. 톤 곡선이 밝은 쪽을 압축하므로 밝은 배경 위에서 상자가 더 어둡게 앉는다.
        /// 나침반 바늘 색이 0.95 → 0.68 로 내려앉은 것도 같은 이유다.
        ///
        /// <b>결론: IMGUI 숫자를 그대로 쓴다.</b> 글씨는 그대로 밝고 자리·크기는 같다.
        /// 밝은 하늘 앞에서 상자가 조금 더 짙게 보이는 것이 유일한 차이이고, 읽기에는 오히려 낫다.
        /// 함수를 지우지 않고 남기는 까닭: 다음 사람이 "리니어니까 올려야 하지 않나" 하고
        /// 같은 길을 다시 갈 것이기 때문이다. 되살릴 일이 생기면 여기 한 줄만 고치면 된다.
        /// </summary>
        public static Color FromImgui(Color c) { return c; }

        // IMGUI 시절 색을 그대로 옮겼다 — PC에서 같아 보여야 한다
        public static readonly Color ToastText = new Color(1f, 0.92f, 0.7f);
        public static readonly Color ToastBack = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color HintText = new Color(1f, 1f, 1f, 0.55f);
        public static readonly Color StatusText = new Color(1f, 0.92f, 0.7f, 0.9f);
        public static readonly Color AimIdle = new Color(1f, 1f, 1f, 0.45f);
        public static readonly Color AimHot = Color.yellow;
        public static readonly Color NoteBack = new Color(0f, 0f, 0f, 0.62f);
        public static readonly Color NoteHead = new Color(1f, 0.88f, 0.62f, 0.95f);
        public static readonly Color NoteBody = new Color(0.93f, 0.90f, 0.83f, 0.92f);

        static TMP_FontAsset _font;
        static Sprite _white, _disc, _ring;
        static Texture2D _whiteTex, _discTex, _ringTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _font = null;
            _white = _disc = _ring = null;
            _whiteTex = _discTex = _ringTex = null;
            ApplyHangulLineBreaking();
        }

        /// <summary>
        /// 한글을 <b>낱자가 아니라 낱말 단위로</b> 줄바꿈하게 만든다 (2026-08-26).
        ///
        /// ■ 왜 필요한가
        ///   TMP 는 기본적으로 한글을 한자·가나와 한 묶음으로 보아 <b>아무 글자에서나</b> 줄을 끊는다.
        ///   그래서 "…수정편을 겹쳐 초점" / "을 맞춘다." 처럼 <b>낱말 한가운데가 갈라진다</b>
        ///   (레거시 UI.Text 는 띄어쓰기에서 끊었으므로 이건 TMP 로 옮기며 생긴 퇴보다).
        ///   TMP 에는 이걸 바로잡는 설정이 있다 — <c>Use Modern Hangul Line Breaking Rules</c>.
        ///
        /// ■ 왜 설정 파일이 아니라 코드에서 켜는가
        ///   그 설정은 <c>Assets/TextMesh Pro/Resources/TMP Settings.asset</c> 에 들어 있는데,
        ///   TMP 기본 리소스는 저장소에 넣지 않는다(각자 메뉴로 임포트 — .gitignore 참조).
        ///   팀원의 설정 파일은 갓 임포트한 기본값이라 이 항목이 꺼져 있다. 코드에서 켜 두면
        ///   <b>누구 컴퓨터에서든 같은 줄바꿈</b>이 나온다.
        ///
        /// ⚠️ TMP 가 공개 설정자를 주지 않아 비공개 필드를 직접 건드린다. TMP 판이 올라가
        ///    이름이 바뀌면 조용히 안 먹을 뿐 깨지지는 않는다 — 그때는 줄바꿈이 예전처럼
        ///    낱자 단위로 돌아가므로 이 자리를 의심할 것.
        /// </summary>
        static void ApplyHangulLineBreaking()
        {
            try
            {
                var settings = TMP_Settings.instance;
                if (settings == null) return;
                var f = typeof(TMP_Settings).GetField("m_UseModernHangulLineBreakingRules",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null && !(bool)f.GetValue(settings)) f.SetValue(settings, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[글꼴] 한글 줄바꿈 규칙을 켜지 못했다 — " + e.Message);
            }
        }

        // ── 글꼴 ─────────────────────────────────────────────

        /// <summary>Resources 안의 TMP 폰트 에셋 이름. <c>UiFontBaker</c> 가 굽는 것과 같아야 한다.</summary>
        public const string FontResource = "TMP_Hangul_ChosunCentennial";

        /// <summary>
        /// <b>크기 보정</b> — 레거시 <see cref="UnityEngine.UI.Text"/> 의 <c>fontSize</c> 를
        /// TMP 로 그대로 옮기면 글자가 달라 보인다. 두 체계가 같은 숫자를 다르게 읽기 때문이다:
        ///   · 레거시는 숫자를 <b>픽셀 em 크기</b>로 받아 OS 글꼴 래스터라이저에 넘긴다.
        ///   · TMP 는 숫자를 <b>포인트</b>로 받아 글꼴 얼굴의 상승/하강값으로 환산한다.
        /// 게다가 조선 궁서체는 맑은 고딕보다 글자 몸통(x-height)이 작아 같은 크기에서 잘게 보인다.
        ///
        /// 그래서 판마다 숫자를 흩어 고치는 대신 <b>여기 한 값</b>으로 전체를 맞춘다.
        /// 값을 바꾸면 모든 판이 함께 움직인다 — 자리·상자 크기는 건드리지 않으므로 배치가 안 깨진다.
        /// (실측으로 정한 값이다. 판 하나가 어색하면 그 판의 숫자가 아니라 이 값을 먼저 의심할 것)
        /// </summary>
        public const float FontScale = 1.0f;

        /// <summary>
        /// 본문 글꼴 — 조선 궁서체(ChosunCentennial). 한자는 이 에셋에 매달린 <b>폴백</b>
        /// (tkFangSong)이 자동으로 맡는다 — 부르는 쪽은 한자를 신경 쓸 필요가 없다.
        ///
        /// ⚠️ 원본 .ttf 와 구운 에셋은 둘 다 gitignore 대상이다 (LFS 사용량 0 방침).
        ///    처음 받은 사람은 <b>Tools ▸ 이문록 ▸ 글꼴 ▸ TMP 폰트 에셋 굽기</b> 를 한 번 눌러야 한다.
        ///    못 찾으면 TMP 기본 글꼴로 내려가되 한 번만 경고를 남긴다 — 화면이 통째로 비지 않게.
        /// </summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<TMP_FontAsset>(FontResource);
                if (_font == null)
                {
                    Debug.LogWarning("[글꼴] " + FontResource + " 를 찾지 못했다 — "
                                     + "Tools ▸ 이문록 ▸ 글꼴 ▸ TMP 폰트 에셋 굽기 를 눌러라. 임시로 기본 글꼴을 쓴다.");
                    _font = TMP_Settings.defaultFontAsset;
                }
                return _font;
            }
        }

        /// <summary>
        /// 레거시 <see cref="TextAnchor"/> 를 TMP 의 정렬로 옮긴다.
        /// IMGUI 시절 숫자를 그대로 물려받은 판들이 아직 TextAnchor 로 자리를 말하기 때문에,
        /// 부르는 쪽을 고치지 않고 여기서 한 번에 환산한다.
        /// </summary>
        public static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>
        /// 레거시 <c>Text.lineSpacing</c>(1.0 = 기본인 <b>배수</b>)을 TMP 값(0 = 기본,
        /// 글꼴 크기의 <b>백분율로 더하는 여분</b>)으로 옮긴다. 1.28 → 28.
        /// 이 환산을 빼먹으면 줄 간격이 글자 크기의 128배가 되어 글이 화면 밖으로 흩어진다.
        /// </summary>
        public static float LineSpacing(float legacyMultiplier)
        {
            return (legacyMultiplier - 1f) * 100f;
        }

        /// <summary>글 하나의 기본 차림. 판마다 흩어져 있던 설정을 한자리에 모았다.</summary>
        public static TextMeshProUGUI Dress(TextMeshProUGUI t, float fontSize, TextAnchor anchor, Color color)
        {
            t.font = Font;
            t.fontSize = fontSize * FontScale;
            t.alignment = Align(anchor);
            t.color = color;
            t.raycastTarget = false;
            t.richText = true;
            // SDF 는 글자 가장자리를 조금 넘겨 그린다 — 여분을 안 주면 획 끝이 잘린다
            t.extraPadding = true;
            t.enableAutoSizing = false;
            return t;
        }

        /// <summary>단색 상자용 1×1 흰 스프라이트.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                _whiteTex = Solid(Color.white);
                _white = Sprite.Create(_whiteTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                _white.name = "UI_흰판";
                return _white;
            }
        }

        /// <summary>꽉 찬 동그라미 — 나침반 눈금·조준 점.</summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc != null) return _disc;
                _discTex = Radial(64, (d) => d < 0.86f ? 1f : Mathf.Clamp01((1f - d) / 0.14f));
                _disc = Sprite.Create(_discTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
                _disc.name = "UI_원";
                return _disc;
            }
        }

        /// <summary>가운데가 빈 테두리 — 나침반 판 둘레.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                _ringTex = Radial(128, (d) => Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) / 0.07f));
                _ring = Sprite.Create(_ringTex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
                _ring.name = "UI_테";
                return _ring;
            }
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            { name = "UI_흰텍스처", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            t.SetPixels(px); t.Apply();
            return t;
        }

        /// <summary>중심에서의 거리(0~1)로 알파를 정하는 원형 텍스처.</summary>
        static Texture2D Radial(int res, System.Func<float, float> alpha)
        {
            var t = new Texture2D(res, res, TextureFormat.RGBA32, false)
            { name = "UI_원텍스처", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float c = (res - 1) * 0.5f;
            var px = new Color[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    px[y * res + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(d)));
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
