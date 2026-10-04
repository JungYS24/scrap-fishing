using System;
using System.Collections.Generic;
using ScrapFishing.Audio;
using ScrapFishing.Boat;
using ScrapFishing.Controls;
using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Dive
{
    public class DiveDirector : MonoBehaviour
    {
        public event Action Finished;

        const float OxygenSeconds = 16f;
        const float SurfaceY = 3.2f;
        const float MineDamage = 0.18f;

        GameObject _neocity;
        GameObject _background;
        GameObject _midground;
        GameObject _player;
        GameObject _fish;
        GameObject _mineTemplate;
        GameObject _surfaceProps;
        GameObject _hook;
        DepthGauge _gauge;
        ScrapSpawner _spawner;
        RunSession _session;
        VirtualJoystick _stick;
        DiverController _diver;
        OxygenMeter _oxygen;
        MagnetPickup _magnet;
        readonly List<Hazard> _hazards = new List<Hazard>();
        bool _active;
        float _iframe;

        public float DiveFill => _oxygen != null ? _oxygen.Normalized : 0f;
        public bool IsActive => _active;
        public bool IsForcedAscent => _diver != null && _diver.IsForcedAscent;

        public void Bind(ScrapSpawner spawner, RunSession session, DepthGauge gauge, GameObject hook, VirtualJoystick stick)
        {
            _spawner = spawner;
            _session = session;
            _gauge = gauge;
            _hook = hook;
            _stick = stick;
            _neocity = GameObject.Find("Neocity");
            _background = GameObject.Find("Background");
            _midground = GameObject.Find("Midground");
            _player = GameObject.Find("Player");
            _fish = GameObject.Find("Fish");
            _mineTemplate = GameObject.Find("Mine");
            _surfaceProps = GameObject.Find("SurfaceProps");
            if (_player != null)
            {
                _diver = _player.GetComponent<DiverController>() ?? _player.AddComponent<DiverController>();
                _diver.Bind(_stick, new Vector2(-2.15f, -4f), new Vector2(2.15f, SurfaceY));
            }

            _oxygen = gameObject.AddComponent<OxygenMeter>();
            _magnet = gameObject.AddComponent<MagnetPickup>();
            if (_player != null)
            {
                _magnet.Bind(_player.transform, _spawner, _session);
            }

            ShowSurface();
        }

        public void Begin()
        {
            _active = true;
            _iframe = 0.4f;
            _oxygen.ResetMeter(OxygenSeconds);
            _session.MarkDived();
            if (_diver != null)
            {
                _diver.Place(new Vector3(0f, -1.35f, 0f));
            }
            var loot = _fish != null ? _fish.GetComponent<SpriteRenderer>() : null;
            _spawner.SetDiveSprite(loot != null ? loot.sprite : null);
            _spawner.SpawnForDive();
            BuildMines();
            if (_fish != null)
            {
                EnsureDrift(_fish, 0.45f, 0.22f, 0.7f);
            }

            ShowUnderwater();
            _stick.SetVisible(true);
        }

        public void Cancel()
        {
            if (!_active)
            {
                ShowSurface();
                return;
            }

            Finish(false);
        }

        void Update()
        {
            if (!_active)
            {
                return;
            }

            var forced = _diver != null && _diver.IsForcedAscent;
            if (!forced)
            {
                _oxygen.Fill(Time.deltaTime);
            }

            _magnet.Collect();
            _iframe = Mathf.Max(0f, _iframe - Time.deltaTime);
            if (!forced && _iframe <= 0f && HitsMine())
            {
                _oxygen.Add(MineDamage);
                _iframe = 0.45f;
                if (_diver != null)
                {
                    _diver.FlashHurt();
                }

                AudioManager.Ensure().PlayHurt();
            }

            if (!forced && (_oxygen.IsFull || _session.IsExpired))
            {
                BeginForcedAscent();
            }

            var atSurface = _diver != null && _diver.Position.y >= SurfaceY - 0.05f;
            var swamUp = !forced && atSurface && _stick.Value.y > 0.15f;
            if (atSurface && (_diver.IsForcedAscent || swamUp))
            {
                Finish(true);
            }
        }

        void BeginForcedAscent()
        {
            if (_diver != null)
            {
                _diver.BeginForcedAscent();
            }

            _stick.SetVisible(false);
        }

        void Finish(bool notify)
        {
            _active = false;
            _stick.SetVisible(false);
            _spawner.Clear();
            ClearMines();
            ShowSurface();
            if (notify)
            {
                Finished?.Invoke();
            }
        }

        void ShowSurface()
        {
            SetActive(_neocity, true);
            SetActive(_background, false);
            SetActive(_midground, false);
            SetActive(_player, false);
            SetActive(_fish, false);
            SetActive(_mineTemplate, false);
            SetActive(_surfaceProps, true);
            SetActive(_hook, true);
            if (_gauge != null)
            {
                _gauge.gameObject.SetActive(true);
            }
        }

        void ShowUnderwater()
        {
            SetActive(_neocity, false);
            SetActive(_background, true);
            SetActive(_midground, true);
            SetActive(_player, true);
            SetActive(_fish, true);
            SetActive(_surfaceProps, false);
            SetActive(_hook, false);
            if (_gauge != null)
            {
                _gauge.gameObject.SetActive(false);
            }
        }

        void BuildMines()
        {
            ClearMines();
            if (_mineTemplate != null)
            {
                PlaceMine(_mineTemplate, new Vector3(-1.1f, -0.8f, 0f));
            }

            for (var i = 0; i < 2; i++)
            {
                var go = _mineTemplate != null ? Instantiate(_mineTemplate) : new GameObject("Mine");
                go.name = "DiveMine";
                go.transform.SetParent(transform, false);
                PlaceMine(go, new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(-3.2f, 0.4f), 0f));
            }
        }

        void PlaceMine(GameObject go, Vector3 position)
        {
            go.SetActive(true);
            go.transform.position = position;
            var hazard = go.GetComponent<Hazard>() ?? go.AddComponent<Hazard>();
            _hazards.Add(hazard);
            EnsureDrift(go, 0.18f, 0.28f, 0.55f);
        }

        static void EnsureDrift(GameObject go, float rangeX, float rangeY, float speed)
        {
            var drift = go.GetComponent<AmbientDrift>() ?? go.AddComponent<AmbientDrift>();
            drift.Configure(go.transform.position, rangeX, rangeY, speed);
        }

        void ClearMines()
        {
            for (var i = 0; i < _hazards.Count; i++)
            {
                if (_hazards[i] == null)
                {
                    continue;
                }

                if (_hazards[i].gameObject == _mineTemplate)
                {
                    _hazards[i].gameObject.SetActive(false);
                    continue;
                }

                Destroy(_hazards[i].gameObject);
            }

            _hazards.Clear();
        }

        bool HitsMine()
        {
            if (_diver == null)
            {
                return false;
            }

            var point = _diver.Position;
            for (var i = 0; i < _hazards.Count; i++)
            {
                if (_hazards[i] != null && _hazards[i].Hits(point))
                {
                    return true;
                }
            }

            return false;
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }
    }
}
