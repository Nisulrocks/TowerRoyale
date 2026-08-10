using UnityEngine;
using TMPro;
using Photon.Pun;
using TR.Data;
using TR.Systems;
using TR.Net;

namespace TR.Battle
{
    
    public class PartnerDragGhost : MonoBehaviour
    {
        [Header("Appearance")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 0.5f;
        [SerializeField] private float followLerp = 18f;
        [SerializeField] private float labelHeight = 1.2f;
        [SerializeField] private float labelFontSize = 3f;

        private DuoBattleCoordinator _coordinator;
        private GameObject _ghost;
        private string _currentCardId;
        private Vector3 _targetPos;
        private bool _active;
        private Color _playerColor = Color.white;
        private TextMeshPro _label;
        private Transform _labelTransform;

        private void Start()
        {
            if (!DuoRuntime.IsDuo)
            {
                enabled = false;
                return;
            }
            _coordinator = FindFirstObjectByType<DuoBattleCoordinator>(FindObjectsInactive.Include);
            if (_coordinator != null) _coordinator.OnPartnerDragChanged += OnPartnerDragChanged;
        }

        private void OnDestroy()
        {
            if (_coordinator != null) _coordinator.OnPartnerDragChanged -= OnPartnerDragChanged;
        }

        private void OnPartnerDragChanged(bool active, string cardId, int level, float worldX, float worldY, int actorNumber)
        {
            if (!active)
            {
                DestroyGhost();
                _active = false;
                return;
            }

            _active = true;
            _targetPos = new Vector3(worldX, worldY, 0f);
            _playerColor = DuoPlayerColors.GetColorForActor(actorNumber);

            
            if (_ghost == null || cardId != _currentCardId)
            {
                DestroyGhost();
                CreateGhost(cardId, level, actorNumber);
                _currentCardId = cardId;
                if (_ghost != null) _ghost.transform.position = _targetPos;
            }
        }

        private void Update()
        {
            if (!_active || _ghost == null) return;
            
            _ghost.transform.position = Vector3.Lerp(_ghost.transform.position, _targetPos, Mathf.Clamp01(followLerp * Time.unscaledDeltaTime));
        }

        private void CreateGhost(string cardId, int level, int actorNumber)
        {
            var card = GameDB.GetCardById(cardId);
            Color c = _playerColor; c.a = alpha;
            if (card != null && card.TowerPrefab != null)
            {
                _ghost = Instantiate(card.TowerPrefab);
                
                foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                foreach (var col in _ghost.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                foreach (var col2d in _ghost.GetComponentsInChildren<Collider2D>(true)) col2d.enabled = false;
                foreach (var sr in _ghost.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.color = c;
                    sr.sortingOrder += 1;
                }
                _ghost.name = $"Partner_{(card != null ? card.DisplayName : cardId)}_Ghost";
            }
            else
            {
                _ghost = new GameObject("PartnerGhost2D");
                var sr = _ghost.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                sr.color = c;
            }
            var p = _ghost.transform.position; p.z = 0f; _ghost.transform.position = p;

            CreateLabel(card, level, actorNumber);
        }

        private void CreateLabel(CardDefinition card, int level, int actorNumber)
        {
            var go = new GameObject("PartnerGhostLabel");
            _labelTransform = go.transform;
            
            _labelTransform.SetParent(_ghost.transform, false);
            _labelTransform.localPosition = Vector3.up * labelHeight;
            _label = go.AddComponent<TextMeshPro>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = labelFontSize;
            _label.color = _playerColor;
            var rt = _label.rectTransform;
            rt.sizeDelta = new Vector2(6f, 2f);
            _label.sortingOrder = 100;

            string towerName = card != null ? card.DisplayName : _currentCardId;
            string playerName = GetPlayerName(actorNumber);
            _label.text = $"{towerName}  Lv {Mathf.Max(1, level)}\n<size=80%>{playerName}</size>";
        }

        private static string GetPlayerName(int actorNumber)
        {
            if (PhotonNetwork.CurrentRoom != null)
            {
                var p = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
                if (p != null)
                {
                    if (p.CustomProperties != null && p.CustomProperties.TryGetValue(DuoNetworkManager.PROP_NICK, out var v) && v is string s && !string.IsNullOrEmpty(s))
                        return s;
                    if (!string.IsNullOrEmpty(p.NickName)) return p.NickName;
                }
            }
            return "Partner";
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
            if (_labelTransform != null)
            {
                Destroy(_labelTransform.gameObject);
                _labelTransform = null;
                _label = null;
            }
            _currentCardId = null;
        }

        private static Sprite CreateSquareSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var cols = new Color[size * size];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
