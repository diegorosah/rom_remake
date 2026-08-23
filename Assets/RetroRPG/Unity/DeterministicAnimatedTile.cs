using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RetroRPG.Unity
{
    /// <summary>
    /// Animated tile with a fixed playback rate and synchronized start time.
    /// Frame repetition is used by the importer for non-uniform durations.
    /// </summary>
    [Serializable]
    public sealed class DeterministicAnimatedTile : TileBase
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] private float framesPerSecond = 1f;

        public int FrameCount => frames?.Length ?? 0;
        public float FramesPerSecond => framesPerSecond;

        public float AnimationStartTime => 0f;

        public Sprite GetFrame(int index)
        {
            if (frames == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return frames[index];
        }

        public void Configure(Sprite[] animationFrames, float playbackFramesPerSecond)
        {
            if (animationFrames == null)
            {
                throw new ArgumentNullException(nameof(animationFrames));
            }

            if (animationFrames.Length == 0)
            {
                throw new ArgumentException("At least one animation frame is required.", nameof(animationFrames));
            }

            if (playbackFramesPerSecond <= 0f || float.IsNaN(playbackFramesPerSecond) || float.IsInfinity(playbackFramesPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(playbackFramesPerSecond));
            }

            frames = (Sprite[])animationFrames.Clone();
            framesPerSecond = playbackFramesPerSecond;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.transform = Matrix4x4.identity;
            tileData.color = Color.white;
            tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tileData.colliderType = Tile.ColliderType.None;
            tileData.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        }

        public override bool GetTileAnimationData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileAnimationData tileAnimationData)
        {
            if (frames == null || frames.Length < 2 || framesPerSecond <= 0f)
            {
                return false;
            }

            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = framesPerSecond;
            // A fixed origin makes every instance of this asset advance in lockstep.
            tileAnimationData.animationStartTime = AnimationStartTime;
            return true;
        }
    }
}
