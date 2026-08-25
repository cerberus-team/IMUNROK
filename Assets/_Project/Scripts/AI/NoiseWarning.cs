using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>문밖의 인기척</b> — 방을 뒤지는 소리를 듣고 말로 알려 주는 귀.
    ///
    /// 소리계(<see cref="NoiseMeter"/>)와 귀(<see cref="NoiseListener"/>)는 다 만들어져
    /// 있었는데, 정작 <b>귀가 붙은 사람이 전부 마당에</b> 있었다. 방에 들어서면 마당 씬이
    /// 통째로 내려가므로, 서랍을 부수든 뛰어다니든 그 소리를 들을 사람이 <b>씬에 없었다</b>.
    /// 들킬 일이 없으면 뒤지는 것이 아니라 구경하는 것이다.
    ///
    /// 그렇다고 방 안에 사람을 세워 둘 수는 없다 — 조사는 <b>빈 방</b>에서 하는 일이다.
    /// 그래서 문밖에 <b>보이지 않는 귀</b> 하나를 둔다. 툇마루를 지나다니는 하인이라 치면
    /// 된다. 이 귀는 다가오지 않는다. 대신 <b>발소리가 멎는다</b> — 그것으로 족하다.
    ///
    /// <b>왜 말로 알리나</b>: 눈금만 올라가는 규칙은 아무도 못 배운다. 처음 걸렸을 때
    /// "밖에서 발소리가 멎었다"는 한 마디가 있어야, 그 다음부터 서랍을 살살 연다.
    /// 벌은 그 뒤의 일이다.
    ///
    /// 붙이는 곳: 문 근처의 빈 오브젝트. <see cref="NoiseListener"/> 와 함께 둔다.
    /// </summary>
    [RequireComponent(typeof(NoiseListener))]
    public class NoiseWarning : MonoBehaviour
    {
        [Header("누가 듣나")]
        [Tooltip("자막에 뜨는 이름. 비우면 이름 없이 상황만 적힌다")]
        [SerializeField] private string _speaker = "";

        [Header("돌아볼 만큼 들렸을 때")]
        [TextArea(2, 3)]
        [Tooltip("한 마디. *별표*로 감싼 낱말은 도드라진다")]
        [SerializeField] private string _noticedLine = "밖에서 *발소리가 멎었다*.";

        [Header("다가올 만큼 들렸을 때")]
        [TextArea(2, 3)]
        [SerializeField] private string _alarmedLine = "마루가 삐걱인다 — *누가 이쪽으로 온다*.";

        [Tooltip("이만큼(초) 안에는 같은 말을 다시 하지 않는다")]
        [SerializeField] private float _quiet = 8f;

        [Header("몇 번까지 봐주나")]
        [Tooltip("크게 들킨 것이 이만큼 쌓이면 <b>들킴</b>으로 친다. 0이면 세지 않는다 — " +
                 "벌을 어떻게 줄지는 사건 쪽이 정한다")]
        [SerializeField] private int _caughtAt = 3;

        [Tooltip("다 쌓였을 때 한 번. 사건 흐름이 받아 처리한다(쫓겨나기·다시 하기 등)")]
        [SerializeField] private UnityEvent _onCaught;

        [Header("사람을 부른다")]
        [Tooltip("크게 들리면 <b>늙은 하인이 살피러 온다</b>(ServantCheck). " +
                 "이 귀는 방 안에 있고 하인은 마당에 서 있어 씬이 달라, 인스펙터가 아니라 " +
                 "이름 없이 부른다. 하인이 없는 씬이면 그냥 말만 뜬다")]
        [SerializeField] private bool _summonServant = true;

        [Header("이어지는 소리로 판단한다")]
        [Tooltip("켜면 <b>봉우리 하나</b>가 아니라 <b>얼마나 오래</b> 시끄러웠나로 판단한다" +
                 "(NoiseMeter 의 달아오름). 서랍을 한 번 확 빼는 것보다 " +
                 "부스럭거림이 <b>이어지는 것</b>이 사람을 부른다")]
        [SerializeField] private bool _useSustained = true;
        [Tooltip("이만큼 달아오르면 <b>한 번</b> 일러 준다 — '밖에서 발소리가 멎었다'")]
        [Range(0.2f, 0.95f)] [SerializeField] private float _warnAt = 0.55f;

        /// <summary>지금까지 크게 들킨 횟수.</summary>
        public int Strikes { get; private set; }

        private float _silentUntil;
        private bool _fired;
        private bool _warned;      // 이번 달아오름에서 이미 일렀나

        /// <summary>
        /// <b>이어지는 소리</b>를 지켜본다.
        ///
        /// 여태 자막이 계속 뜬 까닭이 여기 있었다 — 문을 여닫을 때마다, 서랍을 뺄 때마다
        /// 소리 <b>하나하나</b>가 귀에 걸려 그때마다 "밖에서 발소리가 멎었다"가 떴다.
        /// 열 번 뜨면 그건 경고가 아니라 <b>글자</b>다.
        /// 이제 한 번 달아오르는 동안 <b>한 번만</b> 이르고, 다 달아오르면 사람이 온다.
        /// </summary>
        private void Update()
        {
            if (!_useSustained) return;
            float heat = NoiseMeter.Heat;

            if (heat <= 0.05f) { _warned = false; return; }

            if (!_warned && heat >= _warnAt)
            {
                _warned = true;
                Say(_noticedLine);
                if (_summonServant) ServantCheck.Glance(transform.position);
            }

            if (heat >= 0.999f && !ServantCheck.Watching)
            {
                NoiseMeter.CoolDown();     // 한 번 왔으면 처음부터 다시 센다
                _warned = false;
                Strikes++;
                if (_summonServant) ServantCheck.Summon(transform.position);
                else Say(_alarmedLine);
                if (_caughtAt > 0 && !_fired && Strikes >= _caughtAt) { _fired = true; _onCaught?.Invoke(); }
            }
        }

        /// <summary>돌아볼 만큼 들렸다. 귀의 이벤트에 물린다.</summary>
        public void OnNoticed(Vector3 at)
        {
            if (_useSustained) return;      // 이어지는 소리로 판단하는 동안에는 낱소리에 안 군다
            Say(_noticedLine);
            if (_summonServant) ServantCheck.Glance(at);
        }

        /// <summary>다가올 만큼 들렸다. 귀의 이벤트에 물린다.</summary>
        public void OnAlarmed(Vector3 at)
        {
            if (_useSustained) return;
            // 하인이 오면 그가 말한다. 두 목소리가 겹치지 않게 이쪽은 다문다.
            if (_summonServant && !ServantCheck.Watching) ServantCheck.Summon(at);
            else Say(_alarmedLine);
            if (_caughtAt <= 0 || _fired) return;
            Strikes++;
            if (Strikes < _caughtAt) return;
            _fired = true;
            _onCaught?.Invoke();
        }

        /// <summary>
        /// 알린다 — <b>자막 한 줄로는 못 알아본다</b>.
        ///
        /// 여태 이 경고는 여느 말과 똑같은 자막으로 떴다. 그런데 그 자리에는 물건을
        /// 살펴본 말도 뜨고 하인이 하는 말도 뜬다. 다 지나가는 글이라, 그 가운데
        /// <b>어느 것이 위험을 알리는 말인지</b> 알 수가 없었다.
        ///
        /// 그래서 자막에 더해 <b>상태창에 한 줄을 박아 둔다</b>. 소리 눈금 바로 밑이라
        /// 눈금이 왜 찼는지가 한 자리에서 읽힌다. 위험이 지나면 저절로 사라진다.
        /// </summary>
        private void Say(string line)
        {
            if (string.IsNullOrEmpty(line) || Time.time < _silentUntil) return;
            _silentUntil = Time.time + _quiet;
            SubtitleView.Show(_speaker, line, "");
            StatusPanel.Set("인기척", 2, "▶ " + line);
            _noticeUntil = Time.time + 6f;
        }

        private float _noticeUntil;

        /// <summary>박아 둔 경고를 거둔다. 위험이 지났는데 글이 남아 있으면 그것도 거짓말이다.</summary>
        private void LateUpdate()
        {
            if (_noticeUntil <= 0f) return;
            if (Time.time < _noticeUntil && NoiseMeter.Heat > 0.15f) return;
            _noticeUntil = 0f;
            StatusPanel.Clear("인기척");
        }
    }
}
