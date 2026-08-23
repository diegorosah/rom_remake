using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// Follows a target while keeping an orthographic camera inside map bounds and
    /// snapping the result to the 1/16-unit pixel grid used by imported tiles.
    /// </summary>
    public sealed class PixelPerfectCameraFollow : MonoBehaviour
    {
        public const float PositionQuantum = 1f / 16f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform target;
        [SerializeField] private Rect mapBounds = new Rect(0f, 0f, 1f, 1f);

        public Camera TargetCamera => targetCamera;
        public Transform Target => target;
        public Rect MapBounds => mapBounds;

        public void Configure(Camera configuredCamera, Transform configuredTarget, Rect configuredMapBounds)
        {
            if (configuredCamera == null)
            {
                throw new ArgumentNullException(nameof(configuredCamera));
            }

            if (!configuredCamera.orthographic)
            {
                throw new ArgumentException("Pixel-perfect follow requires an orthographic camera.", nameof(configuredCamera));
            }

            if (configuredTarget == null)
            {
                throw new ArgumentNullException(nameof(configuredTarget));
            }

            if (configuredMapBounds.width < 0f || configuredMapBounds.height < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(configuredMapBounds));
            }

            targetCamera = configuredCamera;
            target = configuredTarget;
            mapBounds = configuredMapBounds;
        }

        public void ConfigureForMap(Camera configuredCamera, Transform configuredTarget, GridCollisionMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            Configure(configuredCamera, configuredTarget, map.WorldBounds);
        }

        public Vector3 CalculateFollowPosition(Vector3 targetPosition)
        {
            if (targetCamera == null)
            {
                throw new InvalidOperationException("A camera must be configured before calculating follow position.");
            }

            if (!targetCamera.orthographic)
            {
                throw new InvalidOperationException("Pixel-perfect follow requires an orthographic camera.");
            }

            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            float x = ClampToViewport(targetPosition.x, mapBounds.xMin, mapBounds.xMax, halfWidth);
            float y = ClampToViewport(targetPosition.y, mapBounds.yMin, mapBounds.yMax, halfHeight);
            x = Quantize(x);
            y = Quantize(y);

            // Quantization can otherwise move a camera a fraction outside a narrow map.
            x = ClampToViewport(x, mapBounds.xMin, mapBounds.xMax, halfWidth);
            y = ClampToViewport(y, mapBounds.yMin, mapBounds.yMax, halfHeight);
            return new Vector3(x, y, targetCamera.transform.position.z);
        }

        public void ApplyFollow()
        {
            if (target != null && targetCamera != null)
            {
                targetCamera.transform.position = CalculateFollowPosition(target.position);
            }
        }

        private void LateUpdate()
        {
            ApplyFollow();
        }

        private static float ClampToViewport(float value, float min, float max, float halfExtent)
        {
            float viewportSize = halfExtent * 2f;
            float boundsSize = max - min;
            if (boundsSize <= viewportSize)
            {
                return (min + max) * 0.5f;
            }

            return Mathf.Clamp(value, min + halfExtent, max - halfExtent);
        }

        private static float Quantize(float value)
        {
            return Mathf.Round(value / PositionQuantum) * PositionQuantum;
        }
    }
}
