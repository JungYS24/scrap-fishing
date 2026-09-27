using System;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class CastingController : MonoBehaviour
    {
        [SerializeField] float descendSpeed = 6.5f;
        [SerializeField] float reelSpeed = 3.4f;

        GameFlow _flow;
        RunSession _session;
        DepthGauge _gauge;
        HookMover _hook;
        ScrapSpawner _spawner;

        float _targetY;
        bool _descending;

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

            _session.CastDepth = _gauge.Normalized;
            _targetY = Mathf.Lerp(SurfaceLayout.WaterlineY - 0.5f, SurfaceLayout.MaxDepthY, _gauge.Normalized);
            _gauge.SetLocked(true);
            _hook.Place(new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f));
            _spawner.SpawnForCast(SurfaceLayout.WaterlineY, _targetY);
            _descending = true;
            _flow.BeginCasting();
        }

        public void ResetHook()
        {
            _gauge.SetLocked(false);
            _hook.Place(new Vector3(SurfaceLayout.HookX, SurfaceLayout.WaterlineY, 0f));
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
                StepToward(_targetY, descendSpeed, () =>
                {
                    _descending = false;
                    _flow.BeginReeling();
                });
            }
            else if (_flow.Phase == GamePhase.Reeling)
            {
                StepToward(SurfaceLayout.WaterlineY, reelSpeed, () => _flow.CompleteCast());
            }
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
