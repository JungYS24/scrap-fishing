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
        public const int CastsPerDive = 3;
        public const float MinDiveSeconds = 8f;
        const string BestKey = "BestCY";

        public float CastDepth { get; private set; }
        public float DeepestCast { get; private set; }
        public int CY { get; private set; }
        public int BestCY { get; private set; }
        public bool IsNewBest { get; private set; }
        public int LastGain { get; private set; }
        public int EarnedDollars { get; private set; }
        public float Elapsed { get; private set; }
        public IReadOnlyList<ScrapDefinition> Caught => _caught;
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public bool IsExpired => _running && Elapsed >= Duration;
        public bool IsRunning => _running;
        public int DiveProgress { get; private set; }
        public bool HasDiveCharge => DiveProgress >= CastsPerDive;
        public int CastsUntilDive => CastsPerDive - DiveProgress;
        public bool HasDiveTime => Remaining >= MinDiveSeconds;
        public bool CanDive => HasDiveCharge && HasDiveTime;

        public event Action CaughtItem;
        public event Action<ScrapDefinition, Vector3, int> CaughtAt;
        public event Action DiveCharged;

        readonly List<ScrapDefinition> _caught = new List<ScrapDefinition>();
        bool _running;
        float _chipMultiplier = 1f;

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
            LastGain = 0;
            EarnedDollars = 0;
            Elapsed = 0f;
            DiveProgress = 0;
            _running = true;
            _chipMultiplier = Progression.ChipMultiplier;
            _caught.Clear();
            Time.timeScale = 1f;
        }

        public void EndRun()
        {
            _running = false;
            EarnedDollars = CY;
            Progression.AddDollars(EarnedDollars);
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

        public void RegisterCastComplete()
        {
            if (HasDiveCharge)
            {
                return;
            }

            DiveProgress++;
            if (HasDiveCharge)
            {
                DiveCharged?.Invoke();
            }
        }

        public void MarkDived()
        {
            DiveProgress = 0;
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
            AddCatch(definition, Vector3.zero);
        }

        public void AddCatch(ScrapDefinition definition, Vector3 worldPos)
        {
            if (definition == null)
            {
                return;
            }

            _caught.Add(definition);
            LastGain = Mathf.RoundToInt(definition.Value * _chipMultiplier);
            AddCY(LastGain);
            CaughtItem?.Invoke();
            CaughtAt?.Invoke(definition, worldPos, LastGain);
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
