using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>크레딧이 사방을 두른다.</b>
    ///
    /// 앞서 크레딧을 한지 한 장에 적어 눈앞에 띄웠다. 그릇은 맞았는데 <b>크기가
    /// 틀렸다</b> — 만든 사람 셋과 빌려 온 것 여남은 줄이 손바닥만 한 종이 한 장에
    /// 다 들어가니, 끝을 맺는 것이 아니라 <b>쪽지 한 장 읽고 마는</b> 것이 됐다.
    /// 게다가 그 종이 하나 말고는 온 사방이 텅 비어 있다.
    ///
    /// 그래서 <b>눈앞</b>이 아니라 <b>둘레</b>에 적는다.
    ///
    /// 글이 사람을 <b>고리처럼 둘러싸고</b>, 고리가 아주 느리게 돌면서 함께
    /// 떠오른다. 두 가지가 한꺼번에 풀린다:
    ///   · <b>가만히 서 있어도 다 읽힌다</b> — 고리가 한 바퀴를 도는 동안 모든 글이
    ///     한 번씩 정면을 지난다. 뒤에 있어서 못 본 것이 없다.
    ///   · <b>둘러보면 더 보인다</b> — 시선을 돌리면 지나간 것과 올 것이
    ///     사방에 걸려 있다. 화면 한 장짜리 크레딧에는 없는 값이다.
    ///
    /// <b>한 바퀴와 한 번 떠오름을 같은 시간에 맞춘다.</b> 둘이 어긋나면 어떤 글은
    /// 발밑에서 정면을 지나고 어떤 글은 머리 위에서 지난다. 같은 시간이면 글마다
    /// 정면을 지나는 <b>높이가 다 다르되 차례가 고르다</b> — 오르막을 한 줄로 걸어
    /// 올라가는 모양이 된다.
    ///
    /// <b>고리를 작게 두는 까닭</b>: 복명이 끝나면 어사는 어전 앞 문지방 밖(z 약 -7.3)에
    /// 서 있다. 고리를 크게 잡으면 어전 건물 <b>안으로 파고들어</b> 글씨가 벽에 잘린다.
    /// 3.2m 면 그 건물에 안 닿으면서도 시야를 가득 채운다 — 가까울수록 크게 보이므로
    /// 「크게 두른다」는 반지름이 아니라 <b>각</b>의 문제다.
    /// </summary>
    public class CreditsRing : MonoBehaviour
    {
        /// <summary>글이 걸리는 고리의 반지름(m).</summary>
        private const float Radius = 3.2f;

        /// <summary>글 한 줄의 크기가 눈에 걸리는 각을 정한다. 1 캔버스 단위 = 이만큼 m.</summary>
        private const float Scale = 0.0042f;

        /// <summary>
        /// 떠오르기 시작하는 높이(눈에서, m).
        ///
        /// <b>높이를 좁게 잡는다.</b> 처음에 -1.9 에서 +2.7 까지 올렸더니, 마지막 판들이
        /// 정면을 지날 때 고개를 <b>40도나 젖혀야</b> 보였다 — 화면에서는 위가 잘리고
        /// 너무 높거나 낮으면 읽기 나쁘다. 정면을 지나는 높이가 대략 눈에서 위아래 25도 안에
        /// 들도록 좁혔다. 「떠오른다」는 느낌은 그만큼으로 충분하다.
        /// </summary>
        private const float FromY = -1.45f;

        /// <summary>다 떠오른 높이(눈에서, m).</summary>
        private const float ToY = 1.60f;

        /// <summary>배어 나오고 스러지는 데 걸리는 시간(초).</summary>
        private const float Fade = 2.2f;

        private string[] _blocks;
        private float _seconds;
        private Action _then;

        private Vector3 _hub;                 // 고리의 한가운데 — 처음 한 번만 잡는다
        private float _eyeY;
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
        private readonly List<Transform> _rims = new List<Transform>();
        private readonly List<float> _angles = new List<float>();

        /// <summary>
        /// 글 뭉치들을 둘레에 걸고 한 바퀴 돌린다.
        /// </summary>
        /// <param name="blocks">뭉치 하나가 판 하나다. 빈 줄은 알아서 걸러진다</param>
        /// <param name="seconds">한 바퀴 도는 데 걸리는 시간(초)</param>
        /// <param name="then">
        /// 뒤를 이을 것. <b>주면</b> 한 바퀴 뒤에 글이 스러지고 이것이 불린다.
        /// <b>안 주면</b> 고리가 멎은 자리에 그대로 남는다 — 크레딧이 끝이면 그쪽이 옳다.
        /// </param>
        public static CreditsRing Show(IList<string> blocks, float seconds = 46f, Action then = null)
        {
            var keep = new List<string>();
            if (blocks != null)
                foreach (var b in blocks)
                    if (!string.IsNullOrEmpty(b) && b.Trim().Length > 0) keep.Add(b.Trim());
            if (keep.Count == 0) { if (then != null) then(); return null; }

            var go = new GameObject("크레딧_두름");
            var ring = go.AddComponent<CreditsRing>();
            ring._blocks = keep.ToArray();
            ring._seconds = Mathf.Max(6f, seconds);
            ring._then = then;
            return ring;
        }

        private void Start() { StartCoroutine(Run()); }

        private IEnumerator Run()
        {
            var cam = Camera.main;
            if (cam == null) { if (_then != null) _then(); Destroy(gameObject); yield break; }

            // 고리의 한가운데는 <b>지금 사람이 선 자리</b>다. 카메라를 따라다니게 하면
            // 시선을 돌릴 때 글이 같이 돌면 <b>영영 못 읽는다</b>.
            _hub = cam.transform.position;
            _eyeY = _hub.y;
            _hub.y = 0f;

            Build();

            // ── 배어 나온다 ─────────────────────────
            float k = 0f;
            while (k < 1f)
            {
                k += Time.deltaTime / Fade;
                Place(0f);
                SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k)));
                yield return null;
            }

            // ── 한 바퀴 돌며 떠오른다 ───────────────
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / _seconds;
                Place(Mathf.Clamp01(t));
                yield return null;
            }

            // ── 멎는다 ─────────────────────────────
            //
            // <b>스러지게 두지 않는다.</b> 한 바퀴가 끝나고 글이 다 사라지면 캄캄한
            // 어전만 남아 아무것도 없는 화면을 하염없이 보게 된다. 앞서 크레딧을
            // 한지에 적었을 때도 <b>내려놓을 수 없게</b> 막아 두었던 것은 같은 까닭이다.
            //
            // 고리는 멎은 자리에 그대로 걸려 있는다. 사방에 글이 남으니, 고개를
            // 돌리면 무엇이든 읽을 것이 있다. 뒤를 이을 것이 있을 때만 스러진다.
            Place(1f);
            if (_then == null) yield break;

            float s = 0f;
            while (s < 1f)
            {
                s += Time.deltaTime / Fade;
                SetAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s)));
                yield return null;
            }

            _then();
            Destroy(gameObject);
        }

        /// <summary>
        /// 판을 고리에 건다. 첫 뭉치가 <b>정면</b>에서 시작하고 나머지는 고르게 벌어진다 —
        /// 첫 줄이 「플레이해 주셔서 감사합니다」이므로 그것만은 안 놓쳐야 한다.
        /// </summary>
        private void Build()
        {
            var cam = Camera.main;
            // 사람이 보고 있는 쪽이 0도다. 세계의 +z 를 0도로 잡으면 어느 쪽을 보고
            // 끝났느냐에 따라 첫 뭉치가 등 뒤에서 시작한다.
            Vector3 face = cam.transform.forward; face.y = 0f;
            float home = face.sqrMagnitude < 0.0001f ? 0f
                       : Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;

            float step = 360f / _blocks.Length;
            var font = UiFont.Resolve(null);

            for (int i = 0; i < _blocks.Length; i++)
            {
                var go = new GameObject("글_" + i, typeof(Canvas), typeof(CanvasGroup));
                go.transform.SetParent(transform, false);

                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = cam;

                var rt = canvas.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(900f, 520f);
                rt.localScale = Vector3.one * Scale;

                var txtGo = new GameObject("줄", typeof(Text));
                var trt = txtGo.GetComponent<RectTransform>();
                trt.SetParent(rt, false);
                trt.sizeDelta = rt.sizeDelta;

                var txt = txtGo.GetComponent<Text>();
                txt.font = font;
                txt.fontSize = 44;
                txt.lineSpacing = 1.35f;
                txt.alignment = TextAnchor.MiddleCenter;
                // 어둠 위의 글씨다. 순백은 눈을 찌르고, 붉은 것은 경고문으로 읽힌다 —
                // 앞서 엔딩이 통째로 붉어 그 꼴이 났다. 한지빛으로 둔다.
                txt.color = UiLook.Text;
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.text = _blocks[i];

                _groups.Add(go.GetComponent<CanvasGroup>());
                _rims.Add(rt);
                _angles.Add(home + i * step);
            }
        }

        /// <summary>
        /// <paramref name="t"/> 는 0에서 1까지. 고리를 그만큼 돌리고 그만큼 띄운다.
        /// </summary>
        private void Place(float t)
        {
            float turn = t * 360f;
            float y = _eyeY + Mathf.Lerp(FromY, ToY, t);

            for (int i = 0; i < _rims.Count; i++)
            {
                if (_rims[i] == null) continue;
                // <b>거꾸로 돈다</b>(-turn). 고리를 왼쪽으로 돌려야 판이 오른쪽에서
                // 나와 왼쪽으로 지나간다 — 글을 읽는 쪽과 같은 방향이라 눈이 안 거스른다.
                float a = (_angles[i] - turn) * Mathf.Deg2Rad;
                var at = new Vector3(_hub.x + Mathf.Sin(a) * Radius, y,
                                     _hub.z + Mathf.Cos(a) * Radius);
                _rims[i].position = at;
                // 판은 고리의 <b>한가운데</b>를 본다. 카메라를 보게 하면 고개를 돌릴 때마다
                // 판이 따라 돌아 고리가 아니라 벽이 된다.
                _rims[i].rotation = Quaternion.LookRotation(
                    new Vector3(at.x - _hub.x, 0f, at.z - _hub.z), Vector3.up);
            }
        }

        private void SetAlpha(float a)
        {
            for (int i = 0; i < _groups.Count; i++)
                if (_groups[i] != null) _groups[i].alpha = a;
        }
    }
}
