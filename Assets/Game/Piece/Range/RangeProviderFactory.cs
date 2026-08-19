using UnityEngine;

/// <summary>
/// 范围提供者工厂 —— 按 className 创建 provider 实例，用 JsonUtility 解析 jsonParams 填充参数。
/// 完全照搬 PieceManager.CreateUltimate / EquipmentManager.CreatePassive 的写法。
/// 新增 provider 类型在此追加 case。
/// </summary>
public static class RangeProviderFactory
{
    // ==========================================
    //  移动范围 Provider
    // ==========================================
    public static IMoveRangeProvider CreateMoveProvider(string className, string jsonParams)
    {
        if (string.IsNullOrEmpty(className)) return null;

        switch (className)
        {
            case "FreeMoveProvider":
                // 无构造参数（effectiveRange 由接口传入）
                return new FreeMoveProvider();

            case "StraightMoveProvider":
                // 无构造参数（effectiveRange 由接口传入）
                return new StraightMoveProvider();

            case "FlyMoveProvider":
                // 飞行移动：BFS 可飞越棋子（被占格作中转），落点仅空格
                return new FlyMoveProvider();

            case "StraightPassProvider":
                // 直线穿越移动：6 方向射线穿过棋子不停，落点仅空格
                return new StraightPassProvider();

            case "ParityMoveProvider":
                // 跳格移动：effectiveRange 内六边形距离为偶数的所有空格（跳跃式，不受阻挡）
                return new ParityMoveProvider();

            default:
                Debug.LogWarning($"[RangeProviderFactory] 未知移动 provider 类名: {className}");
                return null;
        }
    }

    // ==========================================
    //  攻击范围 Provider
    // ==========================================
    public static IAttackRangeProvider CreateAttackProvider(string className, string jsonParams)
    {
        if (string.IsNullOrEmpty(className)) return null;

        string json = string.IsNullOrEmpty(jsonParams) ? "{}" : jsonParams;

        switch (className)
        {
            case "CircleAttackProvider":
                // 无构造参数（effectiveRange 由接口传入）
                return new CircleAttackProvider();

            case "MinMaxAttackProvider":
                var mm = JsonUtility.FromJson<MinMaxParams>(json);
                return new MinMaxAttackProvider(mm.minRange);

            case "AreaAttackProvider":
                // 用 FromJsonOverwrite 保留 AreaSplashParams 字段默认值（spec：splashRadius=1, filterByAttackRange=true 等）
                // —— JsonUtility.FromJson 会把未指定字段重置为 0/false，无法匹配 spec 默认
                var areaParams = new AreaAttackProvider.AreaSplashParams();
                if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, areaParams);
                return new AreaAttackProvider(areaParams);

            case "LineAttackProvider":
                // 无构造参数（effectiveRange 由接口传入）
                return new LineAttackProvider();

            case "ConeAttackProvider":
                var cone = JsonUtility.FromJson<ConeParams>(json);
                return new ConeAttackProvider(cone.angle);

            case "RingTangentProvider":
                // 环切线攻击：目标格 + 同环左右 2 邻居（横向 3 格；供菲林斯/雷电将军大招复用）
                return new RingTangentProvider();

            default:
                Debug.LogWarning($"[RangeProviderFactory] 未知攻击 provider 类名: {className}");
                return null;
        }
    }

    // ==========================================
    //  JsonUtility 反序列化用的参数包装类
    // ==========================================
    [System.Serializable]
    private class MinMaxParams { public int minRange = 0; public int maxRange = 0; }

    [System.Serializable]
    private class ConeParams { public int angle = 60; public int length = 0; }
}
