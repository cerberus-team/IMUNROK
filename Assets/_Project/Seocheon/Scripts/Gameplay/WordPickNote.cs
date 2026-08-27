// 지목할 문장과 어절 데이터를 인스펙터에 입력하고, 상호작용 시 UI에 전달하는 수집 대상입니다.
using System;
using System.Collections.Generic;
using IMUNROK.Common;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 한 인물의 진술을 문장 단위로 보여 주고, 플레이어가 어절 하나를 지목하게 하는 수집 대상.
    ///
    /// 문장 출처는 두 가지이며 ★이후 경로는 완전히 같습니다.
    ///   ① 인스펙터 고정 대사 (지금)
    ///   ② 런타임 주입 SetSentences(...) — 나중에 공통 AI 응답을 문장 배열로 바꿔 넣는 자리
    /// UI 는 Sentence 배열만 받으므로 문장의 출처를 알지 못합니다.
    /// </summary>
    public sealed class WordPickNote : MonoBehaviour
    {
        [Serializable]
        public sealed class Sentence
        {
            [TextArea]
            [Tooltip("화면에 표시할 한 문장")]
            public string text = string.Empty;
            [Tooltip("플레이어가 지목할 수 있는 어절 후보. 비어 있어도 됩니다(짚을 것이 없는 문장)")]
            public WordOption[] options = Array.Empty<WordOption>();
        }

        [Serializable]
        public sealed class WordOption
        {
            [Tooltip("문장 안에서 지목할 어절. text 안에 그대로 존재해야 합니다")]
            public string word = string.Empty;
            [Tooltip("유효 단서인지 여부")]
            public bool isValid;
            [Tooltip("수첩 중복 방지용 단서 ID")]
            public string clueId = string.Empty;
            [Tooltip("수첩에 표시할 문구")]
            public string journalText = string.Empty;
        }

        // ★유효/오답을 가르던 "X_" · "S_" 접두사는 폐기했습니다.
        //   지목 시점에는 판정하지 않습니다. 수첩 key 는 SeocheonClueStore 가 전부 같은 형식으로 만듭니다.

        [Tooltip("표시용 화자 이름. 비워도 됩니다")]
        [SerializeField] private string speakerName = string.Empty;

        [Tooltip("표시할 문장 목록")]
        [SerializeField] private Sentence[] sentences = Array.Empty<Sentence>();

        [Tooltip("문장과 어절을 표시할 TMP UI")]
        [SerializeField] private WordPickUI wordPickUI;

        [Tooltip("수집 현황 표시기. 비워도 동작합니다")]
        [SerializeField] private CollectionMeter collectionMeter;

        [Tooltip("이미 수집을 마친 노트인지. 켜져 있으면 다시 열리지 않습니다")]
        [SerializeField] private bool alreadyCollected;

        [Tooltip("E키 상호작용을 이 컴포넌트가 직접 감지할지 여부")]
        [SerializeField] private bool listenForInteract;

        private readonly List<Sentence> runtimeSentences = new List<Sentence>();
        private int sentenceIndex;
        private bool isRunning;

        /// <summary>지금 사용 중인 문장 목록(인스펙터 값 또는 런타임 주입본).</summary>
        public IReadOnlyList<Sentence> Sentences { get { return runtimeSentences; } }
        public bool IsCollected { get { return alreadyCollected; } }
        public bool IsRunning { get { return isRunning; } }
        public string SpeakerName { get { return speakerName; } }

        /// <summary>모든 문장을 마쳤을 때 발생합니다.</summary>
        public event Action<WordPickNote> Finished;

        private void Awake()
        {
            SyncRuntimeFromInspector();
        }

        private void Update()
        {
            if (!isRunning && listenForInteract && SeocheonInput.InteractPressedThisFrame)
                Begin();
        }

        private void OnDisable()
        {
            if (isRunning) Abort();
        }

        // ─────────────────────────────────────────────
        //  외부 주입 (향후 AI 연동 자리)
        // ─────────────────────────────────────────────

        /// <summary>
        /// 문장을 런타임에 교체합니다. 인스펙터 데이터는 그대로 두고 실행 중 목록만 바꿉니다.
        /// ★나중에 공통 심문 시스템의 AI 응답을 문장으로 쪼개 여기에 넣으면 그대로 동작합니다.
        /// </summary>
        public void SetSentences(IList<Sentence> newSentences)
        {
            runtimeSentences.Clear();
            if (newSentences == null) return;
            for (int i = 0; i < newSentences.Count; i++)
                if (newSentences[i] != null) runtimeSentences.Add(newSentences[i]);
        }

        /// <summary>런타임 목록을 인스펙터 값으로 되돌립니다.</summary>
        public void SyncRuntimeFromInspector()
        {
            SetSentences(sentences);
        }

        /// <summary>수집 완료 플래그를 외부에서 조작합니다(세이브 복원 등).</summary>
        public void SetCollected(bool value)
        {
            alreadyCollected = value;
        }

        // ─────────────────────────────────────────────
        //  진행
        // ─────────────────────────────────────────────

        public void Begin()
        {
            if (isRunning) return;
            if (alreadyCollected) return;

            if (wordPickUI == null)
            {
                Debug.LogWarning("[WordPickNote] " + name + " 에 WordPickUI 가 연결되지 않았습니다.", this);
                return;
            }

            if (runtimeSentences.Count == 0)
            {
                // 넣을 문장이 없으면 조용히 끝냅니다(멈추지 않음).
                Finish();
                return;
            }

            sentenceIndex = 0;
            isRunning = true;
            ShowCurrentSentence();
        }

        /// <summary>UI 가 어절 지목을 알려 옵니다.</summary>
        public void Pick(WordOption option)
        {
            if (!isRunning) return;

            if (option == null)
            {
                Advance();
                return;
            }

            // ★유효/오답을 가르지 않습니다. 문장 전체를 같은 형식으로 적어 둘 뿐입니다.
            string sentence = CurrentSentenceText();
            SeocheonClueStore.Add(speakerName,
                                  string.IsNullOrEmpty(sentence) ? option.word : sentence,
                                  option.word, option.clueId);

            if (collectionMeter != null) collectionMeter.RecordCollect();

            Advance();
        }

        /// <summary>UI 가 "짚을 것이 없다" 를 알려 옵니다. 아무것도 기록하지 않고 넘어갑니다.</summary>
        public void Skip()
        {
            if (!isRunning) return;
            Advance();
        }

        /// <summary>
        /// ★폴백: 어절 판정이 불가능한 문장. 문장 전체를 수첩에 넣고 넘어갑니다.
        /// 유효도 오답도 아니므로 집계에는 넣지 않습니다.
        /// </summary>
        public void FallbackWholeSentence()
        {
            if (!isRunning) return;

            string text = CurrentSentenceText();
            if (!string.IsNullOrEmpty(text))
            {
                // ★일반 지목과 같은 형식으로 들어갑니다. 폴백이었다는 흔적을 수첩에 남기지 않습니다.
                SeocheonClueStore.Add(speakerName, text, string.Empty, string.Empty);
                Debug.LogWarning("[WordPickNote] " + name + " 문장 " + sentenceIndex +
                                 " 의 어절 판정에 실패해 문장 전체를 기록했습니다.", this);
            }

            Advance();
        }

        /// <summary>진행 중인 수집을 중단하고 UI 를 닫습니다(수집 완료로 치지 않습니다).</summary>
        public void Abort()
        {
            if (!isRunning) return;
            isRunning = false;
            if (wordPickUI != null) wordPickUI.Hide();
        }

        private void Advance()
        {
            sentenceIndex++;
            if (sentenceIndex >= runtimeSentences.Count)
            {
                Finish();
                return;
            }
            ShowCurrentSentence();
        }

        private void Finish()
        {
            isRunning = false;
            alreadyCollected = true;
            if (wordPickUI != null) wordPickUI.Hide();
            if (Finished != null) Finished(this);
        }

        private void ShowCurrentSentence()
        {
            if (wordPickUI == null || sentenceIndex < 0 || sentenceIndex >= runtimeSentences.Count)
            {
                Finish();
                return;
            }

            Sentence s = runtimeSentences[sentenceIndex];
            if (s == null || string.IsNullOrEmpty(s.text))
            {
                Advance();   // 빈 문장은 건너뜁니다(멈추지 않음)
                return;
            }

            wordPickUI.Show(s, speakerName, sentenceIndex, runtimeSentences.Count,
                            Pick, Skip, FallbackWholeSentence);
        }

        private string CurrentSentenceText()
        {
            if (sentenceIndex < 0 || sentenceIndex >= runtimeSentences.Count) return string.Empty;
            Sentence s = runtimeSentences[sentenceIndex];
            return s == null ? string.Empty : (s.text ?? string.Empty);
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────
        //  에디터 검증 — word 가 text 안에 없으면 여기서 잡습니다
        // ─────────────────────────────────────────────
        private void OnValidate()
        {
            if (sentences == null) return;

            for (int i = 0; i < sentences.Length; i++)
            {
                Sentence s = sentences[i];
                if (s == null || s.options == null) continue;

                for (int j = 0; j < s.options.Length; j++)
                {
                    WordOption o = s.options[j];
                    if (o == null || string.IsNullOrEmpty(o.word)) continue;

                    string where = name + " 문장[" + i + "] 어절[" + j + "] \"" + o.word + "\"";

                    int first = string.IsNullOrEmpty(s.text)
                        ? -1
                        : s.text.IndexOf(o.word, StringComparison.Ordinal);

                    if (first < 0)
                    {
                        Debug.LogWarning("[WordPickNote] " + where +
                                         " 가 문장 안에 없습니다. 런타임에는 이 어절을 무시합니다.", this);
                        continue;
                    }

                    if (first + o.word.Length < s.text.Length &&
                        s.text.IndexOf(o.word, first + 1, StringComparison.Ordinal) >= 0)
                        Debug.LogWarning("[WordPickNote] " + where +
                                         " 가 문장 안에 두 번 이상 나옵니다. 첫 번째 것만 지목됩니다.", this);

                    if (o.isValid && string.IsNullOrEmpty(o.clueId))
                        Debug.LogWarning("[WordPickNote] " + where +
                                         " 는 유효인데 clueId 가 비었습니다. 어절 자체가 수첩 key 로 쓰입니다.", this);
                }
            }
        }
#endif
    }
}
