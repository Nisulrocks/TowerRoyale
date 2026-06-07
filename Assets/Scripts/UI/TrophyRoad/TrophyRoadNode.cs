using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data.Progression;

namespace TR.UI.TrophyRoad
{
    public class TrophyRoadNode : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text thresholdText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private GameObject claimedBadge;
        [SerializeField] private GameObject lockedOverlay;

        private int _index;
        private TrophyMilestone _milestone;

        public void SetData(int index, TrophyMilestone milestone)
        {
            _index = index;
            _milestone = milestone;
            if (thresholdText) thresholdText.text = milestone != null ? milestone.trophyRequired.ToString() : "-";
            if (rewardText) rewardText.text = milestone?.reward != null ? milestone.reward.GetDisplayName() : "-";
            if (rewardIcon) rewardIcon.sprite = milestone?.reward != null ? milestone.reward.GetIcon() : null;
            if (claimedBadge) claimedBadge.SetActive(false); 
            if (lockedOverlay) lockedOverlay.SetActive(true); 
            RefreshState();
            if (button)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }

        public void RefreshState()
        {
            bool claimed = PlayerProfile.IsTrophyMilestoneClaimed(_index);
            int trophies = PlayerProfile.GetTrophies();
            bool claimable = _milestone != null && trophies >= Mathf.Max(0, _milestone.trophyRequired) && !claimed;

            if (claimedBadge) claimedBadge.SetActive(claimed);
            if (lockedOverlay) lockedOverlay.SetActive(!claimable && !claimed);

            if (button) button.interactable = claimable;
        }

        private void OnClick()
        {
            
            var res = TrophyRoadService.Claim(_index);
            if (res.ok)
            {
                RefreshState();
            }
            else
            {
                
                Debug.Log($"[TrophyRoadNode] Claim failed: {res.message}");
            }
        }
    }
}
