# -*- coding: utf-8 -*-
"""
生成 Chaotic Chess 的完整 Range 配置资产（MoveRangeConfig / AttackRangeConfig）。
命名语义化中文、不带数字。每个资产生成 .asset + .meta（随机唯一 guid）。
覆盖所有移动/攻击 provider 类型 + 常用范围档位。
"""
import os
import uuid

# 脚本 GUID（来自 MoveRangeConfig.cs.meta / AttackRangeConfig.cs.meta）
MOVE_SCRIPT_GUID = "ef613c650945465582d7c1be39738cf3"
ATTACK_SCRIPT_GUID = "743814c531a542e596b1f884dec598f0"

BASE = "Assets/Game/Resources/Range"
MOVE_DIR = os.path.join(BASE, "Move")
ATTACK_DIR = os.path.join(BASE, "Attack")

# 移动配置：(资产名, providerClassName, baseRange)
MOVES = [
    ("短距移动", "FreeMoveProvider", 2),
    ("标准移动", "FreeMoveProvider", 3),
    ("长距移动", "FreeMoveProvider", 4),
    ("疾行移动", "FreeMoveProvider", 5),
    ("直线移动", "StraightMoveProvider", 4),
    ("直线穿越", "StraightPassProvider", 4),
    ("跳格移动", "ParityMoveProvider", 4),
    ("飞行移动", "FlyMoveProvider", 4),
]

# 攻击配置：(资产名, providerClassName, baseRange, jsonParams)
ATTACKS = [
    ("近战攻击", "CircleAttackProvider", 1, ""),
    ("中距攻击", "CircleAttackProvider", 2, ""),
    ("远程攻击", "CircleAttackProvider", 3, ""),
    ("超远程攻击", "CircleAttackProvider", 4, ""),
    ("直线攻击", "LineAttackProvider", 3, ""),
    ("溅射攻击", "AreaAttackProvider", 2,
     '{"splashRadius":1,"filterByAttackRange":true,"splashIsElemental":false,'
     '"splashGoldPercent":0,"baseDamage":0,"attackPercent":0.5}'),
    ("范围溅射", "AreaAttackProvider", 1,
     '{"splashRadius":2,"filterByAttackRange":true,"splashIsElemental":true,'
     '"splashGoldPercent":0,"baseDamage":0,"attackPercent":0.5}'),
    ("远程狙击", "MinMaxAttackProvider", 5, '{"minRange":2}'),
    ("扇形攻击", "ConeAttackProvider", 3, '{"angle":60}'),
]


def _escape(s):
    """非 ASCII 字符转 \\uXXXX（对齐 Unity 序列化输出，确保 YAML 解析兼容）"""
    return ''.join(c if ord(c) < 128 else '\\u%04x' % ord(c) for c in s)


def _asset_yaml(name, script_guid, class_identifier, provider_class, json_params, base_range):
    json_line = f"  providerJsonParams: '{json_params}'" if json_params else "  providerJsonParams: "
    return (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
        f"  m_Name: {_escape(name)}\n"
        f"  m_EditorClassIdentifier: {class_identifier}\n"
        f"  providerClassName: {provider_class}\n"
        f"{json_line}\n"
        f"  baseRange: {base_range}\n"
    )


def _meta_yaml(guid):
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "MonoImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  defaultReferences: []\n"
        "  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def write_asset(directory, name, script_guid, class_identifier, provider_class, json_params, base_range):
    os.makedirs(directory, exist_ok=True)
    asset_path = os.path.join(directory, f"{name}.asset")
    meta_path = asset_path + ".meta"
    guid = uuid.uuid4().hex
    with open(asset_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(_asset_yaml(name, script_guid, class_identifier, provider_class, json_params, base_range))
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(_meta_yaml(guid))
    return asset_path, guid


def main():
    created = []
    for name, provider, base in MOVES:
        p, g = write_asset(MOVE_DIR, name, MOVE_SCRIPT_GUID,
                           "Assembly-CSharp::MoveRangeConfig", provider, "", base)
        created.append((p, g, "Move", provider, base))
    for name, provider, base, jsonp in ATTACKS:
        p, g = write_asset(ATTACK_DIR, name, ATTACK_SCRIPT_GUID,
                           "Assembly-CSharp::AttackRangeConfig", provider, jsonp, base)
        created.append((p, g, "Attack", provider, base))

    print(f"创建完成，共 {len(created)} 个资产：")
    for p, g, kind, provider, base in created:
        print(f"  [{kind}] {os.path.basename(p)}  guid={g}  provider={provider}  baseRange={base}")


if __name__ == "__main__":
    main()
