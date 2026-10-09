using ScrapFishing.Boat;
using ScrapFishing.Core;
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
                    _session.AddCatch(scrap.Definition, scrap.transform.position);
                    _spawner.Remove(scrap);
                }
            }
        }
    }
}
