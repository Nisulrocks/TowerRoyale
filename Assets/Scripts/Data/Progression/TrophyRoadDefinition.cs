using System.Collections.Generic;
using UnityEngine;

namespace TR.Data.Progression
{
    [CreateAssetMenu(fileName = "TrophyRoadDefinition", menuName = "TR/Data/Trophy Road Definition")]
    public class TrophyRoadDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string roadId = "main";

        [Header("Settings")]

        [Min(0)] [SerializeField] private int maxTrophies = 4000;

        [SerializeField] private bool retroactiveClaim = true;

        [Header("Milestones (ascending by trophyRequired)")]
        [SerializeField] private List<TrophyMilestone> milestones = new();

        public string RoadId => roadId;
        public int MaxTrophies => Mathf.Max(0, maxTrophies);
        public bool RetroactiveClaim => retroactiveClaim;
        public IReadOnlyList<TrophyMilestone> Milestones => milestones;
    }
}
