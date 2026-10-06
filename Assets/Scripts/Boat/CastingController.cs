using System;
using ScrapFishing.Audio;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class CastingController : MonoBehaviour
    {
        GameFlow _flow;
        RunSession _session;
        DepthGauge _gauge;
        HookMover _hook;
        ScrapSpawner _spawner;

        float _targetY;
        bool _descending;
        float _dwell;
        int _zone;

        public void Bind(GameFlow flow, RunSession session, DepthGauge gauge, HookMover hook, ScrapSpawner spawner)
        {
            _flow = flow;
            _session = session;
            _gauge = gauge;
            _hook = hook;
            _spawner = spawner;
        }

        public void CastFromGauge()
        {
            if (_flow.Phase != GamePhase.Aiming)
            {
                return;
            }

            _session.RegisterCast(_gauge.Normalized);
            _targetY = Mathf.Lerp(SurfaceLayout.WaterlineY - 0.5f, SurfaceLayout.MaxDepthY, _session.CastDepth);
            _gauge.SetLocked(true);
            _hook.Place(new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f));
            _spawner.SpawnForCast(SurfaceLayout.WaterlineY, _targetY, _session.CastDepth);
            _descending = true;
            _dwell = 0f;
            _zone = 0;
            _flow.BeginCasting();
        }

        public void ResetHook()
        {
            _gauge.SetLocked(false);
            _hook.Place(new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f));
            _hook.Tint(Palette.NeonGreen);
            _spawner.Clear();
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
            _spawner.AttractToward(hook, 1.4f, 2.6f);
            _spawner.CollectNear(hook, 0.5f, _session);
        }

        void StepToward(float y, float speed, Action arrived)
        {
            var current = _hook.Position;
            var nextY = Mathf.MoveTowards(current.y, y, speed * Time.deltaTime);
            _hook.Place(new Vector3(SurfaceLayout.HookX, nextY, 0f));
            if (Mathf.Abs(nextY - y) <= 0.01f)
            {
                arrived();
            }
        }
    }
}
