using UnityEngine;

namespace ScrapFishing.Boat
{
    [CreateAssetMenu(menuName = "ScrapFishing/ZoneBackdropArt")]
    public class ZoneBackdropArt : ScriptableObject
    {
        [SerializeField] Sprite[] zones = new Sprite[4];
        [SerializeField] Texture2D[] zoneTextures = new Texture2D[4];

        public Sprite[] Zones => zones;

        public Texture2D TextureAt(int index)
        {
            if (zoneTextures == null || index < 0 || index >= zoneTextures.Length)
            {
                return null;
            }

            return zoneTextures[index];
        }
    }
}
