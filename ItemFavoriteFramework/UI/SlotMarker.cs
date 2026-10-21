using UnityEngine;
using UnityEngine.UI;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The woven frame on one inventory cell, and the grey veil over a trash-flagged item's icon. Lives on
    // the cell's own GameObject, so it is created with the cell, dies with it when vanilla rebuilds the
    // grid, and moves with it when a mod such as EAQS relocates cells.
    internal class SlotMarker : MonoBehaviour {
        private Image outerCord;
        private Image innerCord;
        private Image icon;
        private Image veil;
        private Image markIcon;
        private bool shown;
        private bool veiled;
        private Color lastOuter;
        private Color lastInner;
        private Color lastVeil;
        private Sprite lastIcon;

        // Filled in by GridVisualsPatch each frame, then applied.
        internal bool SlotFavorited;
        internal ItemMark ItemMark;
        internal CustomMarkType CustomMark;

        internal static SlotMarker For(InventoryElement element) {
            SlotMarker marker = element.GetComponent<SlotMarker>();
            if (marker == null) {
                marker = element.gameObject.AddComponent<SlotMarker>();
                marker.Build(element);
            }
            return marker;
        }

        private void Build(InventoryElement element) {
            GameObject weave = new GameObject("IFF_Weave", typeof(RectTransform));
            FillParent(weave, element.transform);

            // Just above the item icon, under the amount/quality text and the equipped marker. Parented to
            // the cell root rather than a vanilla child, since UI replacers (Auga) may not have them all.
            icon = element.m_icon;
            if (icon != null && icon.transform.parent == element.transform) {
                weave.transform.SetSiblingIndex(icon.transform.GetSiblingIndex() + 1);
            } else {
                weave.transform.SetAsLastSibling();
            }
            innerCord = NewImage("IFF_WeaveInner", weave.transform, SpriteFactory.WeaveInner);
            outerCord = NewImage("IFF_WeaveOuter", weave.transform, SpriteFactory.WeaveOuter);
            lastInner = innerCord.color;
            lastOuter = outerCord.color;

            // The icon's own sprite again, in translucent grey on top of it. A child of the icon so it
            // always covers exactly the icon. Tinting the icon itself would fight vanilla, which sets its
            // colour every frame, and redraw the inventory every frame a trash item is in view.
            if (icon != null) {
                veil = NewImage("IFF_TrashVeil", icon.transform, null);
                veil.preserveAspect = icon.preserveAspect;
                lastVeil = veil.color;
            }

            // A custom mark's icon, in the cell's top-left corner, above the weave.
            GameObject iconGo = new GameObject("IFF_MarkIcon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(element.transform, worldPositionStays: false);
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(2f, -2f);
            iconRect.sizeDelta = new Vector2(20f, 20f);
            iconGo.transform.SetSiblingIndex(weave.transform.GetSiblingIndex() + 1);
            markIcon = iconGo.GetComponent<Image>();
            markIcon.preserveAspect = true;
            markIcon.raycastTarget = false;
            markIcon.enabled = false;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite) {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            FillParent(go, parent);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static void FillParent(GameObject go, Transform parent) {
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal void Reset(bool slotFavorited) {
            SlotFavorited = slotFavorited;
            ItemMark = ItemMark.None;
            CustomMark = null;
        }

        internal void Apply() {
            if (outerCord == null) { return; }
            ApplyIcon();
            ApplyVeil();

            // The item's mark takes the inner cord and a favorited slot the outer one, so both show at once.
            // A cell with only one of them weaves both cords in its colour.
            Color outer, inner;
            if (TryGetItemColor(out Color itemColor)) {
                inner = itemColor;
                outer = SlotFavorited ? ValConfig.BorderColorFavoritedSlot.Value : itemColor;
            } else if (SlotFavorited) {
                inner = ValConfig.BorderColorFavoritedSlot.Value;
                outer = inner;
            } else {
                if (shown) {
                    outerCord.enabled = false;
                    innerCord.enabled = false;
                    shown = false;
                }
                return;
            }

            // Touching Image.color or .enabled dirties the canvas, so only write what actually changed.
            if (shown == false) {
                outerCord.enabled = true;
                innerCord.enabled = true;
                shown = true;
            }
            if (outer != lastOuter) {
                outerCord.color = outer;
                lastOuter = outer;
            }
            if (inner != lastInner) {
                innerCord.color = inner;
                lastInner = inner;
            }
        }

        // A custom mark outranks the built-in ones (it is about this item right now).
        private bool TryGetItemColor(out Color color) {
            if (CustomMark != null) {
                color = CustomMark.Color;
            } else if (ItemMark == ItemMark.Favorite) {
                color = ValConfig.BorderColorFavoritedItem.Value;
            } else if (ItemMark == ItemMark.Trash) {
                color = ValConfig.BorderColorTrashFlaggedItem.Value;
            } else {
                color = default;
                return false;
            }
            return true;
        }

        private void ApplyVeil() {
            if (veil == null) { return; }
            Color color = ValConfig.IconOverlayTrashFlaggedItem.Value;
            if (ItemMark != ItemMark.Trash || color.a <= 0f || icon.enabled == false) {
                if (veiled) {
                    veil.enabled = false;
                    veiled = false;
                }
                return;
            }
            // Vanilla sets the icon's sprite every frame; it only changes when the item does.
            if (veil.sprite != icon.sprite) { veil.sprite = icon.sprite; }
            if (veiled == false) {
                veil.enabled = true;
                veiled = true;
            }
            if (color != lastVeil) {
                veil.color = color;
                lastVeil = color;
            }
        }

        private void ApplyIcon() {
            if (markIcon == null) { return; }
            Sprite wanted = CustomMark != null ? CustomMark.Icon : null;
            if (wanted == lastIcon) { return; }
            lastIcon = wanted;
            markIcon.sprite = wanted;
            markIcon.enabled = wanted != null;
            if (wanted != null) { markIcon.color = Color.white; }
        }
    }
}
