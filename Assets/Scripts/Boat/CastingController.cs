using System;
using System.Collections;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class CastingController : MonoBehaviour
    {
        [Header("Wind-up")]
        [SerializeField] float pullBackDistance = 0.4f;
        [SerializeField] float pullBackLift = 0.12f;
        [SerializeField] float pullBackTime = 0.12f;
        [SerializeField] AnimationCurve pullBackEase = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Flight")]
        [SerializeField] float flightTime = 0.25f;
        [SerializeField] float arcHeight = 0.7f;
        [SerializeField] AnimationCurve flightEase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Steer")]
        [SerializeField] float steerSensitivity = 1f;
        [SerializeField] float maxSteerSpeed = 9f;
        [SerializeField] float steerSmoothTime = 0.08f;
        [SerializeField] float steerMinX = -1.6f;
        [SerializeField] float steerMaxX = 1.6f;
        [SerializeField] float maxTiltDegrees = 15f;
        [SerializeField] float tiltDegreesPerSecond = 120f;
        [SerializeField] float surfaceReturnDistance = 0.4f;
        [SerializeField] float surfaceReturnSpeed = 24f;

        [Header("Catch")]
        [SerializeField] float collectRadius = 0.5f;
        [SerializeField] float attractRadius = 0.8f;
        [SerializeField] float attractSpeed = 1.5f;

        GameFlow _flow;
        RunSession _session;
        DepthGauge _gauge;
        HookMover _hook;
        ScrapSpawner _spawner;

        float _targetY;
        bool _descending;
        float _dwell;
        int _zone;
        Coroutine _launch;
        float _steerTargetX;
        float _steerX;
        float _steerVelocity;
        float _tilt;
        int _castSerial;

        public bool IsLaunching => _launch != null;

        public bool AcceptsSteer =>
            _flow != null
            && !IsLaunching
            && !IsHoming
            && ((_flow.Phase == GamePhase.Casting && (_descending || _dwell > 0f)) || _flow.Phase == GamePhase.Reeling);

        public bool ShowSteerGuide => _castSerial == 1 && AcceptsSteer;

        bool IsHoming =>
            _flow != null
            && _hook != null
            && _flow.Phase == GamePhase.Reeling
            && _hook.Position.y >= SurfaceLayout.WaterlineY - surfaceReturnDistance;

        public void Bind(GameFlow flow, RunSession session, DepthGauge gauge, HookMover hook, ScrapSpawner spawner)
        {
            _flow = flow;
            _session = session;
            _gauge = gauge;
            _hook = hook;
            _spawner = spawner;
            ResetSteer();
        }

        public void PrepareForRun()
        {
            _castSerial = 0;
            ResetHook();
        }

        public void AddSteerDelta(float worldDeltaX)
        {
            if (!AcceptsSteer)
            {
                return;
            }

            _steerTargetX = Mathf.Clamp(_steerTargetX + worldDeltaX * steerSensitivity, steerMinX, steerMaxX);
        }

        public void CastFromGauge()
        {
            if (_flow.Phase != GamePhase.Aiming)
            {
                return;
            }

            _castSerial++;
            ResetSteer();
            _session.RegisterCast(_gauge.Normalized);
            _targetY = Mathf.Lerp(SurfaceLayout.WaterlineY - 0.5f, SurfaceLayout.MaxDepthY, _session.CastDepth);
            _gauge.SetLocked(true);
            _spawner.SpawnForCast(SurfaceLayout.WaterlineY, _targetY, _session.CastDepth);
            _descending = false;
            _dwell = 0f;
            _zone = 0;
            _flow.BeginCasting();
            _launch = StartCoroutine(Launch());
        }

        public void ResetHook()
        {
            StopLaunch();
            _descending = false;
            _dwell = 0f;
            _gauge.SetLocked(false);
            ResetSteer();
            _hook.Place(EntryPoint);
            _hook.SetTilt(0f);
            _hook.Tint(Palette.NeonGreen);
            _spawner.Clear();
        }

        void ResetSteer()
        {
            _steerTargetX = SurfaceLayout.HookX;
            _steerX = SurfaceLayout.HookX;
            _steerVelocity = 0f;
            _tilt = 0f;
            if (_hook != null)
            {
                _hook.SetTilt(0f);
            }
        }

        static Vector3 EntryPoint => new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f);

        IEnumerator Launch()
        {
            var entry = EntryPoint;
            var back = (SurfaceLayout.RodTip - entry).normalized;
            var windUp = entry + back * pullBackDistance + Vector3.up * pullBackLift;

            yield return Tween(pullBackTime, t => _hook.Place(Vector3.LerpUnclamped(entry, windUp, pullBackEase.Evaluate(t))));
            yield return Tween(flightTime, t =>
            {
                var point = Vector3.LerpUnclamped(windUp, entry, flightEase.Evaluate(t));
                point.y += arcHeight * 4f * t * (1f - t);
                _hook.Place(point);
            });

            _hook.Place(entry);
            SplashBurst.Spawn(entry);
            AudioManager.Ensure().PlaySplash();
            _launch = null;
            _descending = true;
        }

        static IEnumerator Tween(float duration, Action<float> step)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                step(elapsed / duration);
                yield return null;
                elapsed += Time.deltaTime;
            }

            step(1f);
        }

        void StopLaunch()
        {
            if (_launch == null)
            {
                return;
            }

            StopCoroutine(_launch);
            _launch = null;
        }

        void Update()
        {
            if (_flow == null)
            {
                return;
            }

            if (_flow.Phase == GamePhase.Casting && _descending)
            {
                TickHook();
                StepToward(_targetY, Mathf.Lerp(7.2f, 5.2f, _session.CastDepth), () =>
                {
                    _descending = false;
                    _dwell = 0.28f;
                });
            }
            else if (_flow.Phase == GamePhase.Casting && _dwell > 0f)
            {
                TickHook();
                PlaceSteered(_hook.Position.y);
                _dwell -= Time.deltaTime;
                if (_dwell <= 0f)
                {
                    AudioManager.Ensure().PlayReel();
                    _flow.BeginReeling();
                }
            }
            else if (_flow.Phase == GamePhase.Reeling)
            {
                TickHook();
                StepToward(SurfaceLayout.WaterlineY, Mathf.Lerp(4.1f, 2.35f, _session.CastDepth), () => _flow.CompleteCast());
            }
        }

        void TickHook()
        {
            var hook = _hook.Position;
            var zone = DepthZone.Index(DepthZone.FromWorldY(hook.y));
            if (zone != _zone)
            {
                _zone = zone;
                AudioManager.Ensure().PlayZone(zone);
            }

            _hook.Tint(DepthZone.MarkerColor(DepthZone.FromWorldY(hook.y)));
            _spawner.AttractToward(hook, attractRadius, attractSpeed);
            _spawner.CollectNear(hook, collectRadius + Progression.CatchRadiusBonus, _session);
        }

        void StepToward(float y, float speed, Action arrived)
        {
            var current = _hook.Position;
            var nextY = Mathf.MoveTowards(current.y, y, speed * Time.deltaTime);
            PlaceSteered(nextY);
            if (Mathf.Abs(nextY - y) <= 0.01f)
            {
                arrived();
            }
        }

        void PlaceSteered(float y)
        {
            var previousX = _steerX;
            if (IsHomingY(y))
            {
                _steerTargetX = SurfaceLayout.HookX;
                _steerX = Mathf.MoveTowards(_steerX, SurfaceLayout.HookX, surfaceReturnSpeed * Time.deltaTime);
                _steerVelocity = 0f;
            }
            else
            {
                _steerX = Mathf.SmoothDamp(
                    _steerX,
                    _steerTargetX,
                    ref _steerVelocity,
                    Mathf.Max(0.0001f, steerSmoothTime),
                    maxSteerSpeed,
                    Time.deltaTime);
            }

            var clamped = Mathf.Clamp(_steerX, steerMinX, steerMaxX);
            if (!Mathf.Approximately(clamped, _steerX))
            {
                _steerX = clamped;
                _steerVelocity = 0f;
            }

            _hook.Place(new Vector3(_steerX, y, 0f));
            var tiltSpeed = IsHomingY(y) ? (_steerX - previousX) / Mathf.Max(Time.deltaTime, 0.0001f) : _steerVelocity;
            _tilt = Mathf.MoveTowards(_tilt, SteerTilt(tiltSpeed), tiltDegreesPerSecond * Time.deltaTime);
            _hook.SetTilt(_tilt);
        }

        bool IsHomingY(float y)
        {
            return _flow != null
                && _flow.Phase == GamePhase.Reeling
                && y >= SurfaceLayout.WaterlineY - surfaceReturnDistance;
        }

        float SteerTilt(float speed)
        {
            if (maxSteerSpeed <= 0.01f)
            {
                return 0f;
            }

            var amount = Mathf.Clamp(speed / maxSteerSpeed, -1f, 1f);
            return -amount * maxTiltDegrees;
        }
    }
}
