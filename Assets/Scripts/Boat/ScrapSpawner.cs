using System.Collections.Generic;
using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class ScrapSpawner : MonoBehaviour
    {
        readonly List<ScrapView> _live = new List<ScrapView>();
        Sprite _diveSprite;

        public IReadOnlyList<ScrapView> Live => _live;

        public void SetDiveSprite(Sprite sprite)
        {
            _diveSprite = sprite;
        }

        public void SpawnForDive(float depth)
        {
            Clear();
            var count = 8 + DepthZone.Index(depth);
            for (var i = 0; i < count; i++)
            {
                SpawnOne(
                    ScrapCatalog.Pick(depth),
                    new Vector3(Random.Range(-1.7f, 1.7f), Random.Range(-3.6f, 2.4f), 0f),
                    0.22f,
                    0.16f,
                    _diveSprite);
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

        void SpawnOne(ScrapDefinition definition, Vector3 position, float rangeX, float rangeY, Sprite spriteOverride)
        {
            var go = new GameObject(definition.DisplayName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (0.26f + (int)definition.Grade * 0.055f + Random.Range(-0.02f, 0.03f));
            var view = go.AddComponent<ScrapView>();
            view.Bind(definition, spriteOverride);
            go.AddComponent<AmbientDrift>().Configure(position, rangeX, rangeY, Random.Range(0.45f, 0.85f));
            _live.Add(view);
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
