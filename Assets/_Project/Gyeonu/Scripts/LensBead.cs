using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 볼록렌즈 구슬 한 알 (2026-08-24). 처음에는 서안 위에 놓여 있고,
    /// 문갑 상판을 조사하는 순간 <see cref="LensPuzzle"/>이 판 위로 옮겨 놓는다.
    ///
    /// ■ 하는 일은 두 가지뿐
    ///   ① 판 위 좌표(<see cref="PanelPos"/>)를 트랜스폼과 맞춰 둔다.
    ///   ② 확대·자리를 재질의 **텍스처 타일·오프셋**으로 옮긴다 (셰이더는 URP Lit 그대로).
    ///   조명은 URP가 판에 하는 것과 같은 코드로 한다 — 어긋날 여지가 없다.
    ///
    /// ■ 왜 Interactable인가
    ///   판 밖(서안 위)에 있을 때는 **조사할 수 있는 물건**이어야 한다.
    ///   "밑에 놓인 것을 크게 비춘다"를 여기서 한 번 알려 줘야 퍼즐이 갑자기 튀어나오지 않는다.
    ///   판 위로 올라간 뒤에는 조준 대상에서 빠진다(포커스 안에서만 만진다) —
    ///   ⚠️ 콜라이더를 켠 채 두면 상판을 겨눌 때 구슬이 먼저 맞아 **판이 조준되지 않는다.**
    ///      그래서 판 위에서는 레이어를 Ignore Raycast로 옮긴다. 포커스 쪽은
    ///      <see cref="Collider.Raycast"/>로 직접 쏘므로 레이어와 무관하게 잡힌다.
    /// </summary>
    [ExecuteAlways]
    public class LensBead : Interactable
    {
        [Tooltip("소속 퍼즐 — 판 좌표계를 여기서 얻는다")]
        public LensPuzzle puzzle;

        [Tooltip("렌즈 반지름(m). 돔 메시의 밑면 반지름과 같아야 한다")]
        public float radius = 0.018f;

        [Tooltip("판 위에 올라와 있는가")]
        public bool onPanel;

        [Tooltip("판 로컬 좌표(m). onPanel일 때만 뜻이 있다")]
        public Vector2 panelPos;

        [TextArea]
        public string inspectNote = "맑은 유리 구슬이다. 한쪽이 볼록해서, 밑에 놓인 것이 크게 부풀어 보인다.";

        [Header("재질 — 판 위/판 밖이 서로 다른 셰이더를 쓴다")]
        [Tooltip("판 위: 그림을 확대해 비추는 URP Lit 재질 (구슬마다 하나씩 — 타일·오프셋이 다르다)")]
        public Material panelMaterial;
        [Tooltip("판 밖(서안 위): 그냥 유리 구슬")]
        public Material looseMaterial;

        Renderer rend;
        Collider col;
        Material runtimeMat;   // Play 중에는 에셋을 더럽히지 않도록 사본을 쓴다

        public Vector2 PanelPos => panelPos;
        public Collider Col { get { if (col == null) col = GetComponent<Collider>(); return col; } }

        public override string Prompt => "살펴보기";
        public override bool CanInteract(GameObject actor) => !onPanel;
        public override void Interact(GameObject actor) => DebugToast.ShowPinned(inspectNote);

        void OnEnable() { Refresh(); }
        void Update() { if (!Application.isPlaying) Refresh(); }   // 에디터에서도 미리 보인다

        /// <summary>판 위 좌표를 정하고 트랜스폼·셰이더를 함께 맞춘다.</summary>
        public void SetPanelPos(Vector2 p)
        {
            panelPos = p;
            onPanel = true;
            if (puzzle != null)
            {
                var t = puzzle.PanelTransform;
                if (t != null)
                {
                    transform.SetPositionAndRotation(
                        puzzle.PanelToWorld(p) + t.up * puzzle.beadLift, t.rotation);
                }
            }
            Refresh();
        }

        /// <summary>판 밖(서안 위 등)에 그대로 두는 상태로 되돌린다.</summary>
        public void SetOffPanel()
        {
            onPanel = false;
            Refresh();
        }

        /// <summary>
        /// 재질·레이어 갱신. 판을 옮기거나 크기를 바꾼 뒤에 부른다.
        ///
        /// 확대와 자리를 **텍스처 타일/오프셋 하나**로 표현한다. 돔 메시의 UV가
        /// "렌즈 반지름으로 정규화한 판 위 오프셋"(−1~1)이라, 그림 UV는 아핀 사상으로 떨어진다:
        ///     그림UV = (R / (배율 · 판크기)) · uv + (렌즈중심 / 판크기 + 0.5)
        /// 덕분에 렌즈는 **URP Lit 그대로** 쓸 수 있고, 조명이 판과 어긋날 여지가 없다.
        /// (커스텀 셰이더로 조명을 손수 재현했을 때는 4배 어두워졌고 원인 추적도 오래 걸렸다.)
        /// </summary>
        public void Refresh()
        {
            if (rend == null) rend = GetComponent<Renderer>();
            if (rend == null) return;

            var want = onPanel ? panelMaterial : looseMaterial;

            int wantLayer = onPanel ? 2 : 0;      // 2 = Ignore Raycast
            if (gameObject.layer != wantLayer) gameObject.layer = wantLayer;

            if (!onPanel)
            {
                if (want != null && rend.sharedMaterial != want) rend.sharedMaterial = want;
                if (runtimeMat != null) { Destroy(runtimeMat); runtimeMat = null; }
                return;
            }
            if (!Application.isPlaying && want != null && rend.sharedMaterial != want)
                rend.sharedMaterial = want;
            if (puzzle == null || puzzle.PanelTransform == null) return;

            var size = puzzle.panelSize;
            if (size.x <= 1e-4f || size.y <= 1e-4f) return;
            float mag = Mathf.Max(0.01f, puzzle.magnification);
            var tiling = new Vector2(radius / (mag * size.x), radius / (mag * size.y));
            var offset = new Vector2(panelPos.x / size.x + 0.5f, panelPos.y / size.y + 0.5f);

            // Play 중에는 재질 에셋을 고치면 안 된다 — 사본을 만들어 그쪽에 쓴다.
            if (Application.isPlaying)
            {
                if (runtimeMat == null || rend.sharedMaterial != runtimeMat)
                {
                    runtimeMat = new Material(want != null ? want : rend.sharedMaterial);
                    runtimeMat.name = name + "_런타임";
                    rend.sharedMaterial = runtimeMat;
                }
                runtimeMat.SetTextureScale("_BaseMap", tiling);
                runtimeMat.SetTextureOffset("_BaseMap", offset);
            }
            else if (want != null)
            {
                want.SetTextureScale("_BaseMap", tiling);
                want.SetTextureOffset("_BaseMap", offset);
            }
        }

        void OnDestroy() { if (runtimeMat != null) Destroy(runtimeMat); }
    }
}
