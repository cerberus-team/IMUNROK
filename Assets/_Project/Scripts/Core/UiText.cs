using TMPro;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>글씨를 짓는 한 자리.</b>
    ///
    /// 우리 판은 죄 낡은 <see cref="UnityEngine.UI.Text"/> 로 지어져 있었다. 글꼴 파일은
    /// 견우와 같은 조선궁서체인데도 두 화면을 나란히 놓으면 <b>글자가 달라 보였다</b> —
    /// 저쪽은 TMP(부호거리장, SDF)로 굽고 우리는 운영체제 래스터라이저에 맡겼기 때문이다.
    /// 같은 크기라도 SDF 쪽이 획이 또렷하고 조금 두껍다. 그 차이 하나로 우리 판만
    /// <b>덧그린 것</b>처럼 보이고, 같은 게임의 화면으로 안 읽힌다.
    ///
    /// <b>여기서 하는 일은 갈아 끼우는 일이지 새로 정하는 일이 아니다.</b> 글꼴·크기 보정·
    /// 정렬 환산은 전부 꾸러미(<see cref="IMUNROK.Ui.UiSkin"/>)가 이미 정해 두었으므로
    /// 그것을 그대로 부른다 — 저쪽이 값을 고치면 이쪽도 따라 움직인다.
    ///
    /// <b>부르는 쪽 모양을 안 바꿨다.</b> 판마다 <c>NewText(이름, 글, 자리, 크기, 어버이,
    /// 글씨크기, 색)</c> 로 부르고 있었으므로 <see cref="New"/> 도 같은 차례를 받는다.
    /// 그래야 판을 하나씩 옮기면서 배치가 안 흔들린다.
    /// </summary>
    public static class UiText
    {
        /// <summary>글 하나를 지어 어버이 밑에 앉힌다.</summary>
        public static TextMeshProUGUI New(string name, string content, Vector2 pos, Vector2 size,
                                          Transform parent, float fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var t = Dress(go.GetComponent<TextMeshProUGUI>(), fontSize, anchor, color);
            t.text = content ?? "";
            return t;
        }

        /// <summary>이미 있는 글에 우리 차림새를 입힌다.</summary>
        public static TextMeshProUGUI Dress(TextMeshProUGUI t, float fontSize, TextAnchor anchor, Color color)
        {
            IMUNROK.Ui.UiSkin.Dress(t, fontSize, anchor, color);
            t.textWrappingMode = TextWrappingModes.Normal;

            // ⚠️ <b>낡은 판의 Truncate 를 TMP 의 Truncate 로 그대로 옮기면 안 된다.</b>
            //    TMP 는 상자 높이에 <b>온전히 들어가지 않는 줄을 통째로 버린다</b>. 조선궁서체는
            //    줄 높이가 글자 크기의 1.25배라, 낡은 판 시절 숫자로 잡아 둔 상자에는 그만한
            //    여유가 없다 — 저쪽에서 1.5화소가 모자라 안내 줄이 통째로 사라진 적이 있다.
            //    낡은 판이 실제로 그리던 모습은 Overflow 쪽이고, 여러 줄 글은 어차피
            //    판(RectMask2D)이 잘라 준다. (견우 꾸러미의 실측 주석을 그대로 옮겨 적는다)
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        /// <summary>
        /// 줄을 접을까 말까. 낡은 판의 <c>horizontalOverflow</c> 자리다.
        ///
        /// 낡은 판은 <b>안 접는 것</b>이 기본이었고(Overflow) 접을 곳마다 따로 켰다.
        /// TMP 는 반대로 <b>접는 것</b>이 기본이다. 그대로 옮기면 한 줄로 두려던 글이
        /// 상자 폭에서 접혀 두 줄이 되므로, 옮기는 자리마다 이것을 짚어 준다.
        /// </summary>
        public static void Wrap(TMP_Text t, bool on)
        {
            if (t == null) return;
            t.textWrappingMode = on ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        }

        /// <summary>
        /// <b>글씨에 빛을 두른다</b> — 금물 글씨가 뜰 때 한 번 번지는 그것.
        ///
        /// 낡은 판에는 테두리를 겹치는 수밖에 없었다(<c>UI.Outline</c> 두 겹). TMP 의
        /// 부호거리장 셰이더에는 <b>번짐</b>이 따로 들어 있어, 글자 모양을 뭉개지 않고
        /// 둘레로만 빛이 퍼진다. 겹쳐 그리는 것이 아니라 <b>한 번에</b> 그리므로
        /// 글자 사이가 메워지지도 않는다.
        ///
        /// <paramref name="power"/> 0 이면 빛이 없다. 재질은 이 글 하나만의 것이라
        /// (<c>fontMaterial</c>) 다른 글에 옮아붙지 않는다.
        /// </summary>
        public static void Glow(TMP_Text t, Color color, float power)
        {
            if (t == null) return;
            var mat = t.fontMaterial;
            if (mat == null) return;
            if (power <= 0.001f)
            {
                mat.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
                return;
            }
            mat.EnableKeyword(ShaderUtilities.Keyword_Glow);
            mat.SetColor(ShaderUtilities.ID_GlowColor, color);
            mat.SetFloat(ShaderUtilities.ID_GlowPower, Mathf.Clamp01(power));
            mat.SetFloat(ShaderUtilities.ID_GlowInner, 0.05f);
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.4f);
        }

        /// <summary>
        /// 글씨 밑에 <b>그림자</b>를 깐다 — 어디에 얹혀도 글자가 남게.
        ///
        /// TMP 의 밑칠(Underlay)이다. 낡은 판의 <c>UI.Shadow</c>·<c>UI.Outline</c> 자리인데,
        /// 저쪽은 글자 꼴을 네 번 더 그리는 것이고 이쪽은 셰이더가 한 번에 한다.
        /// </summary>
        public static void Shadow(TMP_Text t, Color color, float offset = 0.6f, float softness = 0.2f)
        {
            if (t == null) return;
            var mat = t.fontMaterial;
            if (mat == null) return;
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
        }
    }
}
