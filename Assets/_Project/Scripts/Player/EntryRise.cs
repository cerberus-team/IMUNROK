using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 조사청에 처음 들어서는 <b>한 호흡</b> — 앉은 채로 방을 둘러보고, 다 보고 나서 일어선다.
    ///
    /// <b>왜 앉아서 시작하나</b>: 조사청은 사건을 고르는 방이지 뛰어다니는 방이 아니다.
    /// 서서 시작하면 들어서자마자 걸어 나가게 되고, 방이 <b>지나가는 곳</b>이 된다.
    /// 앉은 눈높이에서 시작해 고개부터 돌리면, 처음 하는 일이 <b>보는 일</b>이 된다 —
    /// 조사가 손을 대는 일이기 전에 보는 일이라는 것이 방에 앉는 자세로 먼저 나온다.
    ///
    /// <b>왜 일어서는 것이 나중인가</b>: 다 보고 나서 일어서야 <b>둘러본 끝에 일어선</b>
    /// 것이 된다. 일어서면서 돌면 둘이 뭉개져 그냥 카메라가 움직인 것으로 보인다.
    ///
    /// 흐름: 앉음 → (돌아본다) 창 쪽으로 → 잠깐 머문다 → 일어선다 → 손을 돌려준다
    ///
    /// 도는 동안에는 걷는 부품을 <b>통째로 끈다</b>. 잠그기만 하면 시점은 여전히
    /// 마우스를 따라가서, 저 혼자 도는 화면과 손이 서로 잡아당긴다.
    /// 끝나면 다시 켜면서 <see cref="DebugFlyCamera.SyncAngles"/> 로 <b>지금 보는 쪽</b>을
    /// 받아들이게 한다 — 안 그러면 마우스를 누르는 순간 옛 각도로 홱 돌아간다.
    ///
    /// 씬에 놓을 것이 없다. 조사청이 올라오면 저 혼자 붙고, <b>한 판에 한 번</b>만 한다 —
    /// 사건을 마치고 돌아올 때마다 다시 앉힐 수는 없다.
    /// </summary>
    public class EntryRise : MonoBehaviour
    {
        [Tooltip("앉은 채로 돌아볼 곳. 비우면 아래 이름으로 찾는다")]
        [SerializeField] private Transform _lookAt;
        [SerializeField] private string _lookAtPath = "조사청_실내/구조/창/창_중방_동";

        [Tooltip("일어서면서 이만큼 올라선다(m). 아래 '바닥에서 재기'를 끄면 이 값만 쓴다")]
        [SerializeField] private float _riseHeight = 0.50f;

        [Header("앉은 키 · 선 키 — 바닥에서 잰다")]
        [Tooltip("켜면 발밑 바닥을 찾아 <b>거기서부터</b> 앉은 키·선 키를 잡는다. " +
                 "끄면 씬에 잡아 둔 자리를 앉은 자리로 삼고 위의 값만큼만 올라선다")]
        [SerializeField] private bool _measureFromFloor = true;
        [Tooltip("<b>앉은 눈높이</b>(바닥에서 눈까지, m). 방바닥에 책상다리로 앉으면 0.9 남짓이다 — " +
                 "이보다 낮으면 마루에 드러누워 올려다보는 눈이 된다")]
        [SerializeField] private float _seatEye = 0.92f;
        [Tooltip("<b>선 눈높이</b>(m). 0이면 걷는 부품(DebugFlyCamera)이 쓰는 눈높이를 그대로 받아 쓴다")]
        [SerializeField] private float _standEye = 0f;
        [Tooltip("고개를 다 돌리는 데 걸리는 시간(초)")]
        [SerializeField] private float _turnSeconds = 3.2f;
        [Tooltip("다 보고 잠깐 머무는 시간(초). 이 사이가 없으면 보자마자 일어난 꼴이 된다")]
        [SerializeField] private float _holdSeconds = 0.7f;
        [Tooltip("일어서는 데 걸리는 시간(초)")]
        [SerializeField] private float _riseSeconds = 1.5f;

        /// <summary>한 판에 한 번. 사건에서 돌아올 때마다 다시 앉히지 않는다.</summary>
        private static bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_done) return;
            if (GameObject.Find("조사청_실내") == null) return;   // 조사청에서만

            // 헤드셋을 쓰고 있으면 <b>앉히지 않는다</b>. 앉은 눈높이는 카메라를 내려서
            // 만드는 것인데, VR 에서 눈높이를 정하는 것은 쓰고 있는 사람의 <b>실제 키</b>다.
            // 거기에 대고 카메라를 끌어내리면 몸은 서 있는데 눈만 꺼지는 꼴이 되어
            // 곧바로 멀미가 난다. 앉은 채 시작하는 연출은 VR 용으로 따로 지어야 한다.
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
            var cam = Camera.main;
            if (cam == null || cam.GetComponent<EntryRise>() != null) return;
            cam.gameObject.AddComponent<EntryRise>();
        }

        private void Start()
        {
            if (_done) { Destroy(this); return; }
            _done = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (_lookAt == null && !string.IsNullOrEmpty(_lookAtPath))
            {
                var g = GameObject.Find(_lookAtPath);
                if (g != null) _lookAt = g.transform;
            }

            var fly = GetComponent<DebugFlyCamera>();
            if (fly != null) fly.enabled = false;      // 도는 동안 손을 뗀다


            // ① 앉은 키와 선 키를 <b>바닥에서</b> 잡는다.
            //
            // 내력이 둘 있다. 처음엔 시작 자리에서 0.52m 를 더 내렸다가 마루에 드러누운
            // 눈이 되었고, 그것을 고치느라 "씬에 잡아 둔 자리를 그대로 앉은 자리로 쓴다"고
            // 했다. 그런데 씬의 그 자리가 바닥에 가까우면 <b>바닥에서 솟아오르는</b> 것이
            // 되어, 앉았다 일어서는 사람이 아니라 마루를 뚫고 나오는 사람이 된다.
            //
            // 어느 쪽도 씬 값에 기대는 한 어긋난다. 발밑을 찾아 <b>거기서부터</b> 앉은 키
            // 0.92m, 선 키 1.7m 를 잡는다. 그러면 시작 자리를 어디에 두든 앉은 눈은 늘
            // 앉은 눈이고, 일어선 눈은 늘 선 눈이다.
            Vector3 seat = transform.position;
            float standEye = _standEye;
            if (standEye <= 0.01f) standEye = fly != null ? fly.EyeHeight : 1.7f;

            if (_measureFromFloor &&
                Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down,
                                out var floor, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                seat = new Vector3(seat.x, floor.point.y + _seatEye, seat.z);
                transform.position = seat;                       // 앉은 눈높이에서 시작한다
            }
            Vector3 stand = _measureFromFloor
                          ? new Vector3(seat.x, seat.y + Mathf.Max(0.05f, standEye - _seatEye), seat.z)
                          : seat + Vector3.up * _riseHeight;

            // ② 고개를 돌린다
            Quaternion from = transform.rotation;
            Quaternion to = from;
            if (_lookAt != null)
            {
                var rs = _lookAt.GetComponentsInChildren<Renderer>(true);
                Vector3 at = _lookAt.position;
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                    at = b.center;
                }
                Vector3 dir = at - seat;
                if (dir.sqrMagnitude > 0.0001f) to = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            yield return Ease(_turnSeconds, t => transform.rotation = Quaternion.Slerp(from, to, t));

            // ③ 다 보고 잠깐 — 이 사이가 곧 "보았다" 는 뜻이다
            yield return new WaitForSeconds(_holdSeconds);

            // ④ 일어선다. 고개는 조금 따라 든다 — 앉아서 내려다보던 각이 서면 펴진다.
            Quaternion up = Quaternion.Euler(Mathf.Max(-6f, to.eulerAngles.x > 180f ? to.eulerAngles.x - 360f : to.eulerAngles.x) - 4f,
                                             to.eulerAngles.y, 0f);
            yield return Ease(_riseSeconds, t =>
            {
                transform.position = Vector3.Lerp(seat, stand, t);
                transform.rotation = Quaternion.Slerp(to, up, t);
            });

            transform.position = stand;

            if (fly != null)
            {
                fly.enabled = true;
                // 올라선 높이(앉은 자리 + 0.5m)가 곧 선 키는 아니다. 바닥을 찾아
                // 거기서 눈높이만큼 다시 잡는다 — Tab 을 한 번 누른 것과 같은 일이다.
                fly.StandOnGround();
                fly.SyncAngles();     // 지금 보는 쪽을 제 것으로 받아들이게 한다
            }
            Destroy(this);
        }

        /// <summary>0에서 1까지 부드럽게. 시간이 멈춰도 도는 연출이라 unscaled 로 잰다.</summary>
        private static IEnumerator Ease(float seconds, System.Action<float> step)
        {
            if (seconds <= 0f) { step(1f); yield break; }
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                step(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            step(1f);
        }
    }
}
