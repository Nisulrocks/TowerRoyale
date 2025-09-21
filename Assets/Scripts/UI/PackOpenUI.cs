using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;
using TR.Data;

namespace TR.UI
{
    public class PackOpenUI : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string packId = "normal_pack";

        [Header("UI Refs")]
        [SerializeField] private TMP_Text packNameText;
        [SerializeField] private TMP_Text packCountText;
        [SerializeField] private TMP_Text resultsText;
        [SerializeField] private Button openOneButton;
        [SerializeField] private Button openAllButton;

        private void Awake()
        {
            GameDB.EnsureLoaded();
            if (openOneButton) openOneButton.onClick.AddListener(OpenOne);
            if (openAllButton) openAllButton.onClick.AddListener(OpenAll);
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            var pack = GameDB.GetPackById(packId);
            if (packNameText) packNameText.text = pack != null ? pack.DisplayName : packId + " (not found)";
            int count = PlayerProfile.Data.GetPackCount(packId);
            if (packCountText) packCountText.text = $"x{count}";

            bool hasPack = count > 0 && pack != null;
            if (openOneButton) openOneButton.interactable = hasPack;
            if (openAllButton) openAllButton.interactable = count > 1 && pack != null;
        }

        private void OpenOne()
        {
            Open(1);
        }

        private void OpenAll()
        {
            int count = PlayerProfile.Data.GetPackCount(packId);
            Open(count);
        }

        private void Open(int times)
        {
            var pack = GameDB.GetPackById(packId);
            if (pack == null || times <= 0) return;

            var sb = new StringBuilder();
            int opened = 0;
            while (times-- > 0 && PlayerProfile.Data.ConsumePack(packId))
            {
                var cards = PackService.OpenPack(pack);
                var summary = CollectionService.AwardCards(cards);
                sb.AppendLine($"Opened {pack.DisplayName}:");
                foreach (var c in cards)
                {
                    if (c == null) continue;
                    sb.AppendLine($" - {c.DisplayName} ({c.Rarity?.DisplayName})");
                }
                if (!string.IsNullOrEmpty(summary))
                {
                    sb.AppendLine("-- Awards --");
                    sb.AppendLine(summary);
                }
                opened++;
            }

            if (resultsText)
            {
                if (opened == 0)
                    resultsText.text = "No packs to open.";
                else
                    resultsText.text = sb.ToString();
            }

            PlayerProfile.Save();
            Refresh();
        }
    }
}
