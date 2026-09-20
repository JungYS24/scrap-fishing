using System;
using UnityEngine;

namespace ScrapFishing.Core
{
    public enum GamePhase
    {
        Title,
        Aiming,
        Casting,
        Reeling,
        CastComplete
    }

    public class GameFlow : MonoBehaviour
    {
        public GamePhase Phase { get; private set; } = GamePhase.Title;
        public event Action<GamePhase> PhaseChanged;

        public void SetPhase(GamePhase next)
        {
            if (Phase == next)
            {
                return;
            }

            Phase = next;
            PhaseChanged?.Invoke(next);
        }

        public void StartRun()
        {
            SetPhase(GamePhase.Aiming);
        }

        public void BeginCasting()
        {
            SetPhase(GamePhase.Casting);
        }

        public void BeginReeling()
        {
            SetPhase(GamePhase.Reeling);
        }

        public void CompleteCast()
        {
            SetPhase(GamePhase.CastComplete);
        }

        public void ReturnToAiming()
        {
            SetPhase(GamePhase.Aiming);
        }
    }
}
