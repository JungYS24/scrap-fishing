using ScrapFishing.Audio;
using ScrapFishing.Core;
using ScrapFishing.Controls;
using ScrapFishing.Fishing;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class SwipeCatcher : MonoBehaviour
    {
        [SerializeField] bool catchOnSwipe;
        [SerializeField] float hookHitRadius = 1.35f;
        [SerializeField] float catchRadius = 1.05f;

        HookMover _hook;
        ScrapSpawner _spawner;
        RunSession _session;
        LureCollector _collector;

        public void Bind(HookMover hook, ScrapSpawner spawner, RunSession session, LureCollector collector)
        {
            _hook = hook;
            _spawner = spawner;
            _session = session;
            _collector = collector;
        }

        public bool HandleSwipe(SwipeInfo swipe)
        {
            if (!catchOnSwipe || _hook == null || _spawner == null || (_collector != null && _collector.IsSettling))
            {
                return false;
            }

            var hookPos = _hook.Position;
            var distanceToSwipe = DistanceToSegment(hookPos, swipe.StartWorld, swipe.EndWorld);
            if (distanceToSwipe > hookHitRadius)
            {
                return false;
            }

            var radius = catchRadius + Progression.CatchRadiusBonus;
            for (var i = _spawner.Live.Count - 1; i >= 0; i--)
            {
                var scrap = _spawner.Live[i];
                if (scrap == null)
                {
                    continue;
                }

                if (scrap.Attached || Vector3.Distance(scrap.transform.position, hookPos) > radius)
                {
                    continue;
                }

                if (LureLoad.IsHazard(scrap.ChipValue))
                {
                    _session.AddCatch(scrap.Definition, scrap.data, scrap.transform.position);
                    AudioManager.Ensure().PlayHazard();
                    LureCollector.PresentHazard(scrap.transform.position, scrap.ChipValue, _collector);
                    _spawner.Remove(scrap);
                    continue;
                }

                if (_collector != null && _collector.TryAttach(scrap))
                {
                    _spawner.Unregister(scrap);
                }
            }

            return true;
        }

        static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f)
            {
                return Vector3.Distance(point, a);
            }

            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return Vector3.Distance(point, a + ab * t);
        }
    }
}
