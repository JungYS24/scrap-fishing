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
                var along = Random.Range(0.15f, 1f);
                var y = Mathf.Lerp(surfaceY - 0.45f, targetY + 0.2f, along);
                SpawnOne(
                    ScrapCatalog.Pick(depth),
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
            go.transform.localScale = Vector3.one * Random.Range(0.28f, 0.42f);
            var view = go.AddComponent<ScrapView>();
            view.Bind(definition, spriteOverride);
            go.AddComponent<AmbientDrift>().Configure(position, rangeX, rangeY, Random.Range(0.45f, 0.85f));
            _live.Add(view);
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
