using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// Play 모드 보행 검증용 오토파일럿 — 웨이포인트를 따라 CharacterController.Move로
    /// 실제 걷고, 끼임(1.5초간 0.15m 미만 이동)·낙수(y &lt; 0.25)를 기록한다.
    /// 디버그 워커 GO에 붙이면 DebugWalkController를 끄고 조종을 가져간다.
    /// 검증 전용 — 게임 로직에서 사용하지 않는다.
    /// </summary>
    public class DebugAutoWalker : MonoBehaviour
    {
        public Vector3[] waypoints;
        public float speed = 3f;
        public int wpIndex;
        public bool done;
        /// <summary>이 높이 아래로 내려가면 낙수·추락으로 기록. 실내 씬은 바닥이 음수라 씬마다 지정한다
        /// (관측실 바닥 -3.06, 수직갱 바닥 -11.3).</summary>
        public float fallY = 0.25f;
        public List<string> issues = new List<string>();

        CharacterController _cc;
        float _stuckTimer, _vy;
        Vector3 _lastPos;
        bool _wetLogged;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            var manual = GetComponent<DebugWalkController>();
            if (manual != null) manual.enabled = false;
            _lastPos = transform.position;
        }

        void Update()
        {
            if (done || waypoints == null || wpIndex >= waypoints.Length) { done = true; return; }

            var target = waypoints[wpIndex];
            var cur = transform.position;
            var to = new Vector3(target.x - cur.x, 0f, target.z - cur.z);
            if (to.magnitude < 0.6f)
            {
                wpIndex++;
                _stuckTimer = 0f;
                _wetLogged = false;
                return;
            }

            _vy = _cc.isGrounded ? -1f : _vy - 20f * Time.deltaTime;
            var mv = to.normalized * speed;
            mv.y = _vy;
            _cc.Move(mv * Time.deltaTime);

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 1.5f)
            {
                if (Vector3.Distance(transform.position, _lastPos) < 0.15f)
                {
                    issues.Add($"STUCK wp{wpIndex} @({transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1}) → ({target.x:F0},{target.z:F0})");
                    wpIndex++;   // 건너뛰고 계속 — 한 지점에서 전체가 멎지 않게
                }
                _lastPos = transform.position;
                _stuckTimer = 0f;
            }

            if (!_wetLogged && transform.position.y < fallY)
            {
                issues.Add($"WATER/FALL wp{wpIndex} @({transform.position.x:F1},{transform.position.y:F1},{transform.position.z:F1})");
                _wetLogged = true;
            }
        }
    }
}
