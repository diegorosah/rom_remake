using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>Scene root for an imported map and its runtime-local warp definitions.</summary>
    public sealed class MapRuntimeRoot : MonoBehaviour
    {
        [SerializeField] private string mapId;
        [SerializeField] private GridCollisionMap collisionMap;
        [SerializeField] private MapCellOccupancy occupancy;
        [SerializeField] private List<MapRuntimeWarp> warps = new List<MapRuntimeWarp>();
        [SerializeField] private List<MapRuntimeConnection> connections = new List<MapRuntimeConnection>();
        [SerializeField] private List<NpcController> npcs = new List<NpcController>();
        [SerializeField] private NpcSimulationDriver npcSimulationDriver;
        [SerializeField] private bool isRuntimeActive = true;

        public string MapId => mapId;
        public string StableMapId => mapId;
        public GridCollisionMap CollisionMap => collisionMap;
        public MapCellOccupancy Occupancy => occupancy;
        public IReadOnlyList<MapRuntimeWarp> Warps => warps;
        public IReadOnlyList<MapRuntimeConnection> Connections => connections;
        public IReadOnlyList<NpcController> Npcs => npcs;
        public NpcSimulationDriver NpcSimulationDriver => npcSimulationDriver;
        public bool IsRuntimeActive => isRuntimeActive;

        public void Configure(string configuredMapId, GridCollisionMap configuredCollisionMap, IList<MapRuntimeWarp> configuredWarps)
        {
            Configure(configuredMapId, configuredCollisionMap, configuredWarps, null, null, null);
        }

        public void Configure(
            string configuredMapId,
            GridCollisionMap configuredCollisionMap,
            IList<MapRuntimeWarp> configuredWarps,
            MapCellOccupancy configuredOccupancy,
            IList<NpcController> configuredNpcs)
        {
            Configure(configuredMapId, configuredCollisionMap, configuredWarps, null, configuredOccupancy, configuredNpcs);
        }

        public void Configure(
            string configuredMapId,
            GridCollisionMap configuredCollisionMap,
            IList<MapRuntimeWarp> configuredWarps,
            IList<MapRuntimeConnection> configuredConnections,
            MapCellOccupancy configuredOccupancy,
            IList<NpcController> configuredNpcs)
        {
            if (string.IsNullOrWhiteSpace(configuredMapId))
            {
                throw new ArgumentException("Map ID is required.", nameof(configuredMapId));
            }

            if (configuredCollisionMap == null)
            {
                throw new ArgumentNullException(nameof(configuredCollisionMap));
            }

            mapId = configuredMapId;
            collisionMap = configuredCollisionMap;
            occupancy = configuredOccupancy;
            if (occupancy != null)
            {
                if (occupancy.CollisionMap == null)
                {
                    occupancy.Configure(collisionMap);
                }
                else if (occupancy.CollisionMap != collisionMap)
                {
                    throw new ArgumentException("Map occupancy must use this map's collision grid.", nameof(configuredOccupancy));
                }
            }
            warps = configuredWarps == null ? new List<MapRuntimeWarp>() : new List<MapRuntimeWarp>(configuredWarps);
            connections = configuredConnections == null ? new List<MapRuntimeConnection>() : new List<MapRuntimeConnection>(configuredConnections);
            npcs = configuredNpcs == null ? new List<NpcController>() : new List<NpcController>(configuredNpcs);
            ValidateWarps();
            ValidateConnections();
            ValidateNpcs();
        }

        public void SetRuntimeActive(bool active)
        {
            isRuntimeActive = active;
            if (occupancy != null)
            {
                occupancy.SetMapActive(active);
            }

            for (int index = 0; index < npcs.Count; index++)
            {
                if (npcs[index] != null)
                {
                    npcs[index].SetRuntimeActive(active);
                }
            }

            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }

        /// <summary>Registers the single fixed-tick NPC driver allowed for this map.</summary>
        public bool TryAttachNpcSimulationDriver(NpcSimulationDriver driver)
        {
            if (driver == null)
            {
                return false;
            }

            if (npcSimulationDriver != null && npcSimulationDriver != driver)
            {
                return false;
            }

            npcSimulationDriver = driver;
            return true;
        }

        public void DetachNpcSimulationDriver(NpcSimulationDriver driver)
        {
            if (npcSimulationDriver == driver)
            {
                npcSimulationDriver = null;
            }
        }

        public bool TryGetWarp(string warpId, out MapRuntimeWarp warp)
        {
            warp = null;
            if (string.IsNullOrWhiteSpace(warpId))
            {
                return false;
            }

            for (int index = 0; index < warps.Count; index++)
            {
                MapRuntimeWarp candidate = warps[index];
                if (candidate != null && string.Equals(candidate.WarpId, warpId, StringComparison.Ordinal))
                {
                    warp = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetActivatedWarp(Vector2Int playerCell, GridDirection direction, out MapRuntimeWarp warp)
        {
            warp = null;
            if (!isRuntimeActive)
            {
                return false;
            }

            for (int index = 0; index < warps.Count; index++)
            {
                MapRuntimeWarp candidate = warps[index];
                if (candidate != null && candidate.MatchesMovement(playerCell, direction))
                {
                    warp = candidate;
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            // Generated maps can be inactive in the scene. Do not call SetActive here
            // because Awake is also executed while the editor serializes the scene.
            isRuntimeActive = gameObject.activeSelf;
            if (occupancy == null)
            {
                occupancy = GetComponentInChildren<MapCellOccupancy>(true);
            }

            if (npcs == null || npcs.Count == 0)
            {
                npcs = new List<NpcController>(GetComponentsInChildren<NpcController>(true));
            }

            if (npcSimulationDriver == null)
            {
                npcSimulationDriver = GetComponent<NpcSimulationDriver>();
            }

            if (occupancy != null)
            {
                occupancy.SetMapActive(isRuntimeActive);
            }
        }

        private void OnEnable()
        {
            if (occupancy != null)
            {
                occupancy.SetMapActive(isRuntimeActive);
            }
        }

        private void OnDisable()
        {
            if (occupancy != null)
            {
                occupancy.SetMapActive(false);
            }
        }

        private void OnValidate()
        {
            if (warps == null)
            {
                warps = new List<MapRuntimeWarp>();
            }

            if (connections == null)
            {
                connections = new List<MapRuntimeConnection>();
            }

            if (npcs == null)
            {
                npcs = new List<NpcController>();
            }
        }

        private void ValidateWarps()
        {
            if (collisionMap == null)
            {
                throw new ArgumentException("A collision map is required before warps can be validated.", nameof(collisionMap));
            }

            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < warps.Count; index++)
            {
                MapRuntimeWarp warp = warps[index];
                if (warp == null || !warp.HasValidIdentity() || !knownIds.Add(warp.WarpId) ||
                    !collisionMap.IsInBounds(warp.ActivationCell) || !collisionMap.IsInBounds(warp.ArrivalCell))
                {
                    throw new ArgumentException(
                        "Each map warp must be non-null, valid, in bounds, and have a unique ID.",
                        nameof(warps));
                }
            }
        }

        private void ValidateConnections()
        {
            if (connections == null)
            {
                connections = new List<MapRuntimeConnection>();
                return;
            }

            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < connections.Count; index++)
            {
                MapRuntimeConnection connection = connections[index];
                if (connection == null || string.IsNullOrWhiteSpace(connection.DestinationMapId))
                {
                    throw new ArgumentException("Map connections must be non-null and have destination ids.", nameof(connections));
                }

                var key = ((int)connection.Direction).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + connection.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + connection.DestinationMapId;
                if (!known.Add(key))
                {
                    throw new ArgumentException("Duplicate runtime map connections are not allowed.", nameof(connections));
                }
            }
        }

        private void ValidateNpcs()
        {
            var knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < npcs.Count; index++)
            {
                NpcController npc = npcs[index];
                if (npc == null || string.IsNullOrWhiteSpace(npc.NpcId) || !knownIds.Add(npc.NpcId))
                {
                    throw new ArgumentException("Map NPCs must be non-null and have unique stable IDs.", nameof(npcs));
                }
            }
        }
    }
}
