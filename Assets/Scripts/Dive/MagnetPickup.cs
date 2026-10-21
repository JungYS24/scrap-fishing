using ScrapFishing.Audio;
using ScrapFishing.Boat;
using ScrapFishing.Core;
using ScrapFishing.Fishing;
using ScrapFishing.FX;
using UnityEngine;

namespace ScrapFishing.Dive
{
    public class MagnetPickup : MonoBehaviour
    {
        [SerializeField] float radius = 0.55f;

        Transform _diver;
        ScrapSpawner _spawner;
        RunSession _session;

        public void Bind(Transform diver, ScrapSpawner spawner, RunSession session)
        {
            _diver = diver;
            _spawner = spawner;
            _session = session;
        }

        public void SetRadius(float value)
        {
            radius = value;
        }

        public void Collect()
        {
            if (_diver == null || _spawner == null)
            {
                return;
            }

            for (var i = _spawner.Live.Count - 1; i >= 0; i--)
            {
                var scrap = _spawner.Live[i];
                if (scrap == null)
                {
                    continue;
                }

                if (Vector3.Distance(scrap.transform.position, _diver.position) <= radius)
                {
                    var from = scrap.transform.position;
                    var n = scrap.ChipValue;
                    var sprite = scrap.Renderer != null ? scrap.Renderer.sprite : null;
                    var scale = scrap.transform.localScale;
                    _session.AddCatch(scrap.Definition, scrap.data, from);
                    if (LureLoad.IsHazard(n))
                    {
                        AudioManager.Ensure().PlayHazard();
                        LureCollector.PresentHazard(from, n, null);
                        _spawner.Remove(scrap);
                        Fx.Burst(from, null, 6, Palette.Hazard);
                        continue;
                    }

                    AudioManager.Ensure().PlayCatch(scrap);
                    RarityGlow.PlayAcquireFlash(scrap);
                    _spawner.Remove(scrap);
                    Fx.SuckIn(from, _diver.position, sprite, scale, n);
                }
            }
        }
    }
}
