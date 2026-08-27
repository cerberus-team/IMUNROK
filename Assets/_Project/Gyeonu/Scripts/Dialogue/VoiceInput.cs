using System;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 목소리로 묻기 (2026-08-25). <b>V를 누르고 있는 동안 녹음, 떼면 받아쓰기</b>.
    ///
    /// ■ 왜 Unity 내장 음성 인식을 안 쓰는가 — 이 기계에서 실측
    ///     PhraseRecognitionSystem.isSupported = False
    ///     new DictationRecognizer() → UnityException "Speech recognition is not supported on this machine."
    ///   내장 받아쓰기(<c>UnityEngine.Windows.Speech</c>)는 윈도 음성 인식 기능이 켜져 있어야 하고
    ///   한국어 음성 팩이 따로 필요하며, <b>Quest(안드로이드)에서는 아예 없다</b>. 최종 목표가
    ///   Quest 3 이므로 처음부터 쓸 수 없는 길이다.
    ///
    ///   그래서 <b>녹음만 유니티가 하고, 알아듣는 일은 Gemini에게 맡긴다</b>
    ///   (<see cref="GyeonuGeminiResponder.Transcribe"/>). 대사와 같은 통로라 키·설정이 하나뿐이고,
    ///   한국어를 그대로 알아들으며, PC든 Quest든 같은 코드가 돈다.
    ///
    /// ■ 받아쓴 글은 <b>입력칸에 올릴 뿐</b> 바로 보내지 않는다
    ///   잘못 알아들었을 때 고칠 수 있어야 한다. 보내기는 글자로 칠 때와 완전히 같은 길(Enter/묻기)이다.
    ///
    /// ■ 샘플레이트
    ///   이 기계의 마이크는 48kHz 고정이다(<c>GetDeviceCaps 48000~48000</c>). 말소리에 48kHz는
    ///   과하고 전송량만 3배가 되므로 16kHz로 줄여 WAV로 굽는다 — 받아쓰기 품질에는 차이가 없다.
    /// </summary>
    public class VoiceInput
    {
        /// <summary>녹음 최대 길이(초). 이보다 길게 눌러도 앞에서부터 이만큼만 남는다.</summary>
        public const int MaxSeconds = 20;

        /// <summary>받아쓰기에 넘길 표본율. 말소리에는 16kHz면 넉넉하다.</summary>
        public const int TargetHz = 16000;

        /// <summary>이보다 짧게 눌렀다 떼면 실수로 본다 — 부르지 않는다.</summary>
        public const float MinSeconds = 0.35f;

        public bool Recording { get; private set; }
        public float ElapsedSeconds => Recording ? Time.unscaledTime - _startTime : 0f;
        /// <summary>지금 잡히는 소리 크기 0~1 — 판에 막대로 그려 "들리고 있다"를 보여 준다.</summary>
        public float Level { get; private set; }

        public static bool HasMicrophone => Microphone.devices != null && Microphone.devices.Length > 0;
        public static string DeviceName => HasMicrophone ? Microphone.devices[0] : null;

        AudioClip _clip;
        string _device;
        float _startTime;
        int _deviceHz;
        readonly float[] _peek = new float[256];

        public bool Begin()
        {
            if (Recording) return true;
            if (!HasMicrophone) { Debug.LogWarning("[목소리] 마이크가 없다."); return false; }

            _device = Microphone.devices[0];
            int min, max;
            Microphone.GetDeviceCaps(_device, out min, out max);
            // 0,0 은 "아무 값이나 된다"는 뜻이다. 그 외에는 장치가 허락하는 범위 안으로 맞춘다.
            _deviceHz = (min == 0 && max == 0) ? TargetHz : Mathf.Clamp(TargetHz, min, max);

            _clip = Microphone.Start(_device, false, MaxSeconds, _deviceHz);
            if (_clip == null) { Debug.LogWarning("[목소리] 녹음을 시작하지 못했다."); return false; }

            Recording = true;
            Level = 0f;
            _startTime = Time.unscaledTime;
            return true;
        }

        /// <summary>매 프레임 불러 소리 크기를 갱신한다 (막대 표시용).</summary>
        public void Tick()
        {
            if (!Recording || _clip == null) return;
            int pos = Microphone.GetPosition(_device) - _peek.Length;
            if (pos < 0) return;
            if (!_clip.GetData(_peek, pos)) return;
            float peak = 0f;
            for (int i = 0; i < _peek.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(_peek[i]));
            Level = Mathf.Lerp(Level, Mathf.Clamp01(peak * 4f), 0.35f);
        }

        /// <summary>녹음을 멈추고 WAV 바이트를 돌려준다. 너무 짧으면 null.</summary>
        public byte[] End()
        {
            if (!Recording) return null;
            Recording = false;
            Level = 0f;

            int written = Microphone.GetPosition(_device);
            Microphone.End(_device);

            float seconds = Time.unscaledTime - _startTime;
            if (_clip == null || written <= 0 || seconds < MinSeconds)
            {
                if (_clip != null) UnityEngine.Object.Destroy(_clip);
                _clip = null;
                return null;
            }

            var samples = new float[written * _clip.channels];
            _clip.GetData(samples, 0);
            int channels = _clip.channels;
            UnityEngine.Object.Destroy(_clip);
            _clip = null;

            var mono = ToMono(samples, channels);
            var resampled = _deviceHz == TargetHz ? mono : Resample(mono, _deviceHz, TargetHz);
            return EncodeWav(resampled, TargetHz);
        }

        /// <summary>녹음을 버린다 (대화가 끝났는데 아직 누르고 있는 경우 등).</summary>
        public void Cancel()
        {
            if (!Recording) return;
            Recording = false;
            Level = 0f;
            Microphone.End(_device);
            if (_clip != null) { UnityEngine.Object.Destroy(_clip); _clip = null; }
        }

        // ── 소리 다듬기 ──────────────────────────────────────────
        static float[] ToMono(float[] interleaved, int channels)
        {
            if (channels <= 1) return interleaved;
            int frames = interleaved.Length / channels;
            var mono = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += interleaved[i * channels + c];
                mono[i] = sum / channels;
            }
            return mono;
        }

        /// <summary>선형 보간 리샘플. 말소리를 글로 옮기는 데는 이 정도면 충분하다.</summary>
        static float[] Resample(float[] src, int fromHz, int toHz)
        {
            if (src.Length == 0 || fromHz == toHz) return src;
            int outLen = Mathf.Max(1, (int)((long)src.Length * toHz / fromHz));
            var dst = new float[outLen];
            float step = (float)(src.Length - 1) / Mathf.Max(1, outLen - 1);
            for (int i = 0; i < outLen; i++)
            {
                float x = i * step;
                int i0 = (int)x;
                int i1 = Mathf.Min(i0 + 1, src.Length - 1);
                dst[i] = Mathf.Lerp(src[i0], src[i1], x - i0);
            }
            return dst;
        }

        /// <summary>16비트 PCM 모노 WAV. Gemini가 <c>audio/wav</c> 로 그대로 받는다.</summary>
        public static byte[] EncodeWav(float[] samples, int hz)
        {
            int dataBytes = samples.Length * 2;
            var bytes = new byte[44 + dataBytes];
            int p = 0;
            void Str(string s) { foreach (char c in s) bytes[p++] = (byte)c; }
            void I32(int v) { bytes[p++] = (byte)v; bytes[p++] = (byte)(v >> 8); bytes[p++] = (byte)(v >> 16); bytes[p++] = (byte)(v >> 24); }
            void I16(int v) { bytes[p++] = (byte)v; bytes[p++] = (byte)(v >> 8); }

            Str("RIFF"); I32(36 + dataBytes); Str("WAVE");
            Str("fmt "); I32(16); I16(1); I16(1);      // PCM, 모노
            I32(hz); I32(hz * 2); I16(2); I16(16);      // 바이트율, 블록정렬, 비트수
            Str("data"); I32(dataBytes);

            for (int i = 0; i < samples.Length; i++)
            {
                int v = Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767);
                bytes[p++] = (byte)v;
                bytes[p++] = (byte)(v >> 8);
            }
            return bytes;
        }
    }
}
