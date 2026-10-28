using UnityEngine;

namespace CardClash
{
    [CreateAssetMenu(fileName = "GameRules", menuName = "CardClash/Game Rules")]
    public class GameRulesSO : ScriptableObject
    {
        [Header("Players")]
        public int MinPlayers = 3;
        public int MaxPlayers = 7;
        public int DefaultPlayers = 4;

        [Header("Cards")]
        public int MinCards = 3;
        public int MaxCards = 7;
        public int DefaultStartingCards = 4;

        [Header("Timing")]
        public float TurnTimeLimit = 15f;
        public float GameTimeLimit = 300f;

        [Header("Last Card Call")]
        [Tooltip("A player must call LAST CARD when their hand count is at or below this value.")]
        public int LastCardCallThreshold = 2;

        [Tooltip("Cards drawn as a penalty for failing to call LAST CARD in time.")]
        public int MissedLastCardPenalty = 1;

        [Header("Notifications")]
        public float NotificationDisplayDuration = 2f;
    }
}