using System.Collections.Generic;
using ScrapFishing.Core;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class ScrapSpawner : MonoBehaviour
    {
        readonly List<ScrapView> _live = new List<ScrapView>();
        ScrapDefinition[] _catalog;

        public IReadOnlyList<ScrapView> Live => _live;

        void EnsureCatalog()
        {
            if (_catalog != null)
            {
                return;
            }

            _catalog = new[]
            {
                ScrapDefinition.CreateRuntime("녹슨 회로", ScrapGrade.Junk, 10, new Color(0.55f, 0.55f, 0.5f)),
                ScrapDefinition.CreateRuntime("폐배터리", ScrapGrade.Common, 25, Palette.Cyan),
                ScrapDefinition.CreateRuntime("네온 기판", ScrapGrade.Rare, 60, Palette.Magenta),
                ScrapDefinition.CreateRuntime("변이 어류", ScrapGrade.Mutant, 90, Palette.NeonGreen)
            };
        }

        public void SpawnForCast(float surfaceY, float targetY)
        {
            EnsureCatalog();
            Clear();
            var count = Random.Range(5, 9);
            for (var i = 0; i < count; i++)
            {
                var definition = _catalog[WeightedIndex()];
                var go = new GameObject(definition.DisplayName);
                go.transform.SetParent(transform, false);
                var y = Random.Range(Mathf.Min(surfaceY - 0.4f, targetY + 0.3f), Mathf.Max(surfaceY - 0.4f, targetY + 0.3f));
                go.transform.position = new Vector3(Random.Range(-1.35f, 1.35f), y, 0f);
                go.transform.localScale = Vector3.one * Random.Range(0.28f, 0.42f);
                var view = go.AddComponent<ScrapView>();
                view.Bind(definition);
                _live.Add(view);
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

        int WeightedIndex()
        {
            var roll = Random.value;
            if (roll < 0.4f) return 0;
            if (roll < 0.7f) return 1;
            if (roll < 0.9f) return 2;
            return 3;
        }
    }
}
