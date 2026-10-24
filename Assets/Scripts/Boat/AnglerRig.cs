using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Boat
{
    // 보트 위 주인공(Nomad)을 보트(뒤) → 피셔 → 낚싯대(앞) 세 레이어로 조합한다.
    // Nomad 오브젝트를 부모로 써서 위치·크기·펀치 연출을 그대로 공유하고, 자식 RodTip(낚싯줄 시작점)을 낚싯대 끝으로 옮긴다.
    // 외형은 GearVisuals 규칙(장착 id, 없으면 normal_01)을 따른다. 효과는 GearBonuses가 따로 다룬다.
    public class AnglerRig : MonoBehaviour
    {
        // 보트·피셔 레이어(256)와 낚싯대 이미지(128)는 모두 PPU 100, 피벗 중앙이다.
        const float PixelsPerUnit = 100f;
        const float LayerSize = 256f;
        const float RodSize = 128f;
        const float GripRatio = 0.25f;
        public const float DefaultFootBelowMid = 0.1f;
        // order: 보트 Nomad−1, 피셔 Nomad, 낚싯대 Nomad+2(루어 6과 겹치지 않게). 드론은 이보다 앞(9).
        const int BoatOrderOffset = -1;
        const int RodOrderOffset = 2;

        [Tooltip("피셔 발끝을 보트 높이(불투명 범위)의 가운데에서 이 비율만큼 아래에 둔다. 0.1 = 보트 높이의 10%")]
        [SerializeField] float footBelowMid = DefaultFootBelowMid;

        public struct Layout
        {
            public float FisherOffsetY;
            public float FootTargetY;
            public Vector2 HandPx;
            public Vector2 RodOffsetPx;
            public Vector2 GripPx;
            public Vector2 TipPx;
        }

        SpriteRenderer _original;
        SpriteRenderer _fisher;
        SpriteRenderer _boat;
        SpriteRenderer _rod;
        Transform _rodTip;
        Transform _head;
        float _appliedFootBelowMid = float.NaN;

        // 피셔 머리 꼭대기 위치. 보트 씬 동반 드론이 이 점을 기준으로 떠다닌다.
        public Transform Head
        {
            get
            {
                EnsureLayers();
                return _head;
            }
        }

        // 256 레이어 픽셀 좌표로 계산한다. 피셔 발끝(footY)을 보트 불투명 범위의 가운데보다 footBelowMid만큼 아래에 두고,
        // 낚싯대는 손잡이 끝에서 끝 방향 25% 지점(쥐는 곳)이 피셔 두 손에 오게 놓는다.
        public static Layout Compute(GearAnchors.Boat boat, GearAnchors.Fisher fisher, GearAnchors.Rod rod, float fisherFootY, float footBelowMid)
        {
            var height = boat.Bottom - boat.Top;
            var footTarget = (boat.Top + boat.Bottom) * 0.5f + height * footBelowMid;
            var offsetY = footTarget - fisherFootY;
            var hand = fisher.Hand + new Vector2(0f, offsetY);
            var grip = rod.HandleEnd + (rod.Tip - rod.HandleEnd) * GripRatio;
            var rodOffset = hand - grip;
            return new Layout
            {
                FisherOffsetY = offsetY,
                FootTargetY = footTarget,
                HandPx = hand,
                RodOffsetPx = rodOffset,
                GripPx = rodOffset + grip,
                TipPx = rodOffset + rod.Tip
            };
        }

        // 256 레이어 픽셀 → 부모(Nomad) 로컬 좌표.
        public static Vector3 ToLocal(Vector2 px)
        {
            var half = LayerSize * 0.5f;
            return new Vector3((px.x - half) / PixelsPerUnit, (half - px.y) / PixelsPerUnit, 0f);
        }

        public static AnglerRig Attach(GameObject nomad)
        {
            if (nomad == null)
            {
                return null;
            }

            var rig = nomad.GetComponent<AnglerRig>();
            return rig != null ? rig : nomad.AddComponent<AnglerRig>();
        }

        // 장착이 바뀔 때마다(보트 교체 포함) 다시 부른다.
        public void Compose()
        {
            var boatId = GearVisuals.EquippedOrDefaultId(GearSlot.Boat);
            var fisherId = GearVisuals.EquippedOrDefaultId(GearSlot.Fisher);
            var rodId = GearVisuals.EquippedOrDefaultId(GearSlot.Rod);
            var boatSprite = GearVisuals.Load(GearVisuals.WorldPath(GearSlot.Boat, boatId));
            var fisherSprite = GearVisuals.Load(GearVisuals.WorldPath(GearSlot.Fisher, fisherId));
            var rodSprite = GearVisuals.Load(GearVisuals.WorldPath(GearSlot.Rod, rodId));
            EnsureLayers();
            if (boatSprite == null || fisherSprite == null || rodSprite == null
                || !GearAnchors.Boats.TryGetValue(boatId, out var boatAnchor)
                || !GearAnchors.Fishers.TryGetValue(fisherId, out var fisherAnchor)
                || !GearAnchors.Rods.TryGetValue(rodId, out var rodAnchor))
            {
                Debug.LogWarning("보트·피셔·낚싯대 레이어나 기준점이 없어 기존 Nomad 이미지를 씁니다.");
                ShowLayers(false);
                return;
            }

            var layout = Compute(boatAnchor, fisherAnchor, rodAnchor, GearAnchors.FisherFootY, footBelowMid);
            _appliedFootBelowMid = footBelowMid;
            _boat.sprite = boatSprite;
            _boat.transform.localPosition = Vector3.zero;
            _fisher.sprite = fisherSprite;
            _fisher.transform.localPosition = new Vector3(0f, -layout.FisherOffsetY / PixelsPerUnit, 0f);
            _rod.sprite = rodSprite;
            var rodCenter = layout.RodOffsetPx + new Vector2(RodSize * 0.5f, RodSize * 0.5f);
            _rod.transform.localPosition = ToLocal(rodCenter);
            if (_rodTip != null)
            {
                _rodTip.localPosition = ToLocal(layout.TipPx);
            }

            _head.localPosition = ToLocal(GearAnchors.FisherHead + new Vector2(0f, layout.FisherOffsetY));
            ShowLayers(true);
        }

        // Inspector에서 footBelowMid를 바꾸면 바로 다시 맞춘다.
        void Update()
        {
            if (!float.IsNaN(_appliedFootBelowMid) && !Mathf.Approximately(_appliedFootBelowMid, footBelowMid))
            {
                Compose();
            }
        }

        void EnsureLayers()
        {
            if (_original == null)
            {
                _original = GetComponent<SpriteRenderer>();
                _rodTip = transform.Find("RodTip");
            }

            if (_head == null)
            {
                _head = new GameObject("FisherHead").transform;
                _head.SetParent(transform, false);
                _head.localPosition = ToLocal(GearAnchors.FisherHead);
            }

            var order = _original != null ? _original.sortingOrder : 5;
            var layer = _original != null ? _original.sortingLayerID : 0;
            _boat = _boat != null ? _boat : CreateLayer("BoatLayer", layer, order + BoatOrderOffset);
            _fisher = _fisher != null ? _fisher : CreateLayer("FisherLayer", layer, order);
            _rod = _rod != null ? _rod : CreateLayer("RodLayer", layer, order + RodOrderOffset);
        }

        SpriteRenderer CreateLayer(string name, int sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        void ShowLayers(bool show)
        {
            if (_original != null)
            {
                _original.enabled = !show;
            }

            _fisher.gameObject.SetActive(show);
            _boat.gameObject.SetActive(show);
            _rod.gameObject.SetActive(show);
        }
    }
}
