using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Gemeinsame Bausteine für Leisten, die sich zur Laufzeit aus Sprites zusammensetzen
    /// (Aufladeanzeige usw.). Unlit, damit sie auch nachts (2D-Lichter) lesbar bleiben.
    /// </summary>
    public static class RuntimeBarSprites
    {
        private static Sprite white;
        private static Material unlit;
        private static Transform container;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            white = null;
            unlit = null;
            container = null;
        }

        /// <summary>1 Einheit breit und hoch, Pivot links/mittig: scale.x ist die Länge in Welteinheiten.</summary>
        public static Sprite White
        {
            get
            {
                if (white == null)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    white = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0f, 0.5f), texture.width, 0, SpriteMeshType.FullRect);
                }

                return white;
            }
        }

        public static Material Unlit
        {
            get
            {
                if (unlit == null)
                    unlit = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };

                return unlit;
            }
        }

        public static Transform Container
        {
            get
            {
                if (container == null)
                    container = new GameObject("WorldBars").transform;

                return container;
            }
        }

        public static SpriteRenderer Create(string name, Transform parent, int sortingOrder, string sortingLayerName)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = White;
            renderer.sharedMaterial = Unlit;
            renderer.sortingOrder = sortingOrder;

            foreach (SortingLayer layer in SortingLayer.layers)
            {
                if (layer.name == sortingLayerName)
                {
                    renderer.sortingLayerName = sortingLayerName;
                    break;
                }
            }

            return renderer;
        }
    }
}