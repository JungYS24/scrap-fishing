using System.Collections.Generic;
using UnityEngine;

namespace ScrapFishing.Audio
{
    [CreateAssetMenu(menuName = "ScrapFishing/Sound Cue Library")]
    public class SoundCueLibrary : ScriptableObject
    {
        public SoundCue[] cues = System.Array.Empty<SoundCue>();

        Dictionary<SfxId, SoundCue> _map;

        void OnEnable()
        {
            _map = null;
        }

        public SoundCue Get(SfxId id)
        {
            if (_map == null)
            {
                Rebuild();
            }

            _map.TryGetValue(id, out var cue);
            return cue;
        }

        public void Rebuild()
        {
            _map = new Dictionary<SfxId, SoundCue>();
            if (cues == null)
            {
                return;
            }

            for (var i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (cue != null)
                {
                    _map[cue.id] = cue;
                }
            }
        }
    }
}
