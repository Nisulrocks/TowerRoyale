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

        public int NodeIndex => _index;
        public Button ClaimButton => button;
        public bool IsClaimable => _milestone != null && PlayerProfile.GetTrophies() >= Mathf.Max(0, _milestone.trophyRequired) && !PlayerProfile.IsTrophyMilestoneClaimed(_index);

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
            // Read the balance either side of the grant so the reward animation knows how much to
            // fly in, and can hold the counter at the old value until the coins land.
            int before = PlayerProfile.GetSoftCurrency();

            var res = TrophyRoadService.Claim(_index);
            if (res.ok)
            {
                RefreshState();
                RewardClaimFX.Present(_milestone?.reward, before, PlayerProfile.GetSoftCurrency());
            }
            else
            {
                
                Debug.Log($"[TrophyRoadNode] Claim failed: {res.message}");
            }
        }
    }
}
