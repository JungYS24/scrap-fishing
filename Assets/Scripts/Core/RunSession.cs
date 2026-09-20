using System.Collections.Generic;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Core
{
    public class RunSession : MonoBehaviour
    {
        public float CastDepth { get; set; }
        public int TotalValue { get; private set; }
        public IReadOnlyList<ScrapDefinition> Caught => _caught;

        readonly List<ScrapDefinition> _caught = new List<ScrapDefinition>();

        public void ResetRun()
        {
            CastDepth = 0f;
            TotalValue = 0;
            _caught.Clear();
        }

        public void AddCatch(ScrapDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _caught.Add(definition);
            TotalValue += definition.Value;
        }

        public ScrapDefinition HighestGrade()
        {
            ScrapDefinition best = null;
            foreach (var scrap in _caught)
            {
                if (best == null || scrap.Grade > best.Grade)
                {
                    best = scrap;
                }
            }

            return best;
        }
    }
}
