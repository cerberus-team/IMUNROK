using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>서가 칸 깊숙이 끼워 둔 문서가 빠져나와 바닥에 떨어진다.</b>
    ///
    /// 불빛이 그 칸에 앉는 순간(<see cref="LightBeam"/> 의 「드러나는 순간」) <see cref="Drop"/> 을 부른다.
    /// 문서고 벽의 책더미는 여태 아무 쓸모가 없었다 — 불빛이 닿아도 아무 일이 없으니 비출 까닭이
    /// 없었다. 이제 한 칸에만 먼지가 흐트러져 있고, 불빛을 거기 앉히면 뒤에 밀어 넣어 두었던 종이가
    /// 스르르 빠져나와 떨어진다. 떨어지기 전에는 짚이지 않는다 — 칸 속에 있는 것을 멀리서 집을 수는 없다.
    ///
    /// 붙이는 곳: 떨어질 문서(콜라이더와 <see cref="InspectableNote"/> 가 함께 있는 것).
    /// </summary>
    public class DropFromShelf : MonoBehaviour
    {
        [Tooltip("칸 밖으로 빠져나오는 쪽(월드). 비우면 이 물건의 앞(-forward 가 아니라 +forward)")]
        [SerializeField] private Vector3 _outward = Vector3.zero;
        [SerializeField] private float _slide = 0.28f;
        [SerializeField] private float _slideSeconds = 0.45f;
        [Tooltip("떨어져 멎을 바닥 높이(월드 y). 0 이면 아래로 재서 찾는다")]
        [SerializeField] private float _floorY = 0f;
        [SerializeField] private AudioClip _thud;
        [Tooltip("떨어진 뒤 한 번 뜨는 말. 비우면 안 뜬다")]
        [TextArea(2, 3)] [SerializeField] private string _line = "";

        private Collider[] _cols;
        private InspectableNote _note;
        private bool _dropped;

        public bool Dropped => _dropped;

        private void Awake()
        {
            _cols = GetComponentsInChildren<Collider>(true);
            _note = GetComponent<InspectableNote>();
            foreach (var c in _cols) c.enabled = false;
            if (_note != null) _note.enabled = false;
        }

        public void Drop()
        {
            if (_dropped || !isActiveAndEnabled) return;
            _dropped = true;
            StartCoroutine(Fall());
        }

        private IEnumerator Fall()
        {
            Vector3 outward = _outward.sqrMagnitude > 0.0001f ? _outward.normalized : transform.forward;
            outward.y = 0f; outward.Normalize();

            // ① 스르르 빠져나온다 — 끝이 걸려 한 번 멈칫
            Vector3 a = transform.position, b = a + outward * _slide;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, _slideSeconds);
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            yield return new WaitForSeconds(0.12f);

            // ② 떨어진다 — 앞으로 조금 기울며 곤두박질하다 바닥에 눕는다
            float floor = _floorY;
            if (Mathf.Approximately(floor, 0f))
            {
                RaycastHit hit;
                floor = Physics.Raycast(transform.position + outward * 0.1f, Vector3.down, out hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                      ? hit.point.y : transform.position.y - 1f;
            }
            Quaternion from = transform.rotation;
            Vector3 flatFwd = outward;
            Quaternion lie = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * Quaternion.LookRotation(flatFwd, Vector3.up);
            float v = 0f;
            Vector3 p = transform.position;
            float startY = p.y, drop = Mathf.Max(0.05f, startY - (floor + 0.012f));
            while (p.y > floor + 0.012f)
            {
                v += 9.8f * Time.deltaTime;
                p.y -= v * Time.deltaTime;
                p += outward * (0.35f * Time.deltaTime);
                float k = Mathf.Clamp01((startY - p.y) / drop);
                transform.position = new Vector3(p.x, Mathf.Max(p.y, floor + 0.012f), p.z);
                transform.rotation = Quaternion.Slerp(from, lie, k);
                yield return null;
            }
            transform.rotation = lie;

            // ③ 한 번 들썩
            Vector3 rest = transform.position;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.18f;
                transform.position = rest + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.02f;
                yield return null;
            }
            transform.position = rest;

            if (_thud != null) AudioSource.PlayClipAtPoint(_thud, rest, 0.8f);
            foreach (var c in _cols) c.enabled = true;
            if (_note != null) _note.enabled = true;
            if (!string.IsNullOrEmpty(_line)) SubtitleView.Show("", _line, "", false);
            // 바닥에 떨어진 종이는 서가 그늘에 묻혀 놓치기 쉽다 — 집을 때까지 길표를 세운다
            WayMark.Show(transform, "떨어진 종이");
            _marked = true;
        }

        private bool _marked;

        private void Update()
        {
            if (!_marked) return;
            if (DocumentView.IsOpen || !WayMark.Up) { _marked = false; WayMark.Hide(); }
        }
    }
}
