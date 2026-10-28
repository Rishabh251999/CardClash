using UnityEngine;

namespace CardClash
{
    [CreateAssetMenu(fileName = "DeckRules", menuName = "CardClash/Deck Rules")]
    public class DeckRulesSO : ScriptableObject
    {
        [Header("Number Cards (per color)")]
        public int ZeroCardCount = 1;
        public int NumberCardDuplicates = 2; 

        [Header("Action Cards (per color)")]
        public int SkipCount = 2;
        public int ReverseCount = 2;
        public int DrawTwoCount = 2;

        [Header("Wild Cards (total, all colors)")]
        public int WildCount = 4;
        public int WildDrawFourCount = 4;
    }
}