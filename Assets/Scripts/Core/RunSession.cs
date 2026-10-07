using System;
using System.Collections.Generic;
using ScrapFishing.Scrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScrapFishing.Core
{
    public class RunSession : MonoBehaviour
    {
        public const float Duration = 60f;
        const string BestKey = "BestCY";

        public float CastDepth { get; private set; }
        public float DeepestCast { get; private set; }
        public int CY { get; private set; }
        public int BestCY { get; private set; }
        public bool IsNewBest { get; private set; }
        public float Elapsed { get; private set; }
        public IReadOnlyList<ScrapDefinition> Caught => _caught;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public bool IsExpired => _running && Elapsed >= Duration;
        public bool IsRunning => _running;
        public bool HasDived { get; private set; }
        public bool CanDive => !HasDived && Remaining >= 8f;

        public event Action CaughtItem;

        readonly List<ScrapDefinition> _caught = new List<ScrapDefinition>();
        bool _running;

        void Start()
        {
            Time.timeScale = 0f;
        }

        public void StartRun()
        {
            CastDepth = 0f;
            DeepestCast = 0f;
            CY = 0;
            IsNewBest = false;
            Elapsed = 0f;
            HasDived = false;
            _running = true;
            _caught.Clear();
            Time.timeScale = 1f;
        }

        public void EndRun()
        {
            _running = false;
            BestCY = PlayerPrefs.GetInt(BestKey, 0);
            IsNewBest = CY > BestCY;
            if (IsNewBest)
            {
                BestCY = CY;
                PlayerPrefs.SetInt(BestKey, BestCY);
                PlayerPrefs.Save();
            }

            Time.timeScale = 0f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void RegisterCast(float depth)
        {
            CastDepth = Mathf.Clamp01(depth);
            if (CastDepth > DeepestCast)
            {
                DeepestCast = CastDepth;
            }
        }

        public void MarkDived()
        {
            HasDived = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_running)
            {
                return;
            }

            Elapsed = Mathf.Min(Duration, Elapsed + deltaTime);
        }

        public void AddCY(int amount)
        {
            CY = Mathf.Max(0, CY + amount);
        }

        public void AddCatch(ScrapDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _caught.Add(definition);
            AddCY(definition.Value);
            CaughtItem?.Invoke();
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
