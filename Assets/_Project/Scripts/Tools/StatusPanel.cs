using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>눈 위에 뜨는 상태창 하나</b> — 목표도 소리도 여기 한 판에 모인다.
    ///
    /// <b>왜 모았나</b>: 여태 목표 안내(<c>WorldNotice("목표")</c>)와 소리 눈금
    /// (<c>WorldNotice("소리")</c>)이 <b>따로 떠 있었다</b>. 판이 둘이면 눈이 둘로
    /// 갈리고, 둘 사이가 벌어졌다 붙었다 하니 어느 것이 무엇에 딸린 글인지도 알 수
    /// 없다. 무엇보다 지금 <b>조심해야 하는지 아닌지</b>는 목표와 한 몸으로 읽혀야
    /// 하는 것이다 — "뒤져라"와 "지금 시끄럽다"는 따로 있을 말이 아니다.
    ///
    /// 그래서 줄마다 임자를 주고, 임자들이 각자 제 줄만 적는다. 이 판이 그것을
    /// <b>차례대로 이어 붙여</b> 한 판으로 띄운다.
    ///
    /// <code>
    ///   StatusPanel.Set("목표", 0, "밤이다. 몰래 집 안을 뒤져라\n필수 2/4");
    ///   StatusPanel.Set("소리", 1, "소리 ■■■···\n하인 ■·····");
    ///   StatusPanel.Set("인기척", 2, "<b>밖에서 발소리가 멎었다</b>");
    ///   StatusPanel.Clear("인기척");
    /// </code>
    ///
    /// 순서(order)가 작을수록 위에 온다. 같은 임자가 다시 적으면 그 줄만 바뀐다.
    /// </summary>
    public static class StatusPanel
    {
        /// <summary>이 판이 쓰는 알림판 이름표. 다른 알림과 안 겹치게 한 군데로 모은다.</summary>
        private const string Key = "상태";

        /// <summary>
        /// 눈높이에서 이만큼 위(m).
        ///
        /// <b>화면 위쪽으로 올렸다.</b> 0.42 는 눈높이 바로 위라 사람의 얼굴과 겹치는
        /// 자리였다 — 지나가는 한 마디가 마주 선 사람 위에 겹쳐 뜨면 둘 다 안 읽힌다.
        /// 1.6m 앞에서 화면 세로의 절반은 0.92m 이므로, 0.66 은 <b>위쪽 여백</b>께다.
        /// 방문(榜文)이 붙는 자리가 눈앞이 아니라 문 위인 것과 같다.
        /// </summary>
        private const float Height = 0.66f;

        private struct Line
        {
            public int order;
            public string text;
        }

        private static readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>();
        private static string _shown;

        /// <summary>
        /// <b>종이를 읽는 동안에는 급한 것만 남긴다.</b>
        ///
        /// 문서를 펴 들면 방은 어두워지고 눈은 종이에 붙는다. 그 위에 「소리 ■■■···」
        /// 같은 줄이 계속 떠 있으면 <b>읽을 것과 살필 것이 한 화면에서 다툰다</b> —
        /// 둘 다 제대로 안 읽힌다. 그러니 형편을 알리는 줄은 물러난다.
        ///
        /// 다만 <b>다 물릴 수는 없다.</b> 「하인이 오고 있다」는 알리는 말이 아니라
        /// <b>지금 움직이라는 말</b>이다. 종이를 보느라 그것을 못 보고 붙잡히면
        /// 그건 연출이 아니라 함정이다. 그래서 급한 줄은 읽는 동안에도 남는다.
        /// </summary>
        private static bool _quiet;

        /// <summary>읽는 동안에도 남아야 하는 줄들 — 반응해야 하는 것.</summary>
        private static readonly HashSet<string> Urgent = new HashSet<string> { "인기척", "눕기" };

        /// <summary>종이·수첩을 펴고 접을 때 부른다. 켜면 형편 줄이 물러난다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookScenes()
        {
            // 씬을 갈아 들어가면(Single) 지난 씬의 안내 줄을 걷는다 — 누운 채 출도하면 「Space — 일어난다」가,
            // 붙들리면 「하인과 마주쳤다」가 관아까지 따라왔다(2026-09-15). 덧불러오기(Additive)는 그대로 둔다.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) HideAll();
        }

        public static void Quiet(bool on)
        {
            if (_quiet == on) return;
            _quiet = on;
            Redraw();
        }

        /// <summary>한 줄을 적는다. 같은 임자가 다시 적으면 그 줄만 바뀐다.</summary>
        public static void Set(string owner, int order, string text)
        {
            if (string.IsNullOrEmpty(owner)) return;
            if (string.IsNullOrEmpty(text)) { Clear(owner); return; }

            Line had;
            if (_lines.TryGetValue(owner, out had) && had.order == order && had.text == text) return;
            _lines[owner] = new Line { order = order, text = text };
            Redraw();
        }

        /// <summary>그 임자의 줄을 지운다. 남은 줄이 없으면 판이 통째로 사라진다.</summary>
        public static void Clear(string owner)
        {
            if (string.IsNullOrEmpty(owner) || !_lines.Remove(owner)) return;
            Redraw();
        }

        /// <summary>판을 통째로 내린다(심문·수첩을 볼 때).</summary>
        public static void HideAll()
        {
            if (_lines.Count == 0 && _shown == null) return;
            _lines.Clear();
            _shown = null;
            WorldNotice.Hide(Key);
        }

        private static void Redraw()
        {
            if (_lines.Count == 0)
            {
                _shown = null;
                WorldNotice.Hide(Key);
                return;
            }

            // 줄이 몇 개 안 되므로 그때그때 골라 쓴다 — 목록을 따로 들고 있을 것 없다
            var sorted = new List<KeyValuePair<string, Line>>();
            foreach (var kv in _lines)
                if (!_quiet || Urgent.Contains(kv.Key)) sorted.Add(kv);
            if (sorted.Count == 0) { _shown = null; WorldNotice.Hide(Key); return; }
            sorted.Sort((a, b) => a.Value.order.CompareTo(b.Value.order));

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(sorted[i].Value.text);
            }
            string joined = sb.ToString();
            if (joined == _shown) return;
            _shown = joined;
            // <b>바탕 없이 금물 글씨만.</b> 한지 종잇조각으로 두었더니 그것도 결국
            // 네모라, 「마을 어귀에 닿았다」 한 줄에 판때기가 하나 붙어 나왔다.
            // 창은 닫아야 하는 것이고, 이 판은 읽고 흘려보낼 한 마디다 —
            // 글씨만 떴다 지면 그 말이 세상 위에 잠깐 얹힌 것으로 읽힌다.
            WorldNotice.Show(Key, joined, Height, WorldNotice.Look.Gold);
        }
    }
}
