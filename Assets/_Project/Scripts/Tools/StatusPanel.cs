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

        /// <summary>눈높이에서 이만큼 위(m). 앞을 가리지 않을 만큼만 올린다.</summary>
        private const float Height = 0.42f;

        private struct Line
        {
            public int order;
            public string text;
        }

        private static readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>();
        private static string _shown;

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
            var sorted = new List<KeyValuePair<string, Line>>(_lines);
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
            WorldNotice.Show(Key, joined, Height);
        }
    }
}
