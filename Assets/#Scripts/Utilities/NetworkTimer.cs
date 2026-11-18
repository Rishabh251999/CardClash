using Mirror;
using UnityEngine;

namespace CardClash
{
    public readonly struct NetworkTimer
    {
        private readonly double _startTime;
        private readonly float _duration;

        public NetworkTimer(double startTime, float duration)
        {
            _startTime = startTime;
            _duration = duration;
        }

        public float ElapsedTime => (float)(NetworkTime.time - _startTime);

        public float RemainingTime => _duration - ElapsedTime;

        public float Normalized => _duration > 0f ? Mathf.Clamp01(1f - ElapsedTime / _duration) : 0f;

        public bool HasExpired => RemainingTime <= 0f;
    }
}