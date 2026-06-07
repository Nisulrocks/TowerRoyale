using UnityEngine;
using TR.Data;

namespace TR.Battle
{
    public static class TowerFactory
    {
        
        public static GameObject CreateTower(CardDefinition def, int level, Vector3 position, Quaternion rotation)
        {
            GameObject go = null;
            if (def != null && def.TowerPrefab != null)
            {
                go = Object.Instantiate(def.TowerPrefab, position, rotation);
            }
            else
            {
                
                go = new GameObject("TowerPlaceholder2D");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CreateSquareSprite();
                sr.color = new Color(0f, 1f, 1f, 1f);
                go.transform.position = position;
                go.transform.rotation = rotation;
                go.transform.localScale = Vector3.one;
            }

            int lv = Mathf.Max(1, level);

            
            int towersLayer = LayerMask.NameToLayer("Towers");
            if (towersLayer >= 0)
            {
                go.layer = towersLayer;
            }

            
            var tower = go.GetComponent<TowerBase>();
            if (tower == null) tower = go.AddComponent<TowerBase>();
            tower.Initialize(def, lv);
            bool specialized = false;

            
            if (def is TR.Data.InfernoCardDefinition infernoDef)
            {
                var inf = go.GetComponent<InfernoTower>();
                if (inf == null) inf = go.AddComponent<InfernoTower>();
                inf.Initialize(infernoDef, lv);
                specialized = true;
            }

            
            if (def is TR.Data.EconomyCardDefinition econDef)
            {
                var econ = go.GetComponent<EconomyTower>();
                if (econ == null) econ = go.AddComponent<EconomyTower>();
                econ.Initialize(econDef, lv);
                specialized = true;
            }

            
            if (def is TR.Data.BuffCardDefinition buffDef)
            {
                var buff = go.GetComponent<BuffTower>();
                if (buff == null) buff = go.AddComponent<BuffTower>();
                buff.Initialize(buffDef, lv);
                specialized = true;
            }

            
            if (def is TR.Data.PulseCardDefinition)
            {
                var pulse = go.GetComponent<TowerPulse>();
                if (pulse == null) pulse = go.AddComponent<TowerPulse>();
                
                pulse.Initialize(def, lv);
                
                specialized = true;
            }

            
            if (specialized)
            {
                tower.SetCombatEnabled(false);
            }

            
            if (go.GetComponent<Collider2D>() == null && go.GetComponentInChildren<Collider2D>(true) == null)
            {
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.4f;
            }

            
            if (go.GetComponent<TowerSelectable>() == null)
            {
                go.AddComponent<TowerSelectable>();
            }
            return go;
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
