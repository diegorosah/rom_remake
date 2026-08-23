using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// Drives one active map's NPCs at a fixed 60 Hz. NPCs deliberately have no
    /// autonomous Update loop, so this is the sole normal Play-mode scheduler and
    /// an explicit <see cref="Advance"/> method remains available for deterministic tests.
    /// </summary>
    public sealed class NpcSimulationDriver : MonoBehaviour, INpcTickSource
    {
        public const int TickRate = DirectionalSpriteAnimator.TickRate;
        public const float TickDuration = 1f / TickRate;

        [SerializeField] private MapRuntimeRoot mapRoot;
        [SerializeField, Min(1)] private int maximumCatchUpTicks = 600;

        private float accumulatedSeconds;
        private int currentTick;
        private bool isPrimaryDriver;
        private bool isSuspended;

        public MapRuntimeRoot MapRoot => mapRoot;
        public int CurrentTick => currentTick;
        public bool IsPrimaryDriver => isPrimaryDriver;
        public bool IsSuspended => isSuspended;

        public void Configure(MapRuntimeRoot configuredMapRoot)
        {
            if (configuredMapRoot == null)
            {
                throw new ArgumentNullException(nameof(configuredMapRoot));
            }

            if (mapRoot != null && mapRoot != configuredMapRoot)
            {
                mapRoot.DetachNpcSimulationDriver(this);
            }

            mapRoot = configuredMapRoot;
            isPrimaryDriver = mapRoot.TryAttachNpcSimulationDriver(this);
            if (!isPrimaryDriver)
            {
                throw new InvalidOperationException("Only one NPC simulation driver may control a map root.");
            }
        }

        public void SetSuspended(bool suspended)
        {
            isSuspended = suspended;
            if (suspended)
            {
                accumulatedSeconds = 0f;
            }
        }

        /// <summary>Advances the fixed clock by supplied elapsed time without relying on Unity frame timing.</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !isPrimaryDriver || isSuspended || mapRoot == null || !mapRoot.IsRuntimeActive)
            {
                if (mapRoot == null || !mapRoot.IsRuntimeActive)
                {
                    accumulatedSeconds = 0f;
                }

                return;
            }

            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            accumulatedSeconds += deltaSeconds;
            int ticksToRun = Mathf.FloorToInt(accumulatedSeconds * TickRate);
            if (ticksToRun <= 0)
            {
                return;
            }

            ticksToRun = Mathf.Min(ticksToRun, Mathf.Max(1, maximumCatchUpTicks));
            accumulatedSeconds -= ticksToRun * TickDuration;
            for (int tick = 0; tick < ticksToRun; tick++)
            {
                currentTick = checked(currentTick + 1);
                TickActiveMap(currentTick);
            }
        }

        public void TickOnce()
        {
            if (!isPrimaryDriver || isSuspended || mapRoot == null || !mapRoot.IsRuntimeActive)
            {
                return;
            }

            currentTick = checked(currentTick + 1);
            TickActiveMap(currentTick);
        }

        private void Awake()
        {
            if (mapRoot == null)
            {
                mapRoot = GetComponent<MapRuntimeRoot>();
            }

            if (mapRoot != null)
            {
                isPrimaryDriver = mapRoot.TryAttachNpcSimulationDriver(this);
                if (!isPrimaryDriver)
                {
                    enabled = false;
                }
            }
        }

        private void OnDestroy()
        {
            if (mapRoot != null)
            {
                mapRoot.DetachNpcSimulationDriver(this);
            }
        }

        private void OnDisable()
        {
            // Do not burst through elapsed wall-clock time after a map is reactivated.
            accumulatedSeconds = 0f;
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private void TickActiveMap(int tick)
        {
            var npcs = mapRoot.Npcs;
            for (int index = 0; index < npcs.Count; index++)
            {
                NpcController npc = npcs[index];
                if (npc != null && npc.IsMapActive)
                {
                    npc.Tick(tick);
                }
            }
        }
    }
}
