using System.Collections.Generic;
using System.Linq;
using TMPro;
using ThreeColorCard;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // Retain old serialized references for the existing scene.
    public GameObject cardPrefab;
    public List<CardData> allCardDatas;
    [SerializeField] private int targetScore = 60;
    private BattleState battle;
    private readonly List<int> incoming = new List<int>();
    private readonly List<int> outgoing = new List<int>();
    private Transform root;
    private TMP_FontAsset font;
    private string notice = "";

    private void Start()
    {
        if (cardPrefab != null)
        {
            TMP_Text sample = cardPrefab.GetComponentInChildren<TMP_Text>(true);
            if (sample != null) font = sample.font;
            // The prototype has more Chinese labels than one 1024px atlas can hold.
            if (font != null) font.isMultiAtlasTexturesEnabled = true;
        }
        // Hide the old demonstration card, then use a dedicated runtime canvas.
        foreach (Card card in FindObjectsByType<Card>(FindObjectsSortMode.None))
            card.gameObject.SetActive(false);
        GameObject canvasObject = new GameObject("Battle Prototype", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 800);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root = canvasObject.transform;
        Restart();
    }

    private void Restart()
    {
        battle = new BattleState(System.Environment.TickCount, targetScore);
        incoming.Clear(); outgoing.Clear(); notice = "";
        Render();
    }

    private RectTransform Box(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private void Label(Transform parent, string text, float x, float y, float w, float h, int size = 22)
    {
        TextMeshProUGUI label = Box("Label", parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text; label.fontSize = size; label.color = Color.white;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void Button(Transform parent, string text, float x, float y, float w, float h,
        UnityEngine.Events.UnityAction action, Color color, bool enabled = true)
    {
        RectTransform rect = Box(text, parent, x, y, w, h);
        Image image = rect.gameObject.AddComponent<Image>(); image.color = color;
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image; button.interactable = enabled;
        button.onClick.AddListener(action);
        Label(rect, text, 10, 0, w - 20, h, 20);
    }

    private Color ColorFor(CardColors colors)
    {
        if (colors == CardColors.Red) return new Color(0.64f, 0.22f, 0.27f);
        if (colors == CardColors.Blue) return new Color(0.16f, 0.39f, 0.65f);
        if (colors == CardColors.Yellow) return new Color(0.55f, 0.40f, 0.10f);
        return new Color(0.45f, 0.30f, 0.58f);
    }

    private string ColorName(CardColors colors)
    {
        string result = "";
        if ((colors & CardColors.Red) != 0) result += "红";
        if ((colors & CardColors.Blue) != 0) result += "蓝";
        if ((colors & CardColors.Yellow) != 0) result += "黄";
        return result;
    }

    private void Select(List<int> selected, int id, int limit)
    {
        notice = "";
        if (selected.Contains(id)) selected.Remove(id);
        else if (selected.Count < limit) selected.Add(id);
        else notice = "已达到选择数量，请先取消一张。";
        Render();
    }

    private void Render()
    {
        foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        RectTransform backdrop = Box("Background", root, 0, 0, 1280, 800);
        backdrop.anchorMax = Vector2.one; backdrop.anchorMin = Vector2.zero;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.075f, 0.12f);
        Label(root, "三色牌 · 战斗原型", 30, 12, 900, 46, 30);
        Label(root, $"分数 {battle.Score:0.#} / {battle.Target}     金币 {battle.Coins:0.#}     能量 {battle.Energy:0.#}", 30, 62, 1100, 38);
        string stage = battle.Finished ? (battle.Won ? "挑战成功" : "挑战失败") :
            battle.Phase == 0 ? "主要阶段 · 选择三张牌" : $"次要阶段 {battle.Phase} / 3";
        Label(root, stage, 30, 106, 1100, 34, 24);
        Label(root, $"抽牌堆 {battle.DrawCount}   弃牌堆 {battle.DiscardCount}   已洗牌 {battle.ShuffleCount} 次", 30, 145, 1100, 30, 18);
        string hint = battle.Finished ? "本场已结束，可以重新开始尝试另一种资源分配。" : battle.Prepared ?
            "用箭头排列，最右侧为主牌；想换牌可点“返回重选”，结算前不锁定。" : battle.Phase >= 2 ?
            $"点击“移出”标记两张旧牌（{outgoing.Count}/2），再选两张手牌（{incoming.Count}/2），点击“替换两张牌”。" : $"从手牌选择 {(battle.Phase == 0 ? 3 : 2)} 张，按选中顺序放入计分区。";
        Label(root, hint, 30, 181, 1220, 35, 20);
        Label(root, "计分区（最右侧主牌决定额外收益）", 30, 221, 1000, 30, 20);
        for (int i = 0; i < battle.Scoring.Count; i++)
        {
            int index = i;
            BattleCard card = battle.Scoring[i];
            bool selected = outgoing.Contains(card.Id);
            string title = $"{ColorName(card.Colors)}  {card.Points} 点\n{(selected ? "待移出" : i == battle.Scoring.Count - 1 ? "主牌" : "计分牌")}";
            Button(root, title, 30 + i * 244, 260, 226, 64,
                () => Select(outgoing, card.Id, 2), selected ? ColorFor(card.Colors) * 1.4f : ColorFor(card.Colors),
                !battle.Finished && !battle.Prepared && battle.Phase >= 2);
            if (!battle.Finished && !battle.Prepared && battle.Phase >= 2)
                Button(root, selected ? "取消移出" : "移出", 30 + i * 244, 328, 226, 32,
                    () => Select(outgoing, card.Id, 2), selected ? new Color(0.65f, 0.3f, 0.15f) : new Color(0.24f, 0.32f, 0.43f));
            Button(root, "←", 30 + i * 244, 368, 108, 36,
                () => { battle.MoveScoringCard(index, -1); Render(); }, new Color(0.2f, 0.26f, 0.35f), !battle.Finished && i > 0);
            Button(root, "→", 148 + i * 244, 368, 108, 36,
                () => { battle.MoveScoringCard(index, 1); Render(); }, new Color(0.2f, 0.26f, 0.35f), !battle.Finished && i < battle.Scoring.Count - 1);
        }
        string preview = "";
        if (battle.Scoring.Count == 5)
        {
            ScoreResult result = BattleState.Calculate(battle.Scoring);
            preview = $"{(battle.Finished ? "当前牌面" : "结算预览")}：点数 {result.BasePoints} × 同色倍率 {result.Multiplier} → 分数 +{result.Score:0.#} / 金币 +{result.Coins:0.#} / 能量 +{result.Energy:0.#}";
        }
        Label(root, preview, 30, 410, 1220, 34, 19);
        Label(root, "手牌", 30, 451, 1000, 28, 20);
        for (int i = 0; i < battle.Hand.Count; i++)
        {
            BattleCard card = battle.Hand[i];
            int order = incoming.IndexOf(card.Id);
            Button(root, $"{ColorName(card.Colors)}  {card.Points} 点\n{(order >= 0 ? "选中 " + (order + 1) : "点击选择")}",
                30 + i * 174, 488, 158, 105, () => Select(incoming, card.Id, battle.Phase == 0 ? 3 : 2),
                order >= 0 ? ColorFor(card.Colors) * 1.4f : ColorFor(card.Colors));
        }
        if (battle.LastResult != null)
            Label(root, $"上次结算：分数 +{battle.LastResult.Score:0.#}   金币 +{battle.LastResult.Coins:0.#}   能量 +{battle.LastResult.Energy:0.#}", 30, 607, 1160, 32, 19);
        Label(root, notice, 30, 646, 1150, 30, 19);
        if (!battle.Finished)
        {
            bool valid = incoming.Count == (battle.Phase == 0 ? 3 : 2) && outgoing.Count == (battle.Phase >= 2 ? 2 : 0);
            Button(root, battle.Prepared ? (battle.Phase == 0 ? "确认排列 · 开始次要阶段" : "确认排列 · 结算") : battle.Phase >= 2 ? "替换两张牌" : "放入计分区", 30, 698, 380, 54, () =>
            {
                bool success = battle.Prepared ? battle.Confirm() : battle.Prepare(incoming, outgoing);
                if (!success) { notice = "操作未完成，请检查选牌数量。"; Render(); return; }
                incoming.Clear(); outgoing.Clear(); notice = ""; Render();
            }, new Color(0.15f, 0.43f, 0.40f), battle.Prepared || valid);
            if (battle.Prepared)
                Button(root, "返回重选", 430, 698, 200, 54, () =>
                {
                    if (battle.CancelPreparation())
                    { incoming.Clear(); outgoing.Clear(); notice = "已恢复本阶段选牌前的牌面，可以重新选择。"; }
                    Render();
                }, new Color(0.25f, 0.34f, 0.48f));
        }
        Button(root, "重新开始", 1030, 698, 220, 54, Restart, new Color(0.25f, 0.29f, 0.38f));
    }
}
