using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>사람을 붙들면 늘 같은 그림이 된다.</b>
    ///
    /// 여태 말을 거는 자리는 그때그때였다. 두 걸음 앞에서 누르면 얼굴이 화면을 채우고,
    /// 네 걸음 앞에서 누르면 발끝까지 든 채로 대화가 시작됐다. 같은 일인데 <b>매번
    /// 다른 그림</b>이면 그것은 연출이 아니라 우연이다 — 어디에 서서 눌렀는지가
    /// 이야기보다 크게 말하게 된다.
    ///
    /// 그래서 <b>담을 세로 길이</b>를 못 박는다(<see cref="FrameHeight"/>). 상대까지의
    /// 거리는 그때그때 다르지만, 그 길이가 화면에서 차지하는 몫은 늘 같도록 화각을
    /// 되잡으면 <b>어디서 눌러도 상반신</b>이 된다.
    ///
    /// <b>다가서지 않고 당긴다.</b> 카메라를 옮기면 담을 넘고 벽을 뚫고 사람 속으로
    /// 들어간다 — 걸어 들어갈 길이 있는지를 매번 따져야 한다. 화각만 좁히면 그 물음이
    /// 아예 없어지고, 눈을 가늘게 뜨고 들여다보는 결도 그쪽이 맞다.
    ///
    /// 붙들린 사람은 <b>이쪽을 마주 본다</b>. 좌우로만 돌린다 — 위아래까지 돌리면
    /// 사람이 아니라 판때기가 된다.
    ///
    /// 씬에 둘 필요 없다. <see cref="InterrogationController"/> 가 부르면 스스로 선다.
    /// </summary>
    public class TalkFraming : MonoBehaviour
    {
        /// <summary>화면에 담을 몸의 세로(m). 허리께부터 갓 위까지가 이만하다.</summary>
        private const float FrameHeight = 1.15f;

        /// <summary>그것이 화면 세로에서 차지할 몫. 1이면 꽉 찬다.</summary>
        private const float Fill = 0.70f;

        /// <summary>
        /// 겨누는 높이. 0=발, 1=정수리.
        ///
        /// 몸 한가운데가 아니라 <b>얼굴께</b>다. 화면 아래 3분의 1은 묻는 창이 덮으므로,
        /// 한가운데를 겨누면 창이 가슴까지 먹고 <b>턱만 남는다</b>. 얼굴을 위로 올려
        /// 두면 창 위에 남는 자리가 곧 어깨와 얼굴이 된다.
        /// </summary>
        private const float AimAt = 0.82f;

        private const float TurnSeconds = 0.42f;   // 고개가 돌아가는 데 걸리는 시간
        private const float FaceSeconds = 0.55f;   // 상대가 이쪽으로 도는 데 걸리는 시간

        private static TalkFraming _instance;
        private static TalkFraming Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("_심문구도");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TalkFraming>();
            return _instance;
        }

        /// <summary>정적 값은 도메인 리로드가 꺼진 판에서 재생을 넘겨 산다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _instance = null; }

        private Coroutine _running;
        private Camera _cam;
        private float _restFov = -1f;

        /// <summary>붙들었다 — 상반신 구도로 잡고, 상대를 이쪽으로 돌린다.</summary>
        public static void Enter(Transform person)
        {
            if (person == null) return;
            var f = Ensure();
            if (f._running != null) f.StopCoroutine(f._running);
            f._running = f.StartCoroutine(f.EnterRoutine(person));
        }

        /// <summary>놓아준다 — 화각만 되돌린다. 시선은 그대로 둔다(보던 사람을 보고 있는 것이 맞다).</summary>
        public static void Leave()
        {
            var f = _instance;
            if (f == null) return;
            if (f._running != null) { f.StopCoroutine(f._running); f._running = null; }
            if (f._cam != null && f._restFov > 0f) f._running = f.StartCoroutine(f.LeaveRoutine());
        }

        private IEnumerator EnterRoutine(Transform person)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            // 앞선 심문이 화각을 되돌리기 전에 다음 사람을 붙들 수 있다.
            // 그때 지금 값을 「쉼 화각」으로 적어 두면 좁힌 값이 굳는다.
            if (_cam != cam || _restFov <= 0f) _restFov = cam.fieldOfView;
            _cam = cam;

            // 상대가 도는 것은 <b>같이</b> 시작한다. 다 돌기를 기다렸다가 카메라를 옮기면
            // 그 사이가 빈 화면이 된다.
            //
            // 다만 <b>돌리는 자가 이미 있으면 손대지 않는다.</b> FaceThePlayer 는 심문이
            // 열려 있는 동안 LateUpdate 에서 매 프레임 이쪽을 보게 돌리는데, 이 코루틴은
            // Update 켜에서 돈다 — 나중에 도는 쪽이 이기므로 여기서 공들여 눕힌 0.55초가
            // 그대로 덮인다. 게다가 그쪽은 「돌릴 몸」이 안 지정되면 <b>뿌리</b>를 돌리고
            // 이쪽은 <b>Animator 가 붙은 자식</b>을 돌려, 둘이 겹치면 두 겹으로 돌아간다.
            // 한 사람이 맡는 편이 낫고, 내내 마주 보아야 하니 그쪽이 맡는 것이 맞다.
            Transform body = FindMover(person);
            if (person.GetComponentInChildren<FaceThePlayer>(true) == null)
                StartCoroutine(FaceRoutine(body, cam.transform));

            Quaternion fromRot = cam.transform.rotation;
            float fromFov = cam.fieldOfView;

            for (float t = 0f; t < TurnSeconds; t += Time.deltaTime)
            {
                float k = Smooth(t / TurnSeconds);
                Aim(cam, person, fromRot, fromFov, k);
                yield return null;
            }
            Aim(cam, person, fromRot, fromFov, 1f);
            _running = null;
        }

        /// <summary>
        /// <b>매 프레임 다시 잰다.</b> 도는 동안에도 상대와의 거리는 바뀐다(내가 밀리거나
        /// 상대가 돌면서 몸의 겉이 움직인다). 처음에 한 번만 재어 두면 도착했을 때
        /// 구도가 어긋나 있다.
        /// </summary>
        private void Aim(Camera cam, Transform person, Quaternion fromRot, float fromFov, float k)
        {
            Vector3 aim = ModelBounds.EyePoint(person, AimAt);
            Vector3 to = aim - cam.transform.position;
            if (to.sqrMagnitude < 1e-4f) return;

            cam.transform.rotation = Quaternion.Slerp(fromRot, Quaternion.LookRotation(to), k);

            float dist = to.magnitude;
            float want = 2f * Mathf.Atan2(FrameHeight * 0.5f, Mathf.Max(0.2f, dist) * Fill) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Lerp(fromFov, Mathf.Clamp(want, 16f, 70f), k);
        }

        private IEnumerator LeaveRoutine()
        {
            var cam = _cam;
            float from = cam.fieldOfView, to = _restFov;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (cam == null) yield break;
                cam.fieldOfView = Mathf.Lerp(from, to, Smooth(t / 0.5f));
                yield return null;
            }
            if (cam != null) cam.fieldOfView = to;
            _restFov = -1f;
            _running = null;
        }

        /// <summary>
        /// 좌우로만 돌려 이쪽을 보게 한다.
        ///
        /// 돌릴 것은 피벗이 아니라 <b>Animator 가 붙은 몸</b>이다. 마름처럼 껍데기
        /// 오브젝트 밑에 움직이는 몸이 따로 들어 있는 경우, 껍데기를 돌리면 몸은
        /// 그대로 서 있고 애먼 좌표계만 돌아간다.
        /// </summary>
        private static Transform FindMover(Transform root)
        {
            var anim = root.GetComponentInChildren<Animator>(true);
            return anim != null ? anim.transform : root;
        }

        private IEnumerator FaceRoutine(Transform body, Transform eye)
        {
            if (body == null || eye == null) yield break;

            Vector3 flat = eye.position - body.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f) yield break;

            Quaternion from = body.rotation;
            Quaternion to = Quaternion.LookRotation(flat.normalized, Vector3.up);
            for (float t = 0f; t < FaceSeconds; t += Time.deltaTime)
            {
                if (body == null) yield break;
                body.rotation = Quaternion.Slerp(from, to, Smooth(t / FaceSeconds));
                yield return null;
            }
            if (body != null) body.rotation = to;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
