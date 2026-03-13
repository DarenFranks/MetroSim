// =============================================================================
// SimulationEngine.cs  –  Drives the simulation at configurable tick rates.
// Supports Pause / 1× / 2× / 3× speed.
// =============================================================================
using UnityEngine;

namespace MetroSim
{
    public enum SimSpeed { Paused, Normal, Fast, UltraFast }

    public class SimulationEngine : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────
        public SimSpeed Speed       { get; private set; } = SimSpeed.Paused;
        public bool     IsPaused    => Speed == SimSpeed.Paused;
        public int      TickCount   { get; private set; } = 0;

        // Countdown to next tick (seconds)
        private float _tickTimer = 0f;

        // ── Speed control ─────────────────────────────────────────────────────

        public void SetSpeed(SimSpeed speed)
        {
            Speed      = speed;
            _tickTimer = 0f;   // fire immediately on unpause
        }

        public void Pause()       => SetSpeed(SimSpeed.Paused);
        public void SetNormal()   => SetSpeed(SimSpeed.Normal);
        public void SetFast()     => SetSpeed(SimSpeed.Fast);
        public void SetUltraFast()=> SetSpeed(SimSpeed.UltraFast);

        private float TickInterval => Speed switch
        {
            SimSpeed.Normal    => Config.TICK_INTERVAL_1X,
            SimSpeed.Fast      => Config.TICK_INTERVAL_2X,
            SimSpeed.UltraFast => Config.TICK_INTERVAL_3X,
            _                  => float.MaxValue
        };

        // ── Unity update ──────────────────────────────────────────────────────

        private void Update()
        {
            if (IsPaused || GameManager.Instance == null) return;

            _tickTimer -= Time.deltaTime;
            if (_tickTimer <= 0f)
            {
                _tickTimer = TickInterval;
                Tick();
            }
        }

        private void Tick()
        {
            TickCount++;
            GameManager.Instance.OnTick();
        }
    }
}
