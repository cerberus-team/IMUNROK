using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 씬에 깔리는 소리 하나 — 배경 음악이든 풀벌레든.
    ///
    /// 왜 그냥 AudioSource 를 쓰지 않는가: 소리를 뚝 켜고 뚝 끄면 그 순간이 다 들린다.
    /// 특히 씬이 바뀔 때 노래가 잘리면 실수처럼 들린다. 그래서 들고 날 때 여며 준다.
    /// 그리고 노래는 통째로 메모리에 올리면 몇 MB가 그대로 잡히므로 흘려 읽게 한다.
    ///
    /// 붙이는 법: 빈 오브젝트에 붙이고 _clip 에 음원을 넣으면 끝이다.
    /// 같은 씬에 여러 개 둬도 된다 — 음악 하나, 풀벌레 하나 하는 식으로.
    ///
    /// 소리는 <b>사방에서 나는 것(2D)</b>으로 둔다. 배경 음악과 풀벌레는 어느 한 자리에서
    /// 나는 소리가 아니라 그 자리에 깔린 소리라, 고개를 돌릴 때마다 크기가 바뀌면 안 된다.
    /// 특정한 자리에서 나야 하는 소리(등잔 타는 소리 같은)는 _spatial 을 올려 쓴다.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SceneMusic : MonoBehaviour
    {
        [Header("무엇을")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("다 끝나면 처음부터 다시. 배경 음악·풀벌레는 켜 둔다")]
        [SerializeField] private bool _loop = true;

        [Header("얼마나")]
        [Range(0f, 1f)]
        [Tooltip("제 크기. 배경 음악은 0.3~0.5 쯤이 말소리를 안 덮는다")]
        [SerializeField] private float _volume = 0.42f;

        [Tooltip("이만큼에 걸쳐 스며든다(초). 0이면 그냥 켜진다")]
        [SerializeField] private float _fadeIn = 2.5f;

        [Tooltip("Stop() 했을 때 잦아드는 시간(초)")]
        [SerializeField] private float _fadeOut = 1.5f;

        [Header("어디서")]
        [Range(0f, 1f)]
        [Tooltip("0이면 사방에서(배경), 1이면 이 자리에서 난다(등잔·물소리 같은 것)")]
        [SerializeField] private float _spatial = 0f;

        [Tooltip("자리에서 나는 소리일 때 들리는 거리(m)")]
        [SerializeField] private float _range = 12f;

        [Header("언제")]
        [SerializeField] private bool _playOnStart = true;

        [Tooltip("씬이 바뀌어도 계속 흐르게 한다. 표제·어명처럼 씬을 넘어 이어지는 곡에만 켠다")]
        [SerializeField] private bool _keepAcrossScenes = false;

        [Header("잠행")]
        [Tooltip("<b>방을 뒤지는 동안</b>(甲 이 나가고 소리계가 켜진 뒤) 잦아든다. 노래가 흐르는 채로는 " +
                 "「조용히 해야 한다」가 몸에 안 온다. 풀벌레 같은 밤소리는 꺼 둔다 — 그것은 남아야 " +
                 "고요가 들린다")]
        [SerializeField] private bool _hushWhileSneaking = true;
        [Range(0f, 1f)]
        [Tooltip("잠행 동안 남기는 크기(제 크기에 곱한다). 0 이면 완전히 멎는다")]
        [SerializeField] private float _sneakLevel = 0f;

        private AudioSource _src;
        private Coroutine _fading;
        private bool _hushed;

        private static readonly System.Collections.Generic.List<SceneMusic> _all =
            new System.Collections.Generic.List<SceneMusic>();

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() { _all.Remove(this); }

        /// <summary>지금 이 소리의 「제 크기」 — 잠행 중이면 낮춘 크기.</summary>
        private float Base => _hushed ? _volume * _sneakLevel : _volume;

        /// <summary>
        /// <b>잠행이 시작되고 끝날 때.</b> 노래가 천천히 잦아들어 방이 고요해진다 —
        /// 그 고요가 「소리 내지 마라」는 말보다 먼저 온다.
        /// </summary>
        public static void Sneak(bool on, float seconds)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var m = _all[i];
                if (m == null || !m._hushWhileSneaking || m._hushed == on) continue;
                m._hushed = on;
                if (m._src != null && m._src.isPlaying) m.FadeTo(m.Base, seconds);
            }
        }

        /// <summary>씬의 소리를 한꺼번에 낮춘다(사람이 다가올 때). 잠행으로 낮춘 크기에서 더 낮춘다.</summary>
        public static void DuckAll(float toward, float seconds)
        {
            for (int i = 0; i < _all.Count; i++) if (_all[i] != null) _all[i].Duck(toward, seconds);
        }

        /// <summary>한꺼번에 제 크기로 — 잠행 중이면 <b>잠행 크기로</b> 돌아간다(노래가 되살아나지 않는다).</summary>
        public static void UnduckAll(float seconds)
        {
            for (int i = 0; i < _all.Count; i++) if (_all[i] != null) _all[i].Unduck(seconds);
        }

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.clip = _clip;
            _src.loop = _loop;
            _src.playOnAwake = false;
            _src.spatialBlend = _spatial;
            _src.maxDistance = _range;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.volume = 0f;

            if (_keepAcrossScenes)
            {
                // 씬을 넘겨 살리려면 뿌리여야 한다. 자식인 채로 두면 부모째 사라진다.
                if (transform.parent != null) transform.SetParent(null, true);
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (_playOnStart) Play();
        }

        /// <summary>튼다. 이미 흐르고 있으면 크기만 제자리로 되돌린다.</summary>
        public void Play()
        {
            if (_clip == null)
            {
                Debug.LogWarning($"[{name}] 음원이 비어 있어 아무 소리도 안 납니다.", this);
                return;
            }
            if (!_src.isPlaying) _src.Play();
            FadeTo(Base, _fadeIn);
        }

        /// <summary>잦아들다 멎는다.</summary>
        public void Stop() => FadeTo(0f, _fadeOut, true);

        /// <summary>말소리가 깔릴 동안 잠시 낮춘다(심문·자막 때).</summary>
        public void Duck(float toward = 0.35f, float seconds = 0.6f)
            => FadeTo(Base * Mathf.Clamp01(toward), seconds);

        /// <summary>낮췄던 것을 되돌린다.</summary>
        public void Unduck(float seconds = 0.8f) => FadeTo(Base, seconds);

        /// <summary>음원을 바꿔 끼우고 다시 튼다(챕터마다 다른 곡을 쓸 때).</summary>
        public void SetClip(AudioClip clip)
        {
            _clip = clip;
            if (_src == null) return;
            bool was = _src.isPlaying;
            _src.Stop();
            _src.clip = clip;
            if (was) Play();
        }

        private void FadeTo(float target, float seconds, bool stopAtEnd = false)
        {
            if (_fading != null) StopCoroutine(_fading);
            _fading = StartCoroutine(FadeRoutine(target, seconds, stopAtEnd));
        }

        private IEnumerator FadeRoutine(float target, float seconds, bool stopAtEnd)
        {
            float from = _src.volume;
            float dur = Mathf.Max(0.01f, seconds);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;   // 멈춰 세운 동안에도 소리는 여며야 한다
                _src.volume = Mathf.Lerp(from, target, Mathf.Clamp01(t));
                yield return null;
            }
            _src.volume = target;
            if (stopAtEnd) _src.Stop();
            _fading = null;
        }
    }
}
