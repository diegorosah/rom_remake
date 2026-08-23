using System;
using System.Collections;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// Runtime-only orchestrator for transitions between already-loaded map roots.
    /// It resolves stable IDs through <see cref="RuntimeMapCatalog"/>, never reads a
    /// ROM, and can be driven synchronously by tests through <see cref="TryTransitionImmediately"/>.
    /// </summary>
    public sealed class MapTransitionSystem : MonoBehaviour, IGridMoveInterceptor
    {
        [SerializeField] private RuntimeMapCatalog mapCatalog;
        [SerializeField] private PlayerController player;
        [SerializeField] private PixelPerfectCameraFollow cameraFollow;
        [SerializeField] private MapRuntimeRoot activeMap;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField, Min(0f)] private float fadeDuration;

        private bool isTransitioning;
        private bool suppressArrivalWarp;
        private string suppressedMapId;
        private string suppressedWarpId;
        private string lastFailure;

        public RuntimeMapCatalog MapCatalog => mapCatalog;
        public PlayerController Player => player;
        public PixelPerfectCameraFollow CameraFollow => cameraFollow;
        public MapRuntimeRoot ActiveMap => activeMap;
        public bool IsTransitioning => isTransitioning;
        public string LastFailure => lastFailure;

        public void Configure(
            RuntimeMapCatalog configuredCatalog,
            PlayerController configuredPlayer,
            PixelPerfectCameraFollow configuredCameraFollow,
            MapRuntimeRoot configuredActiveMap)
        {
            if (configuredCatalog == null)
            {
                throw new ArgumentNullException(nameof(configuredCatalog));
            }

            if (configuredPlayer == null)
            {
                throw new ArgumentNullException(nameof(configuredPlayer));
            }

            if (configuredActiveMap == null || configuredActiveMap.CollisionMap == null)
            {
                throw new ArgumentException("An active map with collision data is required.", nameof(configuredActiveMap));
            }

            if (!configuredCatalog.TryResolve(configuredActiveMap.MapId, out MapRuntimeRoot resolvedMap) || resolvedMap != configuredActiveMap)
            {
                throw new ArgumentException("The active map must be registered in the runtime catalog.", nameof(configuredActiveMap));
            }

            UnsubscribeFromPlayer();
            mapCatalog = configuredCatalog;
            player = configuredPlayer;
            cameraFollow = configuredCameraFollow;
            activeMap = configuredActiveMap;
            SubscribeToPlayer();
            player.SetMoveInterceptors(this);
            EnsureOnlyActiveMapIsEnabled();
            RebindCamera();
        }

        public void ConfigureFade(CanvasGroup configuredFadeCanvasGroup, float configuredFadeDuration)
        {
            if (configuredFadeDuration < 0f || float.IsNaN(configuredFadeDuration) || float.IsInfinity(configuredFadeDuration))
            {
                throw new ArgumentOutOfRangeException(nameof(configuredFadeDuration));
            }

            fadeCanvasGroup = configuredFadeCanvasGroup;
            fadeDuration = configuredFadeDuration;
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 0f;
            }
        }

        /// <summary>Editor/debug entry point for a selected, already-loaded map without inventing a ROM warp.</summary>
        public bool TryActivateMapImmediately(
            string destinationMapId,
            Vector2Int destinationCell,
            byte destinationElevation,
            GridDirection destinationFacing)
        {
            if (isTransitioning || mapCatalog == null || player == null ||
                !mapCatalog.TryResolve(destinationMapId, out MapRuntimeRoot destination) ||
                destination.CollisionMap == null || !destination.CollisionMap.IsInBounds(destinationCell) ||
                destination.CollisionMap.GetCollision(destinationCell) != 0 ||
                !GridDirections.IsCardinal(destinationFacing))
            {
                return false;
            }

            player.CancelPendingMove();
            if (activeMap != null && activeMap != destination) activeMap.SetRuntimeActive(false);
            destination.SetRuntimeActive(true);
            activeMap = destination;
            suppressArrivalWarp = false;
            suppressedMapId = null;
            suppressedWarpId = null;
            player.PlaceAfterTransition(
                destination.CollisionMap,
                destinationCell,
                destinationElevation,
                destinationFacing,
                destination.Occupancy);
            RebindCamera();
            lastFailure = null;
            return true;
        }

        /// <summary>
        /// Intercepts a matching map-local warp before the player asks normal grid
        /// collision. A matching but malformed warp is also consumed, so a bad map
        /// link cannot let the player walk through an intended boundary.
        /// </summary>
        public bool TryInterceptMove(PlayerController movingPlayer, GridDirection direction)
        {
            if (movingPlayer == null || movingPlayer != player || isTransitioning || activeMap == null)
            {
                return false;
            }

            if (!activeMap.TryGetActivatedWarp(movingPlayer.CurrentCell, direction, out MapRuntimeWarp sourceWarp))
            {
                return false;
            }

            if (IsArrivalWarpSuppressed(activeMap, sourceWarp))
            {
                // Suppression disables the warp, not the movement request. Let the
                // ordinary collision path move the player away and clear suppression.
                lastFailure = null;
                return false;
            }

            if (!TryPrepareTransition(sourceWarp, out TransitionRequest request))
            {
                return true;
            }

            if (fadeCanvasGroup == null || fadeDuration <= 0f)
            {
                ApplyTransition(request);
            }
            else
            {
                StartCoroutine(FadeAndApplyTransition(request));
            }

            return true;
        }

        /// <summary>Synchronously follows one configured warp. Intended for deterministic tests and no-fade flows.</summary>
        public bool TryTransitionImmediately(MapRuntimeWarp sourceWarp)
        {
            if (isTransitioning || !TryPrepareTransition(sourceWarp, out TransitionRequest request))
            {
                return false;
            }

            ApplyTransition(request);
            return true;
        }

        public bool TryResolveDestination(MapRuntimeWarp sourceWarp, out MapRuntimeRoot destinationMap, out MapRuntimeWarp destinationWarp)
        {
            destinationMap = null;
            destinationWarp = null;
            lastFailure = null;

            if (sourceWarp == null || !sourceWarp.HasValidIdentity())
            {
                lastFailure = "The source warp is missing required identity or direction data.";
                return false;
            }

            if (mapCatalog == null || !mapCatalog.TryResolve(sourceWarp.DestinationMapId, out destinationMap))
            {
                lastFailure = "The warp destination map is not registered in the runtime catalog.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceWarp.DestinationWarpId) ||
                !destinationMap.TryGetWarp(sourceWarp.DestinationWarpId, out destinationWarp))
            {
                lastFailure = "The warp destination is missing its target warp.";
                destinationMap = null;
                return false;
            }

            if (destinationMap.CollisionMap == null || !destinationMap.CollisionMap.IsInBounds(destinationWarp.ArrivalCell))
            {
                lastFailure = "The target warp arrival cell is outside the destination collision map.";
                destinationMap = null;
                destinationWarp = null;
                return false;
            }

            return true;
        }

        private void Awake()
        {
            if (mapCatalog != null && player != null && activeMap != null)
            {
                Configure(mapCatalog, player, cameraFollow, activeMap);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromPlayer();
        }

        private bool TryPrepareTransition(MapRuntimeWarp sourceWarp, out TransitionRequest request)
        {
            request = default(TransitionRequest);
            lastFailure = null;
            if (sourceWarp == null)
            {
                lastFailure = "A source warp is required.";
                return false;
            }

            if (!TryResolveDestination(sourceWarp, out MapRuntimeRoot destinationMap, out MapRuntimeWarp destinationWarp))
            {
                return false;
            }

            request = new TransitionRequest(destinationMap, destinationWarp);
            return true;
        }

        private void ApplyTransition(TransitionRequest request)
        {
            bool wasTransitioning = isTransitioning;
            isTransitioning = true;
            try
            {
                // A programmatic transition can happen while an actor is interpolating.
                // Release the old reservation before either map changes active state.
                player.CancelPendingMove();
                if (activeMap != null && activeMap != request.DestinationMap)
                {
                    activeMap.SetRuntimeActive(false);
                }

                request.DestinationMap.SetRuntimeActive(true);
                activeMap = request.DestinationMap;
                byte arrivalElevation = request.DestinationWarp.ArrivalElevation == 0
                    ? player.Elevation
                    : request.DestinationWarp.ArrivalElevation;
                player.PlaceAfterTransition(
                    activeMap.CollisionMap,
                    request.DestinationWarp.ArrivalCell,
                    arrivalElevation,
                    request.DestinationWarp.ArrivalFacing,
                    activeMap.Occupancy);
                RebindCamera();

                suppressArrivalWarp = true;
                suppressedMapId = activeMap.MapId;
                suppressedWarpId = request.DestinationWarp.WarpId;
                lastFailure = null;
            }
            finally
            {
                isTransitioning = wasTransitioning;
            }
        }

        private IEnumerator FadeAndApplyTransition(TransitionRequest request)
        {
            isTransitioning = true;
            yield return FadeTo(1f);
            ApplyTransition(request);
            yield return FadeTo(0f);
            isTransitioning = false;
        }

        private IEnumerator FadeTo(float targetAlpha)
        {
            float startAlpha = fadeCanvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            fadeCanvasGroup.alpha = targetAlpha;
        }

        private void OnPlayerMovementCompleted(PlayerController movingPlayer)
        {
            if (movingPlayer == player && suppressArrivalWarp)
            {
                // The first fully completed ordinary step is the only event that can
                // clear suppression. A direct arrival-warp retry is consumed above.
                suppressArrivalWarp = false;
                suppressedMapId = null;
                suppressedWarpId = null;
            }
        }

        private bool IsArrivalWarpSuppressed(MapRuntimeRoot map, MapRuntimeWarp warp)
        {
            return suppressArrivalWarp && map != null && warp != null &&
                   string.Equals(map.MapId, suppressedMapId, StringComparison.Ordinal) &&
                   string.Equals(warp.WarpId, suppressedWarpId, StringComparison.Ordinal);
        }

        private void SubscribeToPlayer()
        {
            if (player != null)
            {
                player.MovementCompleted += OnPlayerMovementCompleted;
            }
        }

        private void UnsubscribeFromPlayer()
        {
            if (player != null)
            {
                player.MovementCompleted -= OnPlayerMovementCompleted;
            }
        }

        private void EnsureOnlyActiveMapIsEnabled()
        {
            foreach (MapRuntimeRoot map in mapCatalog.Maps)
            {
                if (map != null)
                {
                    map.SetRuntimeActive(map == activeMap);
                }
            }
        }

        private void RebindCamera()
        {
            if (cameraFollow != null && player != null && activeMap != null)
            {
                Camera configuredCamera = cameraFollow.TargetCamera;
                if (configuredCamera != null)
                {
                    cameraFollow.ConfigureForMap(configuredCamera, player.transform, activeMap.CollisionMap);
                    cameraFollow.ApplyFollow();
                }
            }
        }

        private readonly struct TransitionRequest
        {
            public TransitionRequest(MapRuntimeRoot destinationMap, MapRuntimeWarp destinationWarp)
            {
                DestinationMap = destinationMap;
                DestinationWarp = destinationWarp;
            }

            public MapRuntimeRoot DestinationMap { get; }
            public MapRuntimeWarp DestinationWarp { get; }
        }
    }
}
