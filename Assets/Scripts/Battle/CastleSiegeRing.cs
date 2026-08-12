using System.Collections.Generic;
using UnityEngine;

namespace TR.Battle
{




    public static class CastleSiegeRing
    {
        public static float BaseRadius = 1.25f;
        public static float RingSpacing = 0.8f;



        public static float SlotArc = 0.8f;

        public struct Slot
        {
            public int ring;
            public int index;
            public bool valid;
        }

        private static readonly Dictionary<EnemyBase2D, Slot> _byEnemy = new();
        private static readonly Dictionary<long, EnemyBase2D> _bySlot = new();

        private static long Key(int ring, int index) => ((long)ring << 32) | (uint)index;

        public static int SlotsInRing(int ring)
        {
            float r = RadiusOfRing(ring);
            return Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * r / Mathf.Max(0.05f, SlotArc)));
        }

        public static float RadiusOfRing(int ring) => BaseRadius + Mathf.Max(0, ring) * RingSpacing;




        public static float AngleOfSlot(int ring, int index)
        {
            int n = SlotsInRing(ring);
            float step = 2f * Mathf.PI / n;
            float stagger = (ring % 2 == 0) ? 0f : step * 0.5f;
            return index * step + stagger;
        }

        public static Vector3 SlotPosition(Vector3 castle, Slot slot)
        {
            if (!slot.valid) return castle;
            float a = AngleOfSlot(slot.ring, slot.index);
            float r = RadiusOfRing(slot.ring);
            return castle + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }





        public static Slot Claim(EnemyBase2D enemy, Vector3 castle)
        {
            if (enemy == null) return default;
            if (_byEnemy.TryGetValue(enemy, out var existing) && existing.valid) return existing;

            Prune();

            Vector3 from = enemy.transform.position;
            float arrivalAngle = Mathf.Atan2(from.y - castle.y, from.x - castle.x);

            for (int ring = 0; ring < 32; ring++)
            {
                int n = SlotsInRing(ring);

                float bestGap = -1f;
                float bestArrivalDelta = float.MaxValue;
                int best = -1;

                for (int i = 0; i < n; i++)
                {
                    if (_bySlot.ContainsKey(Key(ring, i))) continue;




                    float gap = SlotScore(ring, i, n);
                    float arrivalDelta = Mathf.Abs(Mathf.DeltaAngle(AngleOfSlot(ring, i) * Mathf.Rad2Deg,
                                                                    arrivalAngle * Mathf.Rad2Deg));




                    if (best < 0 || gap > bestGap + 0.5f)
                    {
                        best = i;
                        bestGap = gap;
                        bestArrivalDelta = arrivalDelta;
                    }
                    else if (gap >= bestGap - 0.5f && arrivalDelta < bestArrivalDelta)
                    {
                        best = i;
                        bestGap = Mathf.Max(bestGap, gap);
                        bestArrivalDelta = arrivalDelta;
                    }
                }

                if (best < 0) continue;

                var slot = new Slot { ring = ring, index = best, valid = true };
                _byEnemy[enemy] = slot;
                _bySlot[Key(ring, best)] = enemy;
                return slot;
            }

            return default;
        }




        private static float SlotScore(int ring, int index, int n)
        {
            int cw = -1, ccw = -1;
            for (int d = 1; d <= n; d++)
            {
                if (cw < 0 && _bySlot.ContainsKey(Key(ring, (index + d) % n))) cw = d;
                if (ccw < 0 && _bySlot.ContainsKey(Key(ring, ((index - d) % n + n) % n))) ccw = d;
                if (cw >= 0 && ccw >= 0) break;
            }

            if (cw < 0 && ccw < 0) return n * 1000f + 2f * n;
            if (cw < 0) cw = n;
            if (ccw < 0) ccw = n;




            return Mathf.Min(cw, ccw) * 1000f + (cw + ccw);
        }

        public static void Release(EnemyBase2D enemy)
        {
            if (enemy == null) return;
            if (!_byEnemy.TryGetValue(enemy, out var slot)) return;
            _byEnemy.Remove(enemy);
            if (slot.valid) _bySlot.Remove(Key(slot.ring, slot.index));
        }




        private static void Prune()
        {
            if (_byEnemy.Count == 0) return;

            List<EnemyBase2D> dead = null;
            foreach (var kv in _byEnemy)
            {
                if (kv.Key != null) continue;
                (dead ??= new List<EnemyBase2D>()).Add(kv.Key);
                if (kv.Value.valid) _bySlot.Remove(Key(kv.Value.ring, kv.Value.index));
            }
            if (dead == null) return;

            for (int i = 0; i < dead.Count; i++) _byEnemy.Remove(dead[i]);
        }

        public static void Reset()
        {
            _byEnemy.Clear();
            _bySlot.Clear();
        }
    }
}
