using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 플레이어의 시선을 한 곳으로 끌고 가서, 돋보기로 들여다보듯 좁혀 보여주고 놓아준다.
    ///
    /// 쓰는 자리: 인물이 흘린 말("이 삼복더위에 아궁이는 왜 때누")을 플레이어가 놓쳤을 때,
    /// 그 말이 가리키는 것을 한 번 못 박아주는 용도.
    ///
    /// <b>반드시 실제로 보이는 것만 대상으로 삼는다.</b> 벽 너머의 물건을 비추면
    /// "카메라가 벽을 뚫고 날아가는" 것이 되어 공간 감각이 무너진다. 여기서 하는 일은
    /// 시야를 옮기는 것이지 없던 시야를 만들어내는 것이 아니다.
    /// 대상이 정말 보이는지는 씬뷰의 노란 선(OnDrawGizmosSelected)으로 눈으로 확인할 것.
    ///
    /// 붙이는 법:
    ///   1) 빈 GameObject에 이 컴포넌트 추가
    ///   2) _target 에 쳐다볼 것 연결(연기 기둥의 잘 보이는 높이쯤)
    ///   3) 발동시킬 쪽에서 Play() 호출 — TeleportZone 의 _onTeleported 에 물리면 된다
    ///   4) (선택) _requireClueKey 에 단서 key → 그 단서를 이미 얻었을 때만 발동
    ///
    /// 좌우(yaw)만 돌려 대상을 정면에 놓고, 상하는 플레이어에게 맡긴다 —
    /// 위아래까지 뺏으면 시선을 빼앗긴 느낌이 너무 세다.
    /// </summary>
    public class ForcedGaze : MonoBehaviour
    {
        [Header("무엇을 보여줄까")]
        [Tooltip("쳐다볼 대상. 지붕 위로 올라온 연기처럼, 그 자리에서 실제로 보이는 것이어야 한다")]
        [SerializeField] private Transform _target;

        [Header("발동 조건")]
        [Tooltip("이 단서를 이미 얻었을 때만 발동. 비우면 조건 없음 (예: 乙 심문으로 얻는 J01)")]
        [SerializeField] private string _requireClueKey = "";
        [Tooltip("위 단서가 속한 사건")]
        [SerializeField] private CaseId _requireClueCase = CaseId.Case1_Onggojip;
        [Tooltip("한 번만 발동")]
        [SerializeField] private bool _once = true;
        [Tooltip("발동을 이만큼 미룬다. 순간이동 직후라면 화면이 밝아질 틈을 준다")]
        [SerializeField] private float _delay = 0.6f;

        [Header("연출")]
        [Tooltip("시선이 대상으로 돌아가는 데 걸리는 시간")]
        [SerializeField] private float _turnSeconds = 1.2f;
        [Tooltip("좁혀서 물고 있는 시간")]
        [SerializeField] private float _holdSeconds = 1.8f;
        [Tooltip("화각을 되돌리는 시간")]
        [SerializeField] private float _releaseSeconds = 0.8f;
        [Tooltip("좁힐 화각(도). 작을수록 크게 당겨 보인다. 데스크탑 전용")]
        [SerializeField] private float _zoomFov = 26f;

        [Header("한 번 진하게 보여주고 거두기")]
        [Tooltip("연출 동안 확 진해질 것(연기 기둥). 발동하는 순간부터 뿜는 양을 올려 " +
                 "카메라가 도착했을 때 이미 두껍게 서 있게 만든다")]
        [SerializeField] private ParticleSystem[] _thickenWhilePlaying;
        [Tooltip("뿜는 양을 몇 배로 올릴지")]
        [SerializeField] private float _thickenBy = 4f;
        [Tooltip("연출이 끝나면 꺼버릴 것. 한 번 못 박아 보여줬으면 그만이다 — " +
                 "다시 말을 걸었을 때 같은 연기가 또 떠 있으면 '아까 그거'가 되어 무게가 빠진다")]
        [SerializeField] private GameObject[] _hideWhenDone;
        [Tooltip("연출이 끝나고 이만큼 뒤에 거둔다(사라지는 걸 보게 두지 않으려면 조금 길게)")]
        [SerializeField] private float _hideDelay = 1.2f;

        [Header("보고 나서 시선 되돌리기 (비우면 그 자리를 계속 본다)")]
        [Tooltip("다 보고 나면 이쪽으로 고개를 되돌린다. 말을 꺼낸 사람을 연결한다 — " +
                 "그가 일러 준 것을 보고 나서 그를 등진 채 끝나면 대화가 끊긴 것처럼 보인다")]
        [SerializeField] private Transform _lookBackAt;
        [Tooltip("되돌아보는 데 걸리는 시간(초)")]
        [SerializeField] private float _lookBackSeconds = 0.9f;
        [Tooltip("되돌아보기 전에 그 자리를 더 보는 시간(초)")]
        [SerializeField] private float _lookBackDelay = 0.4f;

        [Header("연출 중 잠글 것")]
        [Tooltip("연출 동안 꺼둘 컴포넌트(시점 조작 등). 비우면 카메라의 DebugFlyCamera 를 자동으로 찾는다")]
        [SerializeField] private Behaviour[] _disableWhilePlaying;

        [Tooltip("연출이 끝난 뒤 실행할 것(자막 띄우기 등)")]
        [SerializeField] private UnityEvent _onFinished;

        private bool _played;
        private bool _playing;

        /// <summary>발동. UnityEvent(TeleportZone 의 _onTeleported 등)에서 부른다.</summary>
        public void Play()
        {
            if (_playing) return;
            if (_once && _played) return;
            if (_target == null)
            {
                Debug.LogWarning($"[{name}] 쳐다볼 대상(_target)이 비어 있어 연출을 건너뜁니다.", this);
                return;
            }
            if (!string.IsNullOrEmpty(_requireClueKey) &&
                (Journal.Instance == null || !Journal.Instance.HasClue(_requireClueCase, _requireClueKey)))
                return;

            _played = true;
            StartCoroutine(PlayRoutine());
        }

        /// <summary>_once 를 무시하고 다시 볼 수 있게 되돌린다(테스트용).</summary>
        public void ResetOnce() => _played = false;

        private IEnumerator PlayRoutine()
        {
            _playing = true;

            // 미루는 동안 이미 두꺼워지기 시작해야 한다. 카메라가 돌아본 뒤에 진해지면
            // "지금 막 불을 지폈다"로 읽혀 버린다 — 원래부터 저러고 있었어야 한다.
            Thicken(_thickenBy);

            if (_delay > 0f) yield return new WaitForSeconds(_delay);

            var cam = Camera.main;
            if (cam == null) { _playing = false; yield break; }

            // 심문창이 떠 있는 동안은 시야를 뺏지 않는다 — 읽던 대사가 날아간다.
            while (InterrogationController.AnyOpen) yield return null;

            var locked = ResolveLocks(cam);
            foreach (var b in locked) if (b != null) b.enabled = false;

            float startFov = cam.fieldOfView;
            Quaternion startRot = cam.transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(_target.position - cam.transform.position);

            // 돌리면서 동시에 좁힌다 — 고개가 돌아가는 사이에 이미 당겨지기 시작해야 "들여다보는" 느낌이 난다.
            for (float t = 0f; t < _turnSeconds; t += Time.deltaTime)
            {
                float k = Smooth(t / _turnSeconds);
                cam.transform.rotation = Quaternion.Slerp(startRot, targetRot, k);
                cam.fieldOfView = Mathf.Lerp(startFov, _zoomFov, k);
                yield return null;
            }
            cam.transform.rotation = targetRot;
            cam.fieldOfView = _zoomFov;

            yield return new WaitForSeconds(_holdSeconds);

            // 화각만 되돌린다. 시선은 그대로 둔다 — 플레이어는 이제 그것을 보고 있는 것이 맞다.
            float held = cam.fieldOfView;
            for (float t = 0f; t < _releaseSeconds; t += Time.deltaTime)
            {
                cam.fieldOfView = Mathf.Lerp(held, startFov, Smooth(t / _releaseSeconds));
                yield return null;
            }
            cam.fieldOfView = startFov;

            // 일러 준 사람에게 고개를 되돌린다.
            //
            // 여기 오기까지 심문은 이미 끝난 상태다 — 이 연출은 헤어지며 던진 한 마디를
            // 듣고 나서 벌어진다. 그러니 되돌아본다고 다시 말이 걸리지는 않는다.
            // 다만 그를 등지고 끝나면 "듣다 말았다"로 보이므로, 눈만 돌려 준다.
            if (_lookBackAt != null)
            {
                if (_lookBackDelay > 0f) yield return new WaitForSeconds(_lookBackDelay);

                Quaternion from = cam.transform.rotation;
                Quaternion back = Quaternion.LookRotation(_lookBackAt.position - cam.transform.position);
                for (float t = 0f; t < _lookBackSeconds; t += Time.deltaTime)
                {
                    float k = Smooth(t / _lookBackSeconds);
                    cam.transform.rotation = Quaternion.Slerp(from, back, k);
                    yield return null;
                }
                cam.transform.rotation = back;
            }

            foreach (var b in locked) if (b != null) b.enabled = true;

            _playing = false;
            _onFinished?.Invoke();

            // 보여줄 만큼 보여줬으면 거둔다. 뿜는 것을 먼저 끊고, 떠 있던 것이 흩어질 참을 준 뒤 끈다.
            if (_hideWhenDone != null && _hideWhenDone.Length > 0)
            {
                Thicken(0f);
                yield return new WaitForSeconds(Mathf.Max(0f, _hideDelay));
                foreach (var g in _hideWhenDone) if (g != null) g.SetActive(false);
            }
        }

        /// <summary>연기 뿜는 양을 배수로 조절한다(0이면 그친다). 원래 값은 처음 한 번만 기억한다.</summary>
        private void Thicken(float multiplier)
        {
            if (_thickenWhilePlaying == null) return;
            if (_baseRates == null)
            {
                _baseRates = new float[_thickenWhilePlaying.Length];
                for (int i = 0; i < _thickenWhilePlaying.Length; i++)
                    _baseRates[i] = _thickenWhilePlaying[i] == null
                        ? 0f : _thickenWhilePlaying[i].emission.rateOverTimeMultiplier;
            }
            for (int i = 0; i < _thickenWhilePlaying.Length; i++)
            {
                var ps = _thickenWhilePlaying[i];
                if (ps == null) continue;
                var em = ps.emission;
                em.rateOverTimeMultiplier = _baseRates[i] * multiplier;
            }
        }
        private float[] _baseRates;


        /// <summary>연출 동안 꺼둘 것들. 지정이 없으면 카메라의 시점 조작만 끈다.</summary>
        private Behaviour[] ResolveLocks(Camera cam)
        {
            if (_disableWhilePlaying != null && _disableWhilePlaying.Length > 0) return _disableWhilePlaying;
            var fly = cam.GetComponent<DebugFlyCamera>();
            return fly != null ? new Behaviour[] { fly } : new Behaviour[0];
        }

        /// <summary>0→1 을 부드럽게. 시선이 툭 끊기지 않게 시작과 끝을 눕힌다.</summary>
        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 씬뷰에서 대상까지 선을 그린다. 이 선이 벽을 지나가면 그 자리에서는 쓰면 안 되는 연출이다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_target == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            Gizmos.DrawLine(transform.position, _target.position);
            Gizmos.DrawWireSphere(_target.position, 0.4f);
        }
    }
}
