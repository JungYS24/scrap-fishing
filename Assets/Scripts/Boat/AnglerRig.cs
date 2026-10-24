using System.Collections.Generic;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Boat
{
    // 보트 위 주인공(Nomad)을 피셔(뒤) → 보트 → 낚싯대(앞) 세 레이어로 조합한다.
    // Nomad 오브젝트를 부모로 써서 위치·크기·펀치 연출을 그대로 공유하고, 자식 RodTip(낚싯줄 시작점)을 낚싯대 끝으로 옮긴다.
    // 장착하지 않은 슬롯은 normal_01 외형만 쓴다(효과는 GearBonuses가 따로 다루므로 적용되지 않는다).
    public class AnglerRig : MonoBehaviour
    {
        public const string DefaultBoat = "boat_normal_01";
        public const string DefaultFisher = "fisher_normal_01";
        public const string DefaultRod = "rod_normal_01";

        // 보트·피셔 레이어(256)와 낚싯대 아이콘(128)은 모두 PPU 100, 피벗 중앙이다.
        const float PixelsPerUnit = 100f;
        const float LayerSize = 256f;
        const float RodSize = 128f;
        const float GripRatio = 0.25f;
        // 피셔는 뱃전(deckY)에서 이만큼 아래 행까지만 그린다. 타이어 구멍처럼 보트 레이어의 투명한 틈으로 다리가 비치지 않게 한다.
        public const float ClipBelowDeckPx = 4f;
        // 루어(6)와 같은 order가 되지 않게 낚싯대는 Nomad보다 2 위에 둔다.
        const int RodOrderOffset = 2;

        public struct Layout
        {
            public float FisherOffsetY;
            // 피셔 이미지에서 이 행(y)부터 아래는 그리지 않는다(뱃전 + ClipBelowDeckPx 높이).
            public float FisherClipY;
            public Vector2 HandPx;
            public Vector2 RodOffsetPx;
            public Vector2 GripPx;
            public Vector2 TipPx;
        }

        static readonly Dictionary<string, Sprite> Clipped = new Dictionary<string, Sprite>();

        SpriteRenderer _original;
        SpriteRenderer _fisher;
        SpriteRenderer _boat;
        SpriteRenderer _rod;
        Transform _rodTip;

        // 256 레이어 픽셀 좌표로 계산한다. 피셔는 보트 뱃전(deckY)에 허리(waistY)를 맞추고,
        // 낚싯대는 손잡이 끝에서 끝 방향 25% 지점(쥐는 곳)이 피셔 두 손에 오게 놓는다.
        public static Layout Compute(GearAnchors.Boat boat, GearAnchors.Fisher fisher, GearAnchors.Rod rod)
        {
            var offsetY = boat.DeckY - fisher.WaistY;
            var hand = fisher.Hand + new Vector2(0f, offsetY);
            var grip = rod.HandleEnd + (rod.Tip - rod.HandleEnd) * GripRatio;
            var rodOffset = hand - grip;
            return new Layout
            {
                FisherOffsetY = offsetY,
                FisherClipY = boat.DeckY + ClipBelowDeckPx - offsetY,
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

        public void Compose(GachaCatalog catalog)
        {
            var boat = Pick(catalog, GearSlot.Boat, DefaultBoat);
            var fisher = Pick(catalog, GearSlot.Fisher, DefaultFisher);
            var rod = Pick(catalog, GearSlot.Rod, DefaultRod);
            EnsureLayers();
            if (boat == null || fisher == null || rod == null || boat.Layer == null || fisher.Layer == null || rod.Icon == null
                || !GearAnchors.Boats.TryGetValue(boat.Id, out var boatAnchor)
                || !GearAnchors.Fishers.TryGetValue(fisher.Id, out var fisherAnchor)
                || !GearAnchors.Rods.TryGetValue(rod.Id, out var rodAnchor))
            {
                Debug.LogWarning("보트·피셔·낚싯대 레이어나 기준점이 없어 기존 Nomad 이미지를 씁니다.");
                ShowLayers(false);
                return;
            }

            var layout = Compute(boatAnchor, fisherAnchor, rodAnchor);
            _boat.sprite = boat.Layer;
            _boat.transform.localPosition = Vector3.zero;
            _fisher.sprite = ClipBelow(fisher.Layer, layout.FisherClipY);
            _fisher.transform.localPosition = new Vector3(0f, -layout.FisherOffsetY / PixelsPerUnit, 0f);
            _rod.sprite = rod.Icon;
            var rodCenter = layout.RodOffsetPx + new Vector2(RodSize * 0.5f, RodSize * 0.5f);
            _rod.transform.localPosition = ToLocal(rodCenter);
            if (_rodTip != null)
            {
                _rodTip.localPosition = ToLocal(layout.TipPx);
            }

            ShowLayers(true);
        }

        // 레이어 위쪽 clipY 행만 남긴 스프라이트. 피벗을 옮겨 원래 자리에 그대로 그려지게 한다.
        static Sprite ClipBelow(Sprite layer, float clipY)
        {
            var keep = Mathf.Clamp(Mathf.RoundToInt(clipY), 1, (int)LayerSize);
            if (layer == null || keep >= (int)layer.rect.height)
            {
                return layer;
            }

            var key = layer.GetInstanceID() + ":" + keep;
            if (Clipped.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var full = layer.rect;
            var cut = full.height - keep;
            var rect = new Rect(full.x, full.y + cut, full.width, keep);
            var pivot = new Vector2(0.5f, (full.height * 0.5f - cut) / keep);
            var sprite = Sprite.Create(layer.texture, rect, pivot, layer.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = layer.name + "_clip" + keep;
            Clipped[key] = sprite;
            return sprite;
        }

        static GearData Pick(GachaCatalog catalog, GearSlot slot, string fallbackId)
        {
            var equipped = GearBonuses.Equipped(slot);
            if (equipped != null)
            {
                return equipped;
            }

            return catalog != null ? catalog.FindGear(fallbackId) : null;
        }

        void EnsureLayers()
        {
            if (_original == null)
            {
                _original = GetComponent<SpriteRenderer>();
                _rodTip = transform.Find("RodTip");
            }

            var order = _original != null ? _original.sortingOrder : 5;
            var layer = _original != null ? _original.sortingLayerID : 0;
            _fisher = _fisher != null ? _fisher : CreateLayer("FisherLayer", layer, order - 1);
            _boat = _boat != null ? _boat : CreateLayer("BoatLayer", layer, order);
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
