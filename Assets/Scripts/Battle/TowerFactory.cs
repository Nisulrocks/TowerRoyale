using UnityEngine;
using TR.Data;

namespace TR.Battle
{
    public static class TowerFactory
    {
        // Creates a tower GameObject for the given card and level at position/rotation.
        public static GameObject CreateTower(CardDefinition def, int level, Vector3 position, Quaternion rotation)
        {
            GameObject go = null;
            if (def != null && def.TowerPrefab != null)
            {
                go = Object.Instantiate(def.TowerPrefab, position, rotation);
            }
            else
            {
                // 2D-friendly fallback placeholder: a cyan square sprite
                go = new GameObject("TowerPlaceholder2D");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                sr.color = new Color(0f, 1f, 1f, 1f);
                go.transform.position = position;
                go.transform.rotation = rotation;
                go.transform.localScale = Vector3.one;
            }

            int lv = Mathf.Max(1, level);

            // Assign to dedicated Towers layer if it exists
            int towersLayer = LayerMask.NameToLayer("Towers");
            if (towersLayer >= 0)
            {
                go.layer = towersLayer;
            }

            // Always attach TowerBase for selection/range ring and common data
            var tower = go.GetComponent<TowerBase>();
            if (tower == null) tower = go.AddComponent<TowerBase>();
            tower.Initialize(def, lv);
            bool specialized = false;

            // Specialized: Inferno
            if (def is TR.Data.InfernoCardDefinition infernoDef)
            {
                var inf = go.GetComponent<InfernoTower>();
                if (inf == null) inf = go.AddComponent<InfernoTower>();
                inf.Initialize(infernoDef, lv);
                specialized = true;
            }

            // Specialized: Economy
            if (def is TR.Data.EconomyCardDefinition econDef)
            {
                var econ = go.GetComponent<EconomyTower>();
                if (econ == null) econ = go.AddComponent<EconomyTower>();
                econ.Initialize(econDef, lv);
                specialized = true;
            }

            // Specialized: Buff Tower
            if (def is TR.Data.BuffCardDefinition buffDef)
            {
                var buff = go.GetComponent<BuffTower>();
                if (buff == null) buff = go.AddComponent<BuffTower>();
                buff.Initialize(buffDef, lv);
                specialized = true;
            }

            // Specialized: Pulse Tower
            if (def is TR.Data.PulseCardDefinition)
            {
                var pulse = go.GetComponent<TowerPulse>();
                if (pulse == null) pulse = go.AddComponent<TowerPulse>();
                // Ensure the pulse component has the same definition/level initialization
                pulse.Initialize(def, lv);
                // TowerPulse derives from TowerBase and drives itself; no extra initialize needed beyond TowerBase.Initialize
                specialized = true;
            }

            // If a specialized behaviour exists, disable TowerBase combat so it handles only selection/range
            if (specialized)
            {
                tower.SetCombatEnabled(false);
            }

            // Ensure a collider for click selection
            if (go.GetComponent<Collider2D>() == null && go.GetComponentInChildren<Collider2D>(true) == null)
            {
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.4f;
            }

            // Ensure selectable component
            if (go.GetComponent<TowerSelectable>() == null)
            {
                go.AddComponent<TowerSelectable>();
            }
            return go;
        }

        private static Sprite CreateSquareSprite()
        {
            // Create a 16x16 white texture and turn it into a sprite
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
