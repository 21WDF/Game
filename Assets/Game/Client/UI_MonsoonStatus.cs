using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 季风之城·季节气候状态显示（纯显示层，只读展示，不改任何机制逻辑）。
/// 挂载在 Canvas 下的常驻 UI GameObject 上（本物体需保持激活，root 指向可隐藏的显示子物体）。
///
/// 显示内容（与真正生效的机制状态严格同步，口径同 MonsoonManager 日志）：
///   当前季节（春/夏/秋/冬）· 昼夜（昼/夜） + 活跃气候并列列表（名称 + 剩余持续轮数；无气候显示「晴」）
///   例：「季风 · 夏·昼 | 暴雨（剩余 2 轮）、暴雪（剩余 1 轮）」
///
/// 刷新时机（事件驱动，无每帧轮询；对标 UI_TurnDisplay 惯例）：
///   1. MonsoonManager.OnMonsoonStateChanged —— 季节/昼夜切换（轮末推进）、气候出现/到期（轮开始结算）
///   2. CityStateManager.OnCityStateChanged —— 城邦确定/切换时显隐
///   3. 订阅完成时初始刷新一次
///
/// 非季风城邦：显示隐藏/文本清空，不残留任何季风信息（红线：非季风城邦不触发季风显示逻辑，
/// 仅做 IsActive 显隐判断，不读取季风状态）。
///
/// Editor 搭建：
///   1. Canvas 下创建常驻 GameObject（挂本脚本），子级放显示根（可含背景 + TextMeshProUGUI）
///   2. Inspector 拖入：statusText（文本引用）、root（显示根，可选——
///      未拖时回退 statusText 所在物体；与脚本同物体时退化为「清空文本」隐藏方式）
/// </summary>
public class UI_MonsoonStatus : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("状态文本（TextMeshProUGUI）")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("显示根物体（显隐控制，可选。未拖时回退 statusText 所在物体；勿指向本物体——隐藏时脚本会一并停用，无法再响应城邦切换）")]
    [SerializeField] private GameObject root;

    private void Awake()
    {
        // root 回退 statusText 所在物体（不能回退本物体：隐藏时连脚本一起停用 → 无法再订阅城邦切换事件）
        if (root == null && statusText != null && statusText.gameObject != gameObject)
            root = statusText.gameObject;
    }

    private void OnEnable()
    {
        // 延迟一帧订阅，确保 Manager 已完成 Awake 初始化（对标 UI_TurnDisplay 惯例）
        StartCoroutine(SubscribeNextFrame());
    }

    private System.Collections.IEnumerator SubscribeNextFrame()
    {
        yield return null; // 等一帧
        if (MonsoonManager.Instance != null)
            MonsoonManager.Instance.OnMonsoonStateChanged += Refresh;
        if (CityStateManager.Instance != null)
            CityStateManager.Instance.OnCityStateChanged += OnCityChanged;
        Refresh();   // 初始刷新一次（含城邦未确定时的隐藏）
    }

    private void OnDisable()
    {
        if (MonsoonManager.Instance != null)
            MonsoonManager.Instance.OnMonsoonStateChanged -= Refresh;
        if (CityStateManager.Instance != null)
            CityStateManager.Instance.OnCityStateChanged -= OnCityChanged;
    }

    /// <summary>城邦确定/切换 → 显隐刷新（进入季风城邦显示；离开季风城邦隐藏，不残留季风信息）</summary>
    private void OnCityChanged(CityStateKind oldKind, CityStateKind newKind)
    {
        Refresh();
    }

    // ==========================================
    //  刷新（事件驱动；全量重绘）
    // ==========================================
    /// <summary>全量刷新：非季风城邦隐藏；季风城邦显示「季节·昼夜 | 活跃气候（剩余轮数）」</summary>
    private void Refresh()
    {
        // 内聚过滤（红线：非季风城邦不触发季风显示逻辑——仅做显隐判断，不读取季风状态）
        bool monsoonActive = MonsoonManager.Instance != null && MonsoonManager.Instance.IsMonsoonUIVisible;
        if (!monsoonActive)
        {
            if (root != null) root.SetActive(false);
            else if (statusText != null) statusText.text = string.Empty;   // 无独立显示根：清空文本（空白）
            return;
        }

        if (root != null) root.SetActive(true);
        if (statusText == null) return;

        var manager = MonsoonManager.Instance;

        // 气候列表（与 MonsoonManager 日志同口径：并列 + 剩余轮数；无气候 = 晴）
        var climates = manager.GetActiveClimateSnapshots();
        string climateDesc;
        if (climates.Count == 0)
        {
            climateDesc = "晴";
        }
        else
        {
            var parts = new List<string>(climates.Count);
            foreach (var c in climates)
                parts.Add($"{c.name}（剩余 {c.roundsRemaining} 轮）");
            climateDesc = string.Join("、", parts);
        }

        // 季节·昼夜（同 PhaseDesc 口径，如「春·昼」） + 气候
        statusText.text = $"季风 · {manager.CurrentSeasonName}·{(manager.CurrentIsDay ? "昼" : "夜")} | {climateDesc}";
    }
}
