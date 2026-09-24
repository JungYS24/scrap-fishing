using System.Collections.Generic;
using ScrapFishing.Scrap;
using UnityEngine;

namespace ScrapFishing.Core
{
    public class RunSession : MonoBehaviour
    {
        public const float Duration = 60f;

        public float CastDepth { get; set; }
        public int TotalValue { get; private set; }
        public float Elapsed { get; private set; }
        public IReadOnlyList<ScrapDefinition> Caught => _caught;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public bool IsExpired => _running && Elapsed >= Duration;
        public bool IsRunning => _running;

        readonly List<ScrapDefinition> _caught = new List<ScrapDefinition>();
        bool _running;

        public void ResetRun()
        {
            CastDepth = 0f;
            TotalValue = 0;
            Elapsed = 0f;
            _running = true;
            _caught.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (!_running)
            {
                return;
            }

            Elapsed = Mathf.Min(Duration, Elapsed + deltaTime);
        }

        public void Stop()
        {
            _running = false;
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
