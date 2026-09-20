using ScrapFishing.Core;
using ScrapFishing.Controls;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class SwipeCatcher : MonoBehaviour
    {
        [SerializeField] float hookHitRadius = 1.15f;
        [SerializeField] float catchRadius = 0.85f;

        HookMover _hook;
        ScrapSpawner _spawner;
        RunSession _session;

        public void Bind(HookMover hook, ScrapSpawner spawner, RunSession session)
        {
            _hook = hook;
            _spawner = spawner;
            _session = session;
        }

        public void HandleSwipe(SwipeInfo swipe)
        {
            if (_hook == null || _spawner == null)
            {
                return;
            }

            var hookPos = _hook.Position;
            var distanceToSwipe = DistanceToSegment(hookPos, swipe.StartWorld, swipe.EndWorld);
            if (distanceToSwipe > hookHitRadius)
            {
                return;
            }

            for (var i = _spawner.Live.Count - 1; i >= 0; i--)
            {
                var scrap = _spawner.Live[i];
                if (scrap == null)
                {
                    continue;
                }

                if (Vector3.Distance(scrap.transform.position, hookPos) <= catchRadius)
                {
                    _session.AddCatch(scrap.Definition);
                    _spawner.Remove(scrap);
                }
            }
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
