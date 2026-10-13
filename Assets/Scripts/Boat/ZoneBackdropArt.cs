using UnityEngine;

namespace ScrapFishing.Boat
{
    [CreateAssetMenu(menuName = "ScrapFishing/ZoneBackdropArt")]
    public class ZoneBackdropArt : ScriptableObject
    {
        [SerializeField] Sprite[] zones = new Sprite[4];

        public Sprite[] Zones => zones;
    }
}
