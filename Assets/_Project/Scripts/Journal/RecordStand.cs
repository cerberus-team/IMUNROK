using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 기록대: 완료된 사건의 판결이 판결패(납작한 큐브)로 쌓이는 곳.
    ///
    /// GameState를 구독해, 사건이 완료(Completed)될 때마다 StackAnchor 위에
    /// 판결패를 아래→위로 쌓는다. 판결패 색은 판결(Verdict)에 따라 달라져
    /// 지금까지의 판결 경향을 한눈에 볼 수 있다(엔딩 총평 분기와 연결됨).
    ///
    /// 판결패는 런타임에 생성/삭제되므로 저장된 씬에는 남지 않는다(항상 상태에서 재구성).
    /// </summary>
    public class RecordStand : MonoBehaviour
    {
        [Tooltip("판결패가 쌓일 기준점(비우면 이 오브젝트 자신을 기준으로)")]
        [SerializeField] private Transform _stackAnchor;

        [Header("판결패 모양")]
        [SerializeField] private Vector3 _plaqueSize = new Vector3(0.5f, 0.06f, 0.35f);
        [Tooltip("판결패 사이 간격")]
        [SerializeField] private float _gap = 0.02f;

        [Header("판결별 색")]
        [SerializeField] private Color _truthColor      = new Color(0.95f, 0.93f, 0.80f); // 진실 — 맑은 상아색
        [SerializeField] private Color _mercyColor      = new Color(0.45f, 0.68f, 1.00f); // 정상참작 — 청색
        [SerializeField] private Color _acceptFakeColor = new Color(0.85f, 0.30f, 0.25f); // 갑 인정 — 적색
        [SerializeField] private Color _defaultColor    = new Color(1.00f, 0.84f, 0.00f); // 그 외 — 금색

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private GameState _state;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>에디터 생성기에서 기준점을 지정할 때 사용.</summary>
        public void Initialize(Transform stackAnchor)
        {
            _stackAnchor = stackAnchor;
        }

        private void OnEnable()
        {
            _state = GameState.Instance;
            _state.OnCaseChanged += HandleCaseChanged;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_state != null)
                _state.OnCaseChanged -= HandleCaseChanged;
        }

        private void HandleCaseChanged(CaseId _) => Rebuild();

        /// <summary>현재 완료된 사건들로 판결패 스택을 처음부터 다시 만든다.</summary>
        private void Rebuild()
        {
            // 기존 판결패 제거
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();

            Transform anchor = _stackAnchor != null ? _stackAnchor : transform;

            // 사건 순서(Case1→2→3)대로, 완료된 것만 아래에서 위로 쌓는다.
            int level = 0;
            foreach (CaseId id in Enum.GetValues(typeof(CaseId)))
            {
                if (_state.GetStatus(id) != CaseStatus.Completed) continue;

                var plaque = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plaque.name = $"VerdictPlaque_{id}";
                plaque.transform.SetParent(anchor, false);
                float y = _plaqueSize.y * 0.5f + level * (_plaqueSize.y + _gap);
                plaque.transform.localPosition = new Vector3(0f, y, 0f);
                plaque.transform.localScale = _plaqueSize;

                // 표시 전용이라 콜라이더 제거(선택 레이 방해 방지)
                var col = plaque.GetComponent<Collider>();
                if (col != null) Destroy(col);

                SetColor(plaque, ColorFor(_state.GetVerdict(id)));

                _spawned.Add(plaque);
                level++;
            }
        }

        private Color ColorFor(Verdict v)
        {
            return v switch
            {
                Verdict.Truth      => _truthColor,
                Verdict.Mercy      => _mercyColor,
                Verdict.AcceptFake => _acceptFakeColor,
                _                  => _defaultColor,
            };
        }

        private void SetColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
