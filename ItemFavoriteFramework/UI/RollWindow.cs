using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The open rolls, at the right edge of the screen. Unlike the config editors it never holds input: rolls
    // come up mid-fight, so the player keeps moving and attacking. Its buttons only take clicks while the
    // cursor is free (inventory, map or chat open); otherwise the vote keys and /need, /greed, /pass do it.
    //
    // Rebuilt whenever the list of rolls changes; only the timers are touched per frame.
    internal static class RollWindow {
        private const float Width = 460f;
        private const float HeaderHeight = 48f;
        private const float RowHeight = 92f;
        private const int MaxRows = 6;

        private static GameObject panel;
        private static CanvasGroup group;
        private static int builtRevision = -1;
        private static Vector2? position;
        private static readonly List<KeyValuePair<ClientRoll, RectTransform>> timers = new List<KeyValuePair<ClientRoll, RectTransform>>();
        private static readonly List<KeyValuePair<ClientRoll, Text>> clocks = new List<KeyValuePair<ClientRoll, Text>>();
        private static float barWidth;

        internal static bool IsUsable => GUIManager.Instance != null && GUIManager.CustomGUIFront != null;

        // Per frame, from WorldTick.
        internal static void Update() {
            bool wanted = LootConfig.ShowRollWindow.Value && RollClient.Open.Count > 0 && Hud.IsUserHidden() == false && IsUsable;
            if (wanted == false) {
                Destroy();
                return;
            }
            if (panel == null || builtRevision != RollClient.Revision) { Build(); }
            if (panel == null) { return; }

            bool cursor = ZCursor.IsVisible;
            group.blocksRaycasts = cursor;
            group.interactable = cursor;
            foreach (KeyValuePair<ClientRoll, RectTransform> timer in timers) {
                float total = Mathf.Max(1f, timer.Key.EndsAt - timer.Key.StartedAt);
                timer.Value.sizeDelta = new Vector2(barWidth * Mathf.Clamp01(timer.Key.SecondsLeft / total), timer.Value.sizeDelta.y);
            }
            foreach (KeyValuePair<ClientRoll, Text> clock in clocks) {
                clock.Value.text = Mathf.CeilToInt(clock.Key.SecondsLeft) + "s";
            }
        }

        private static void Destroy() {
            if (panel != null) {
                position = ((RectTransform)panel.transform).anchoredPosition;
                Object.Destroy(panel);
            }
            panel = null;
            group = null;
            timers.Clear();
            clocks.Clear();
            builtRevision = -1;
        }

        private static void Build() {
            Destroy();
            List<ClientRoll> rolls = RollClient.Open.ToList();
            int shown = Mathf.Min(MaxRows, rolls.Count);
            bool more = rolls.Count > MaxRows;
            float height = HeaderHeight + shown * RowHeight + (more ? 28f : 0f) + 16f;

            panel = GUIManager.Instance.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f),
                position: position ?? new Vector2(-Width / 2f - 24f, 60f),
                width: Width, height: height, draggable: true);
            panel.name = "IFF_RollWindow";
            group = panel.AddComponent<CanvasGroup>();
            builtRevision = RollClient.Revision;
            Transform root = panel.transform;

            GameObject dice = ConfigUI.NewRect("Dice", root, 14f, 10f, 30f, 30f);
            Image diceImage = dice.AddComponent<Image>();
            diceImage.sprite = SpriteFactory.Dice;
            diceImage.color = new Color(1f, 0.85f, 0.45f);
            diceImage.raycastTarget = false;
            ConfigUI.AddText(root, 52f, 8f, 180f, 34f, ConfigUI.L("$iff_roll_window_title", "Loot rolls"), 20, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimYellow);
            if (rolls.Count(r => r.MyVote == RollChoice.None) > 1) {
                ConfigUI.AddButton(root, Width - 216f, 10f, 96f, ConfigUI.L("$iff_roll_greed_all", "Greed all"), () => RollClient.VoteAll(RollChoice.Greed), 30f);
                ConfigUI.AddButton(root, Width - 112f, 10f, 96f, ConfigUI.L("$iff_roll_pass_all", "Pass all"), () => RollClient.VoteAll(RollChoice.Pass), 30f);
            }

            barWidth = Width - 32f;
            for (int i = 0; i < shown; i++) { BuildRow(root, rolls[i], HeaderHeight + i * RowHeight); }
            if (more) {
                ConfigUI.AddText(root, 16f, HeaderHeight + shown * RowHeight, Width - 32f, 26f,
                    ConfigUI.L("$iff_roll_window_more", "+ more rolls waiting"), 14, TextAnchor.MiddleLeft);
            }
        }

        private static void BuildRow(Transform root, ClientRoll roll, float y) {
            GameObject row = ConfigUI.NewRect("Roll", root, 16f, y, Width - 32f, RowHeight - 6f);
            Image background = row.AddComponent<Image>();
            background.color = roll.Wishlisted ? new Color(0.45f, 0.12f, 0.2f, 0.35f) : new Color(0f, 0f, 0f, 0.3f);
            background.raycastTarget = false;
            Transform t = row.transform;
            float w = Width - 32f;

            GameObject iconGo = ConfigUI.NewRect("Icon", t, 6f, 6f, 52f, 52f);
            Image icon = iconGo.AddComponent<Image>();
            icon.sprite = roll.Item?.GetIcon();
            icon.preserveAspect = true;
            icon.enabled = icon.sprite != null;
            AddTooltip(iconGo, roll);

            ConfigUI.AddText(t, 66f, 2f, w - 66f - 50f, 24f, roll.Name, 16, TextAnchor.MiddleLeft);
            Text clock = ConfigUI.AddText(t, w - 50f, 2f, 46f, 24f, "", 15, TextAnchor.MiddleRight, GUIManager.Instance.ValheimYellow);
            clocks.Add(new KeyValuePair<ClientRoll, Text>(roll, clock));

            List<string> info = new List<string>();
            if (roll.Label.Length > 0) { info.Add(roll.Label); }
            if (roll.Wishlisted) { info.Add("<color=#" + ColorUtility.ToHtmlStringRGB(LootConfig.WishlistColor.Value) + ">" + RollClient.Localize("$iff_roll_on_wishlist") + "</color>"); }
            if (LootConfig.DisplayWantedBy.Value && roll.WantedBy.Count > 0) { info.Add(RollClient.Localize("$iff_tooltip_wanted_by", string.Join(", ", roll.WantedBy))); }
            ConfigUI.AddText(t, 66f, 24f, w - 70f, 20f, string.Join("  ·  ", info), 13, TextAnchor.MiddleLeft);

            if (roll.MyVote == RollChoice.None) {
                AddVoteButton(t, 66f, roll, RollChoice.Need, "$iff_roll_choice_need", roll.Preselected == RollChoice.Need);
                AddVoteButton(t, 66f + 96f, roll, RollChoice.Greed, "$iff_roll_choice_greed", false);
                AddVoteButton(t, 66f + 192f, roll, RollChoice.Pass, "$iff_roll_choice_pass", false);
            } else {
                ConfigUI.AddText(t, 66f, 48f, 280f, 26f, RollClient.Localize("$iff_roll_you_chose", RollClient.Localize("$iff_roll_choice_" + RollIds.ChoiceName(roll.MyVote))),
                    15, TextAnchor.MiddleLeft, GUIManager.Instance.ValheimOrange);
            }
            ConfigUI.AddText(t, w - 110f, 48f, 104f, 26f, $"{roll.Voted}/{roll.Of}", 14, TextAnchor.MiddleRight);

            GameObject bar = ConfigUI.NewRect("Timer", t, 0f, RowHeight - 10f, w, 4f);
            Image barImage = bar.AddComponent<Image>();
            barImage.color = new Color(1f, 0.76f, 0.3f, 0.9f);
            barImage.raycastTarget = false;
            timers.Add(new KeyValuePair<ClientRoll, RectTransform>(roll, (RectTransform)bar.transform));
        }

        private static void AddVoteButton(Transform parent, float x, ClientRoll roll, RollChoice choice, string token, bool preselected) {
            GameObject button = ConfigUI.AddButton(parent, x, 48f, 90f, RollClient.Localize(token), () => RollClient.Vote(roll, choice), 28f);
            if (preselected) {
                Image image = button.GetComponent<Image>();
                if (image != null) { image.color = new Color(1f, 0.85f, 0.4f); }
            }
        }

        // The item's own tooltip on hover, the same one inventory cells show (EpicLoot's included).
        private static void AddTooltip(GameObject target, ClientRoll roll) {
            if (roll.Item == null) { return; }
            GameObject prefabSource = InventoryGui.instance != null && InventoryGui.instance.m_playerGrid != null ? InventoryGui.instance.m_playerGrid.m_elementPrefab : null;
            UITooltip template = prefabSource != null ? prefabSource.GetComponent<UITooltip>() : null;
            if (template == null || template.m_tooltipPrefab == null) { return; }
            Image raycast = target.GetComponent<Image>();
            if (raycast != null) { raycast.raycastTarget = true; }
            UITooltip tooltip = target.AddComponent<UITooltip>();
            tooltip.m_tooltipPrefab = template.m_tooltipPrefab;
            tooltip.Set(roll.Item.m_shared.m_name, roll.Item.GetTooltip());
        }
    }
}
