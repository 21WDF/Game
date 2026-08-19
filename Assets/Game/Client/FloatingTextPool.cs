using System.Collections.Generic;
using UnityEngine;

/// <summary>浮动文字对象池（单例 MonoBehaviour）。
/// 挂场景空 GameObject 上，Inspector 拖入 FloatingText 预制体。
/// 纯 View 层渲染，不参与任何游戏逻辑计算。池未就绪（Instance 为 null / prefab 未赋值）时调用静默跳过。</summary>
public class FloatingTextPool : MonoBehaviour
{
    public static FloatingTextPool Instance { get; private set; }

    [Tooltip("World Space Canvas 的 FloatingText 预制体")]
    public FloatingText prefab;

    private readonly Queue<FloatingText> _pool = new Queue<FloatingText>();
    private GameConfig _config;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _config = Resources.Load<GameConfig>("GameConfig");
        if (_config == null)
            Debug.LogError("[FloatingTextPool] GameConfig 加载失败！请确保 Assets/Game/Resources/GameConfig.asset 存在。", this);

        // 预创建 20 个
        if (prefab != null)
        {
            for (int i = 0; i < 20; i++)
            {
                var item = Instantiate(prefab, transform);
                item.gameObject.SetActive(false);
                _pool.Enqueue(item);
            }
        }
        else
        {
            Debug.LogError("[FloatingTextPool] prefab 未赋值！请在 Inspector 拖入 FloatingText 预制体。", this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>从池中取一个（池空时动态 Instantiate）。取出的已 SetActive(true)。</summary>
    public FloatingText Get()
    {
        if (prefab == null) return null;
        FloatingText item = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(prefab, transform);
        item.gameObject.SetActive(true);
        return item;
    }

    /// <summary>归还（SetActive(false) 入池）</summary>
    public void Return(FloatingText item)
    {
        if (item == null) return;
        item.gameObject.SetActive(false);
        _pool.Enqueue(item);
    }

    // ==========================================
    //  便捷方法
    // ==========================================
    /// <summary>伤害跳字。颜色由调用方传入（元素色或伤害类型色，见 ElementColorMapper）；
    /// 样式按伤害类型自动分流：物理=纯色块 / 魔法=明暗渐变 / 真实=深色描边。
    /// 伤害数字不受「是否触发反应」影响——反应提示由独立的反应名跳字承担（FloatStyles.ReactionDamage）。</summary>
    public void ShowDamage(int dmg, Vector3 pos, Color color, DamageKind kind)
    {
        var style = FloatStyles.Damage(color, kind, _config);
        Get()?.Show(dmg.ToString(), pos, style);
    }

    /// <summary>回血跳字（本阶段定义不找调用点，为未来预留）</summary>
    public void ShowHeal(int amount, Vector3 pos)
    {
        Get()?.Show("+" + amount, pos, FloatStyles.Heal(_config));
    }

    /// <summary>护甲跳字（本阶段定义不找调用点，为未来预留）</summary>
    public void ShowArmor(int amount, Vector3 pos)
    {
        Get()?.Show("+" + amount, pos, FloatStyles.Armor(_config));
    }
}
