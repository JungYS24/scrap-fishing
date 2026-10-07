using System;
using System.Collections.Generic;
using ScrapFishing.Audio;
using ScrapFishing.Boat;
using ScrapFishing.Controls;
using ScrapFishing.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ScrapFishing.Dive
{
    public class DiveDirector : MonoBehaviour
    {
        public event Action Finished;

        const float SurfaceY = 3.2f;
        const int SchoolSize = 5;

        static readonly float[] MineRadius = { 0.26f, 0.4f, 0.56f };
        static readonly int[][] MineSizesByZone =
        {
            new[] { 0, 0, 1 },
            new[] { 0, 1 },
            new[] { 1, 2 },
            new[] { 1, 2, 2 }
        };

        GameObject _neocity;
        GameObject _background;
        GameObject _midground;
        GameObject _player;
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
        DiveArt _art;
        readonly List<Hazard> _hazards = new List<Hazard>();
        readonly List<GameObject> _effects = new List<GameObject>();
        bool _active;
        float _iframe;
        float _bubbleTimer;

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
            _art = Resources.Load<DiveArt>("DiveArt");

            var world = GameObject.Find("World");
            _neocity = FindInWorld(world, "Neocity");
            _background = FindInWorld(world, "Background");
            _midground = FindInWorld(world, "Midground");
            _player = FindInWorld(world, "Player");
            _mineTemplate = FindInWorld(world, "Mine");
            _surfaceProps = GameObject.Find("SurfaceProps");
            if (_player != null)
            {
                _diver = _player.GetComponent<DiverController>();
                if (_diver == null)
                {
                    _diver = _player.AddComponent<DiverController>();
                }

                _diver.Bind(_stick, new Vector2(-2.15f, -4f), new Vector2(2.15f, SurfaceY));
                if (_art != null)
                {
                    _diver.SetAnimations(_art.Idle, _art.Swim, _art.Hurt);
                }
            }

            BindParallax(_background);
            BindParallax(_midground);

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
            _bubbleTimer = 0.3f;
            _oxygen.ResetMeter(DepthZone.DiveSeconds(_session.DeepestCast));
            _session.MarkDived();
            if (_diver != null)
            {
                _diver.Place(new Vector3(0f, -1.35f, 0f));
            }

            _spawner.SetDiveArt(_art);
            _spawner.SpawnForDive(_session.DeepestCast);
            if (_magnet != null)
            {
                _magnet.SetRadius(0.5f + DepthZone.Index(_session.DeepestCast) * 0.08f);
            }

            BuildMines(_session.DeepestCast);
            SpawnSchool();
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
            var mine = !forced && _iframe <= 0f ? HitMine() : null;
            if (mine != null)
            {
                _oxygen.Add(DepthZone.MineDamage(_session.DeepestCast));
                _session.AddCY(mine.Amount);
                SpawnExplosion(mine.transform.position, mine.Size);
                mine.gameObject.SetActive(false);
                _iframe = 0.45f;
                if (_diver != null)
                {
                    _diver.FlashHurt();
                }

                AudioManager.Ensure().PlayHurt();
            }

            TickBubbles();
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
            ClearEffects();
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
            SetActive(_surfaceProps, false);
            SetActive(_hook, false);
            if (_gauge != null)
            {
                _gauge.gameObject.SetActive(false);
            }
        }

        void SpawnSchool()
        {
            if (_art == null)
            {
                return;
            }

            for (var i = 0; i < SchoolSize; i++)
            {
                var position = new Vector3(Random.Range(-2.8f, 2.8f), Random.Range(-3.2f, 2.6f), 0f);
                var fish = SpawnEffect("SchoolFish", position, _art.Fish(Random.Range(0, 3)), 6f, true, -12, new Color(0.45f, 0.6f, 0.75f, 0.75f), 0.75f);
                var direction = Random.value < 0.5f ? -1f : 1f;
                fish.AddComponent<LinearMotion>().Configure(new Vector2(direction * Random.Range(0.25f, 0.6f), 0f), 0.08f, 1.2f, 3f);
                fish.GetComponent<SpriteAnimator>().FaceMovement(true);
            }
        }

        void TickBubbles()
        {
            if (_art == null || _diver == null)
            {
                return;
            }

            _bubbleTimer -= Time.deltaTime;
            if (_bubbleTimer > 0f)
            {
                return;
            }

            _bubbleTimer = Random.Range(0.45f, 0.8f);
            var bubble = SpawnEffect("Bubble", _diver.Position + new Vector3(0f, 0.25f, 0f), _art.BubbleFrames, 4.5f, false, 6, new Color(1f, 1f, 1f, 0.85f), 0.8f);
            bubble.AddComponent<LinearMotion>().Configure(new Vector2(0f, 0.8f), 0.05f, 5f);
        }

        void SpawnExplosion(Vector3 position, int size)
        {
            if (_art != null)
            {
                SpawnEffect("MineBlast", position, _art.Explosion(size), 20f, false, 9, Color.white, 1f);
            }
        }

        GameObject SpawnEffect(string name, Vector3 position, Sprite[] frames, float fps, bool loop, int order, Color color, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            renderer.color = color;
            go.AddComponent<SpriteAnimator>().Play(frames, fps, loop, !loop);
            _effects.Add(go);
            return go;
        }

        void ClearEffects()
        {
            for (var i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null)
                {
                    Destroy(_effects[i]);
                }
            }

            _effects.Clear();
        }

        void BuildMines(float depth)
        {
            ClearMines();
            var penalty = DepthZone.MinePenalty(depth);
            var zone = DepthZone.Index(depth);
            if (_mineTemplate != null)
            {
                PlaceMine(_mineTemplate, new Vector3(-1.1f, -0.8f, 0f), penalty, PickMineSize(zone));
            }

            var extra = 2 + DepthZone.ExtraMines(depth);
            for (var i = 0; i < extra; i++)
            {
                var go = _mineTemplate != null ? Instantiate(_mineTemplate) : new GameObject("Mine");
                go.name = "DiveMine";
                go.transform.SetParent(transform, false);
                PlaceMine(go, new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-3.2f, 0.4f), 0f), penalty, PickMineSize(zone));
            }
        }

        static int PickMineSize(int zone)
        {
            var options = MineSizesByZone[Mathf.Clamp(zone, 0, MineSizesByZone.Length - 1)];
            return options[Random.Range(0, options.Length)];
        }

        void PlaceMine(GameObject go, Vector3 position, int penalty, int size)
        {
            go.SetActive(true);
            go.transform.position = position;
            var hazard = go.GetComponent<Hazard>();
            if (hazard == null)
            {
                hazard = go.AddComponent<Hazard>();
            }

            hazard.Configure(penalty, MineRadius[size], size);
            var renderer = go.GetComponent<SpriteRenderer>();
            var sprite = _art != null ? _art.Mine(size) : null;
            if (renderer != null && sprite != null)
            {
                renderer.sprite = sprite;
            }

            _hazards.Add(hazard);
            EnsureDrift(go, 0.18f, 0.28f, 0.55f);
        }

        static void EnsureDrift(GameObject go, float rangeX, float rangeY, float speed)
        {
            var drift = go.GetComponent<AmbientDrift>();
            if (drift == null)
            {
                drift = go.AddComponent<AmbientDrift>();
            }

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

        Hazard HitMine()
        {
            if (_diver == null)
            {
                return null;
            }

            var point = _diver.Position;
            for (var i = 0; i < _hazards.Count; i++)
            {
                var hazard = _hazards[i];
                if (hazard != null && hazard.gameObject.activeSelf && hazard.Hits(point))
                {
                    return hazard;
                }
            }

            return null;
        }

        void BindParallax(GameObject root)
        {
            if (root == null || _player == null)
            {
                return;
            }

            foreach (var layer in root.GetComponentsInChildren<DiveParallax>(true))
            {
                layer.Bind(_player.transform);
            }
        }

        static GameObject FindInWorld(GameObject world, string name)
        {
            if (world == null)
            {
                return GameObject.Find(name);
            }

            var child = world.transform.Find(name);
            return child != null ? child.gameObject : null;
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
