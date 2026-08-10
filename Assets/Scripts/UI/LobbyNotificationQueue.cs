using System.Collections;
using UnityEngine;
using TR.Systems;

namespace TR.UI
{
    public class LobbyNotificationQueue : MonoBehaviour
    {
        [Header("Notifiers")]
        [SerializeField] private LobbyArenaUnlockNotifier arenaNotifier;
        [SerializeField] private LobbyCastleLevelUpNotifier levelUpNotifier;

        [SerializeField] private bool arenaFirst = true;

        private bool _processing;

        private void Awake()
        {
            if (arenaNotifier != null) arenaNotifier.autoPlay = false;
            if (levelUpNotifier != null) levelUpNotifier.autoPlay = false;
        }

        private void Start()
        {
            StartCoroutine(ProcessQueue());
        }

        private void OnEnable()
        {
            PlayerProfile.OnTrophiesChanged += OnPendingChanged;
            PlayerProfile.OnCastleLevelUp += OnCastleLevelUp;
        }

        private void OnDisable()
        {
            PlayerProfile.OnTrophiesChanged -= OnPendingChanged;
            PlayerProfile.OnCastleLevelUp -= OnCastleLevelUp;
        }

        private void OnPendingChanged(int trophies)
        {
            if (!_processing)
                StartCoroutine(ProcessQueue());
        }

        private void OnCastleLevelUp(int fromLevel, int toLevel)
        {
            if (!_processing)
                StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            _processing = true;

            while (true)
            {
                bool any = false;

                if (arenaFirst)
                {
                    if (TryShow(arenaNotifier)) { any = true; yield return WaitFor(arenaNotifier); }
                    if (TryShow(levelUpNotifier)) { any = true; yield return WaitFor(levelUpNotifier); }
                }
                else
                {
                    if (TryShow(levelUpNotifier)) { any = true; yield return WaitFor(levelUpNotifier); }
                    if (TryShow(arenaNotifier)) { any = true; yield return WaitFor(arenaNotifier); }
                }

                if (!any) break;
            }

            _processing = false;
        }

        private bool TryShow(LobbyArenaUnlockNotifier notifier)
        {
            if (notifier == null) return false;
            notifier.TryShowIfPending();
            return notifier.IsShowing;
        }

        private bool TryShow(LobbyCastleLevelUpNotifier notifier)
        {
            if (notifier == null) return false;
            notifier.TryShowIfPending();
            return notifier.IsShowing;
        }

        private IEnumerator WaitFor(LobbyArenaUnlockNotifier notifier)
        {
            if (notifier == null) yield break;
            while (notifier.IsShowing)
                yield return null;
        }

        private IEnumerator WaitFor(LobbyCastleLevelUpNotifier notifier)
        {
            if (notifier == null) yield break;
            while (notifier.IsShowing)
                yield return null;
        }
    }
}
