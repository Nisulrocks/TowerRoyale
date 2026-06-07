using System.Collections;
using UnityEngine;

namespace TR.Battle
{
    
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private BattlePath path;
        [SerializeField] private int count = 10;
        [SerializeField] private float interval = 1.0f;
        [SerializeField] private float startDelay = 1.0f;

        private Coroutine _routine;

        private void OnEnable()
        {
            if (_routine == null && enemyPrefab != null && path != null)
            {
                _routine = StartCoroutine(SpawnRoutine());
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator SpawnRoutine()
        {
            yield return new WaitForSeconds(startDelay);
            for (int i = 0; i < count; i++)
            {
                var e = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
                e.Init(path);
                yield return new WaitForSeconds(interval);
            }
        }
    }
}
