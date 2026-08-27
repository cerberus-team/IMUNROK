// TMP 글자 인덱스 구간으로 어절 클릭 영역을 만드는 공용 판정기입니다. WordPickUI 와 대화 로그가 함께 씁니다.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>한 어절의 클릭 영역. 줄바꿈으로 갈리면 rects 가 여러 개가 됩니다.</summary>
    public sealed class WordRegion
    {
        public WordPickNote.WordOption option;
        public int optionIndex = -1;
        public bool consumed;                                   // 이미 지목한 어절
        public readonly List<Rect> rects = new List<Rect>();
        public float area;
    }

    /// <summary>
    /// ★어절 판정의 단일 구현.
    ///
    /// TMP 의 wordInfo(공백 단위 낱말)를 쓰지 않고 ★characterInfo 의 원문 인덱스 구간을 씁니다.
    /// "6개월 전부터요." 처럼 어절이 낱말 경계와 어긋나는 경우를 낱말 단위로는 잡을 수 없기 때문입니다.
    ///
    /// 상자 높이는 글자 모양이 아니라 ★줄의 ascender/descender 로 잡습니다.
    /// 어절마다 상자 크기가 달라져 정답이 드러나는 것을 막기 위해서입니다.
    /// </summary>
    public static class WordRegionBuilder
    {
        /// <summary>
        /// 문장 하나의 어절 판정 영역을 만듭니다. 호출 전에 text.ForceMeshUpdate() 가 끝나 있어야 합니다.
        /// 결과는 좁은 영역이 앞에 오도록 정렬됩니다(포함 관계에서 큰 쪽이 작은 쪽을 삼키지 않도록).
        /// </summary>
        public static void Build(TMP_Text label, WordPickNote.Sentence sentence,
                                 float padX, float padY, List<WordRegion> results)
        {
            if (results == null) return;
            results.Clear();

            if (label == null || sentence == null || string.IsNullOrEmpty(sentence.text)) return;
            if (sentence.options == null || sentence.options.Length == 0) return;

            TMP_TextInfo info = label.textInfo;
            if (info == null || info.characterCount == 0) return;

            string source = sentence.text;

            for (int oi = 0; oi < sentence.options.Length; oi++)
            {
                WordPickNote.WordOption option = sentence.options[oi];
                if (option == null || string.IsNullOrEmpty(option.word)) continue;

                int start = source.IndexOf(option.word, StringComparison.Ordinal);
                if (start < 0) continue;                        // ★문장에 없는 어절은 무시
                int end = start + option.word.Length;

                WordRegion region = new WordRegion();
                region.option = option;
                region.optionIndex = oi;

                int line = -1;
                float minX = 0f, maxX = 0f;
                bool has = false;

                for (int c = 0; c < info.characterCount; c++)
                {
                    TMP_CharacterInfo ci = info.characterInfo[c];
                    if (ci.index < start || ci.index >= end) continue;
                    if (!ci.isVisible) continue;

                    if (!has || ci.lineNumber != line)
                    {
                        if (has) region.rects.Add(MakeRect(info, line, minX, maxX, padX, padY));
                        line = ci.lineNumber;
                        minX = ci.bottomLeft.x;
                        maxX = ci.topRight.x;
                        has = true;
                    }
                    else
                    {
                        if (ci.bottomLeft.x < minX) minX = ci.bottomLeft.x;
                        if (ci.topRight.x > maxX) maxX = ci.topRight.x;
                    }
                }
                if (has) region.rects.Add(MakeRect(info, line, minX, maxX, padX, padY));
                if (region.rects.Count == 0) continue;

                region.area = 0f;
                for (int r = 0; r < region.rects.Count; r++)
                    region.area += region.rects[r].width * region.rects[r].height;

                results.Add(region);
            }

            results.Sort(CompareByArea);
        }

        /// <summary>로컬 좌표가 어느 영역 안인지. 없으면 -1. 이미 지목한 영역은 건너뜁니다.</summary>
        public static int Find(List<WordRegion> regions, Vector2 localPoint)
        {
            if (regions == null) return -1;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].consumed) continue;
                List<Rect> rects = regions[i].rects;
                for (int r = 0; r < rects.Count; r++)
                    if (rects[r].Contains(localPoint)) return i;
            }
            return -1;
        }

        private static int CompareByArea(WordRegion a, WordRegion b)
        {
            return a.area.CompareTo(b.area);
        }

        private static Rect MakeRect(TMP_TextInfo info, int line, float minX, float maxX, float padX, float padY)
        {
            float top, bottom;
            if (line >= 0 && line < info.lineCount)
            {
                top = info.lineInfo[line].ascender;
                bottom = info.lineInfo[line].descender;
            }
            else
            {
                top = 0f; bottom = 0f;
            }

            return new Rect(minX - padX,
                            bottom - padY,
                            (maxX - minX) + padX * 2f,
                            (top - bottom) + padY * 2f);
        }
    }
}
