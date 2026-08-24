using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 비밀지도 위에 뜨는 별빛 길 (2026-08-23). 지도 프리팹의 <c>별빛길</c> 자식에 붙는다.
    ///
    /// ■ 평소에는 없는 것처럼
    ///   종이에 뚫린 구멍은 늘 있지만 빛은 없다. 그래서 기본값은 세기 0 — 렌더러까지 꺼 둔다.
    ///   빛을 넣는 쪽(<see cref="SecretMapReveal"/>)만 <see cref="SetProgress"/>를 부른다.
    ///
    /// ■ 왜 구간을 나눠 두었나
    ///   "길이 서서히 **뻗어 나가는**" 그림이 필요하다. 별 셰이더(IMUNROK/별_가산)에는 전역
    ///   _Intensity 하나뿐이라 한 덩어리로는 전체가 동시에 밝아질 뿐이다. 천장 별과 같은
    ///   셰이더를 그대로 쓰면서 흐름을 얻으려고, 빌더가 별 메시를 **경로 순서대로** 12토막으로
    ///   잘라 두었다. 토막마다 제 MPB를 쥐고 있어 앞에서 뒤로 차례로 밝아진다.
    ///   (셰이더를 고치면 천장 별·은하담 별길까지 함께 흔들린다 — 건드리지 않는 편을 택했다)
    ///
    /// ■ 한 번 밝힌 뒤에는
    ///   <see cref="visibleFlag"/>가 서 있으면 <see cref="Awake"/>에서 이미 켜진 채로 뜬다.
    ///   소지품 상세에서 다시 꺼내 보아도 길이 남아 있다 — 본 것은 사라지지 않는다.
    /// </summary>
    [AddComponentMenu("이문록/비밀지도 별빛 길 (SecretMapStarPath)")]
    [DisallowMultipleComponent]
    public class SecretMapStarPath : MonoBehaviour
    {
        [Tooltip("경로 순서대로 잘라 둔 별 메시 토막들 (빌더가 채운다)")]
        public Renderer[] segments;

        [Tooltip("이 플래그가 서 있으면 처음부터 밝은 채로 뜬다. 비우면 항상 꺼진 채 시작")]
        public string visibleFlag = GyeonuWorld.F_타공지도_길밝힘;

        [Tooltip("앞 토막이 다 밝기 전에 뒤 토막이 물들기 시작하는 겹침(토막 수). " +
                 "0이면 칸칸이 딱딱 끊겨 보인다")]
        public float overlap = 2.6f;

        [Tooltip("전체 밝기 배수. 별 하나하나의 세기는 메시에 구워져 있으므로, 배경(종이)이 " +
                 "밝고 어두운 정도에 맞춰 여기서만 조절한다 — 메시를 다시 구울 필요가 없다")]
        public float brightness = 1f;

        [Header("바탕 물리기")]
        [Tooltip("길이 밝을 때 함께 물릴 종이 렌더러 (빌더가 채운다). 비우면 아무것도 안 한다")]
        public Renderer paperRenderer;

        [Tooltip("길이 다 밝았을 때 종이에 먹이는 배수. 0이면 종이를 건드리지 않는다 — " +
                 "관측실 연출처럼 종이를 스스로 다루는 쪽은 0으로 꺼 둔다")]
        public float paperDimWhenLit = 0.62f;

        [Tooltip("종이 재질의 원래 발광 세기 (물릴 때 같은 비율로 낮춘다). M_비밀지도_앞과 맞춘다")]
        public float paperEmission = 0.13f;

        MaterialPropertyBlock mpb;
        MaterialPropertyBlock paperMpb;

        /// <summary>0이면 아무것도 안 보이고 1이면 길 전체가 밝다.</summary>
        public float Progress { get; private set; }

        void Awake()
        {
            if (segments == null || segments.Length == 0)
                segments = GetComponentsInChildren<Renderer>(true);
            SetProgress(!string.IsNullOrEmpty(visibleFlag) && GyeonuWorld.Has(visibleFlag) ? 1f : 0f);
        }

        /// <summary>길을 앞에서부터 p만큼 밝힌다 (0~1).</summary>
        public void SetProgress(float p)
        {
            Progress = Mathf.Clamp01(p);
            if (segments == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();

            int n = segments.Length;
            float ov = Mathf.Max(0.01f, overlap);
            float front = Progress * (n + ov);      // 밝기의 앞머리가 지금 어디까지 왔나

            for (int i = 0; i < n; i++)
            {
                var r = segments[i];
                if (r == null) continue;
                float k = Mathf.Clamp01((front - i) / ov);
                k = Mathf.SmoothStep(0f, 1f, k);
                // 세기 0인 토막은 아예 그리지 않는다 — 가산 셰이더라 0이면 어차피 안 보이지만
                // 손에 든 물건이 매 프레임 12번 그려질 이유가 없다
                r.enabled = k > 0.002f;
                if (!r.enabled) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat("_Intensity", k * brightness);
                r.SetPropertyBlock(mpb);
            }

            ApplyPaperDim();
        }

        /// <summary>
        /// 길이 밝을수록 종이를 물린다.
        ///
        /// ⚠️ **이게 없으면 밝은 데서는 길이 아예 안 보인다** (2026-08-23 실측).
        /// 별은 가산(One One)이라 바탕에 빛을 **더하기만** 한다. 소지품 미리보기 무대는
        /// 점광원 셋이 종이를 0.72까지 끌어올려 놓아서, 세기를 3배로 올리든 24배로 올리든
        /// 흰 종이가 조금 더 하얘질 뿐 선으로 읽히지 않았다(최대 차 0.64에서 포화).
        /// 종이를 0.62로 물려 자리를 내주면 같은 별빛이 대번에 길로 보인다.
        ///
        /// 재질이 아니라 **이 개체의 MPB**로 먹인다 — 재질을 건드리면 아직 길을 못 밝힌
        /// 다른 지도(그리고 씬에 놓인 프리팹)까지 함께 어두워진다.
        /// </summary>
        void ApplyPaperDim()
        {
            if (paperRenderer == null || paperDimWhenLit <= 0f) return;
            if (paperMpb == null) paperMpb = new MaterialPropertyBlock();

            float dim = Mathf.Lerp(1f, paperDimWhenLit, Progress);
            paperRenderer.GetPropertyBlock(paperMpb);
            paperMpb.SetColor("_BaseColor", new Color(dim, dim, dim, 1f));
            float e = paperEmission * dim;
            paperMpb.SetColor("_EmissionColor", new Color(e, e, e, 1f));
            paperRenderer.SetPropertyBlock(paperMpb);
        }
    }
}
