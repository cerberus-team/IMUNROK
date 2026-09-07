using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>장면 하나를 영상으로 찍는다.</b> 메뉴: [이문록 ▸ 영상 ▸ 아궁이 뒤지는 장면 찍기]
    ///
    /// 사람이 손으로 돌아다니며 찍으면 손이 떨리고 같은 그림이 두 번 안 나온다.
    /// <b>카메라를 코드로 몰아</b> 못 박은 자리들을 지나가게 하고, 그 사이에 재를
    /// 헤집는 그때를 끼워 넣는다. 몇 번을 다시 찍어도 같은 그림이 나온다.
    ///
    /// <b>왜 씬 둘을 겹쳐 여나.</b> 1막은 <c>Onggojip</c> 이 껍데기고 집(김명관 고택)은
    /// <c>Onggojip_마당</c> 에 따로 들어 있어 놀 때 겹쳐 얹힌다. 껍데기만 열고 찍으면
    /// <b>회색 벌판에 검은 상자 하나</b>가 찍힌다 — 처음에 그렇게 찍어 놓고 왜 아무것도
    /// 없나 했다.
    ///
    /// <b>도메인이 한 번 뒤집힌다.</b> 플레이로 들어갈 때 대본이 통째로 다시 실려
    /// 이 클래스의 값도 다 지워진다. 그래서 "찍는 중"이라는 표시만 <see cref="SessionState"/>
    /// 에 적어 두고, 다시 실린 뒤 <c>InitializeOnLoad</c> 가 그 표시를 보고 이어 간다.
    ///
    /// 결과는 <c>Captures/</c> 밑에 떨어진다 — Assets 밖이라 유니티가 자산으로 물지 않는다.
    /// </summary>
    [InitializeOnLoad]
    public static class ShotRecorder
    {
        private const string Key = "이문록.찍는중";
        private const string Shell = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string Yard  = "Assets/_Project/Onggojip/Scenes/Onggojip_마당.unity";

        private const int W = 1920, H = 1080, Fps = 30;

        /// <summary>재를 헤집는 그때(초). 카메라가 다 숙이고 나서다.</summary>
        private const float RakeAt = 6.4f;

        /// <summary>
        /// 카메라가 지나갈 자리. { 있을 자리, 볼 자리, 여기까지 오는 데 걸리는 시간 }
        ///
        /// 아궁이는 기단 모서리에 걸터앉아 있고 동쪽은 마루가 막아, 트인 데가
        /// <b>북쪽(+z)</b> 하나다 — 사방으로 광선을 쏴 보고 정한 길이다.
        /// </summary>
        private static readonly Leg[] Path =
        {
            new Leg(new Vector3(4.30f, 0.35f, -6.10f), new Vector3(4.40f, -1.00f, -10.95f), 0.0f),
            new Leg(new Vector3(4.30f, 0.10f, -8.10f), new Vector3(4.35f, -1.20f, -10.95f), 3.2f),
            new Leg(new Vector3(4.30f, -0.35f, -9.30f), new Vector3(4.30f, -1.32f, -10.95f), 2.6f),
            new Leg(new Vector3(4.30f, -0.55f, -9.85f), new Vector3(4.30f, -1.32f, -10.95f), 1.4f),  // 숙인다
            new Leg(new Vector3(4.30f, -0.55f, -9.85f), new Vector3(4.30f, -1.32f, -10.95f), 2.2f),  // 헤집는 동안 멈춤
            new Leg(new Vector3(4.62f, -0.62f, -9.95f), new Vector3(4.25f, -1.33f, -10.98f), 3.0f),  // 드러난 것을 본다
        };

        private struct Leg
        {
            public readonly Vector3 At, Look; public readonly float Secs;
            public Leg(Vector3 at, Vector3 look, float secs) { At = at; Look = look; Secs = secs; }
        }

        static ShotRecorder()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Roll;
        }

        [MenuItem("이문록/영상/아궁이 뒤지는 장면 찍기")]
        private static void Shoot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[영상] 플레이를 멈추고 다시 누르십시오."); return; }

            EditorSceneManager.OpenScene(Shell, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(Yard, OpenSceneMode.Additive);

            SessionState.SetBool(Key, true);
            EditorApplication.update += Roll;
            EditorApplication.EnterPlaymode();
        }

        // ── 찍기 ──────────────────────────────────

        private static RecorderController _rec;
        private static Camera _cam;
        private static double _t0 = -1;
        private static bool _raked;

        private static void Roll()
        {
            if (!SessionState.GetBool(Key, false)) { EditorApplication.update -= Roll; return; }
            if (!EditorApplication.isPlaying) return;                 // 아직 들어가는 중

            if (_t0 < 0)
            {
                if (!Begin()) { Finish(); return; }
                _t0 = EditorApplication.timeSinceStartup;
            }

            float t = (float)(EditorApplication.timeSinceStartup - _t0);
            Place(t);

            if (!_raked && t >= RakeAt) { Rake(); _raked = true; }
            if (t >= Total()) Finish();
        }

        private static float Total()
        {
            float s = 0f;
            for (int i = 0; i < Path.Length; i++) s += Path[i].Secs;
            return s;
        }

        private static bool Begin()
        {
            // <b>제 카메라를 세운다.</b> 씬의 카메라를 빌려 쓰면 그 위에 붙은 손·도구·HUD
            // 가 화면 앞을 가리고, 플레이어를 움직이는 대본과 자리를 두고 다툰다.
            var go = new GameObject("_촬영카메라");
            _cam = go.AddComponent<Camera>();
            _cam.depth = 100f;                                        // 씬 카메라보다 위에 그린다
            _cam.fieldOfView = 62f;
            _cam.nearClipPlane = 0.05f;

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRate = Fps;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "아궁이";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = W, OutputHeight = H };
            movie.AudioInputSettings.PreserveAudio = false;
            movie.OutputFile = System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(), "Captures", "아궁이_뒤지기");

            settings.AddRecorderSettings(movie);
            _rec = new RecorderController(settings);
            _rec.PrepareRecording();
            _rec.StartRecording();
            return true;
        }

        /// <summary>지나온 시간만큼 길 위의 자리를 매긴다.</summary>
        private static void Place(float t)
        {
            if (_cam == null) return;
            var at = Path[0].At; var look = Path[0].Look;
            float acc = 0f;
            for (int i = 1; i < Path.Length; i++)
            {
                float dur = Mathf.Max(Path[i].Secs, 0.0001f);
                if (t <= acc + dur)
                {
                    // 부드럽게 들고 난다. 등속으로 밀면 시작과 끝이 툭 끊긴다.
                    float k = Mathf.SmoothStep(0f, 1f, (t - acc) / dur);
                    at = Vector3.Lerp(Path[i - 1].At, Path[i].At, k);
                    look = Vector3.Lerp(Path[i - 1].Look, Path[i].Look, k);
                    _cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at));
                    return;
                }
                acc += dur;
                at = Path[i].At; look = Path[i].Look;
            }
            _cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at));
        }

        /// <summary>재를 헤집는다. 부품에게 시키지 않고 눈에 보이는 것만 갈아 끼운다.</summary>
        private static void Rake()
        {
            var pile = GameObject.Find("게임요소/단서_소품/재무더기_J10");
            if (pile == null) { Debug.LogWarning("[영상] 재무더기를 못 찾았다"); return; }
            var before = pile.transform.Find("재_덮인");
            var after = pile.transform.Find("재_헤집힌");
            if (before != null) before.gameObject.SetActive(false);
            if (after != null) after.gameObject.SetActive(true);
        }

        private static void Finish()
        {
            if (_rec != null && _rec.IsRecording()) _rec.StopRecording();
            _rec = null;
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            _cam = null;
            _t0 = -1; _raked = false;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Roll;
            EditorApplication.ExitPlaymode();
            Debug.Log("[영상] 다 찍었다 — Captures/아궁이_뒤지기.mp4");
        }
    }
}
