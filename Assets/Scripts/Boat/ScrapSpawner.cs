using System.Collections.Generic;
using ScrapFishing.Core;
using ScrapFishing.Dive;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class ScrapSpawner : MonoBehaviour
    {
        static readonly float[] DiveFishScale = { 1.25f, 1.15f, 1.05f };

        readonly List<ScrapView> _live = new List<ScrapView>();
        DiveArt _diveArt;

        public IReadOnlyList<ScrapView> Live => _live;

        public void SetDiveArt(DiveArt art)
        {
            _diveArt = art;
        }

        public void SpawnForDive(float depth)
        {
            Clear();
            var count = 8 + DepthZone.Index(depth);
            for (var i = 0; i < count; i++)
            {
                var definition = ScrapCatalog.Pick(depth);
                var position = new Vector3(Random.Range(-1.7f, 1.7f), Random.Range(-3.6f, 2.4f), 0f);
                if (_diveArt == null)
                {
                    SpawnOne(definition, position, 0.22f, 0.16f, null);
                    continue;
                }

                var kind = DiveArt.FishKind(definition.Grade);
                var frames = _diveArt.Fish(kind);
                var view = SpawnOne(definition, position, 0.32f, 0.16f, frames.Length > 0 ? frames[0] : null);
                view.transform.localScale = Vector3.one * DiveFishScale[kind];
                var animator = view.gameObject.AddComponent<SpriteAnimator>();
                animator.Play(frames, 7f, true);
                animator.FaceMovement(true);
            }
        }

        public void SpawnForCast(float surfaceY, float targetY, float depth)
        {
            Clear();
            var count = DepthZone.SpawnCount(depth);
            for (var i = 0; i < count; i++)
            {
                var along = Random.value < 0.42f ? Random.Range(0.72f, 1f) : Random.Range(0.12f, 0.72f);
                var y = Mathf.Lerp(surfaceY - 0.45f, targetY + 0.15f, along);
                SpawnOne(
                    ScrapCatalog.Pick(DepthZone.FromWorldY(y)),
                    new Vector3(Random.Range(-1.35f, 1.35f), y, 0f),
                    0.08f,
                    0.05f,
                    null);
            }
        }

        ScrapView SpawnOne(ScrapDefinition definition, Vector3 position, float rangeX, float rangeY, Sprite spriteOverride)
        {
            var go = new GameObject(definition.DisplayName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (0.26f + (int)definition.Grade * 0.055f + Random.Range(-0.02f, 0.03f));
            var view = go.AddComponent<ScrapView>();
            view.Bind(definition, spriteOverride);
            go.AddComponent<AmbientDrift>().Configure(position, rangeX, rangeY, Random.Range(0.45f, 0.85f));
            _live.Add(view);
            return view;
        }

        public void CollectNear(Vector3 hook, float radius, RunSession session)
        {
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var scrap = _live[i];
                if (scrap == null)
                {
                    continue;
                }

                if (Vector3.Distance(scrap.transform.position, hook) <= radius)
                {
                    session.AddCatch(scrap.Definition);
                    Remove(scrap);
                }
            }
        }

        public void AttractToward(Vector3 hook, float radius, float speed)
        {
            for (var i = 0; i < _live.Count; i++)
            {
                var scrap = _live[i];
                if (scrap == null)
                {
                    continue;
                }

                var delta = hook - scrap.transform.position;
                if (delta.sqrMagnitude > radius * radius)
                {
                    continue;
                }

                var drift = scrap.GetComponent<AmbientDrift>();
                if (drift != null)
                {
                    drift.PullOrigin(hook, speed * Time.deltaTime);
                }
            }
        }

        public void Remove(ScrapView view)
        {
            _live.Remove(view);
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _live.Count; i++)
            {
                if (_live[i] != null)
                {
                    Destroy(_live[i].gameObject);
                }
            }

            _live.Clear();
        }
    }
}
