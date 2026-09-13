# -*- coding: utf-8 -*-
"""
配置表生成器 → .asset 回写脚本（纯 Python，不依赖 Unity）

读「装备配置生成器.xlsx / 道具配置生成器.xlsx」→ 逐行映射到 .asset →
只更新「语义上发生变化的字段」，其余字节原样保留。

安全机制：
  1. 只改字段值：绝不新建/删除/重建资产，不碰 .meta（guid 原样）；
  2. 保留引用：icon 等「引用型」列在表格中标为 Inspector 修改，脚本跳过，绝不写 fileID/guid；
  3. 语义对比：字段在 .asset 中缺失时视为类默认值（与源码字段默认一致），
     表格值 == 语义值 → 不写（幂等：表格不变 → 文件不变）；
  4. 定点替换：单行字段只替换 `key: ` 之后的值；缺失字段按源码顺序插入一行；
     passives 整体块替换（含多行列表）——其余字节零改动；
  5. 写盘前自动备份被修改资产到 配置表\\_backup_<时间戳>\\（相对 Assets 路径镜像）；
  6. --dry-run 只对比打印 旧值→新值，不写盘。

用法：
  python writeback_generators.py --dry-run          # 干跑：只打印差异
  python writeback_generators.py                    # 正式回写（先备份再写）
  python writeback_generators.py --sheet 装备        # 只处理某张表（装备/道具）
"""
import codecs
import os
import re
import shutil
import sys
import datetime

import openpyxl

CONFIG = r"D:\unity\Project\Chaotic Chess\配置表"
ASSETS = r"D:\unity\Project\Chaotic Chess\Assets"

# ---------- 源码字段 schema（字段名/类型/默认值，逐项对照源码） ----------
EQUIP_TIER = {"Basic": 0, "Intermediate": 1, "Advanced": 2, "CityState": 3}
CITY_KIND = {"None": 0, "Trade": 1, "Merriment": 2, "Monsoon": 3, "Occult": 4, "War": 5, "Element": 6}

# (类型, 默认值, 枚举映射或None)
SCHEMA_EQUIP = [
    ("id", "int", 0, None),
    ("displayName", "str", "Equipment", None),
    ("description", "str", "", None),
    ("tier", "enum", 0, EQUIP_TIER),
    ("cityState", "enum", 0, CITY_KIND),
    ("price", "int", 0, None),
    ("icon", "ref", None, None),
    ("bonusHP", "int", 0, None),
    ("bonusAttack", "int", 0, None),
    ("bonusDefense", "int", 0, None),
    ("bonusMoveRange", "int", 0, None),
    ("bonusAttackRange", "int", 0, None),
    ("bonusEnergyPerTurn", "int", 0, None),
    ("bonusAPCap", "int", 0, None),
    ("passives", "passives", [], None),
]

SCHEMA_GRID = [
    ("id", "int", 0, None),
    ("displayName", "str", "Grid Item", None),
    ("icon", "ref", None, None),
    ("price", "int", 0, None),
    ("apCost", "int", 1, None),
    ("effectClassName", "str", "", None),
    ("effectJsonParams", "str", "", None),
    ("requiresSelectedPiece", "bool", False, None),
    ("minTilesAfter", "int", 7, None),
]

# 生成器 xlsx：表名 → (文件名, schema, 路径列号[1基], 字段列→schema字段名, 说明列)
GENERATORS = [
    ("装备", "装备配置生成器.xlsx", SCHEMA_EQUIP),
    ("道具", "道具配置生成器.xlsx", SCHEMA_GRID),
]


# ---------- .asset 解析（与构建脚本一致） ----------
def decode_str(raw):
    s = raw.strip()
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        return codecs.decode(s[1:-1], "unicode_escape")
    if len(s) >= 2 and s[0] == "'" and s[-1] == "'":
        return codecs.decode(s[1:-1], "unicode_escape")
    return s


def parse_asset(path):
    """返回 {field: 原始行值字符串} + passives 列表（元素为 dict，值=原始行值字符串）"""
    with open(path, encoding="utf-8") as f:
        lines = f.read().split("\n")
    fields = {}
    passives = []
    cur = None
    in_list = False
    j = 0
    while j < len(lines) and "MonoBehaviour:" not in lines[j]:
        j += 1
    if j >= len(lines):
        return fields, passives, lines
    j += 1
    for line in lines[j:]:
        if line.startswith("---"):
            break
        if not line.strip():
            continue
        stripped = line.strip()
        indent = len(line) - len(line.lstrip())
        if indent == 2 and stripped.startswith("- "):
            in_list = True
            cur = {}
            passives.append(cur)
            # 兼容 `- className: X` 内联首字段（dash 与字段同行）
            rest = stripped[2:].strip()
            m2 = re.match(r"^([A-Za-z_]\w*):\s*(.*)$", rest)
            if m2:
                cur[m2.group(1)] = m2.group(2)
            continue
        if indent == 2:
            m = re.match(r"^([A-Za-z_]\w*):\s*(.*)$", stripped)
            if m:
                key, val = m.group(1), m.group(2)
                if key == "passives" and val.strip() == "[]":
                    passives = []
                fields[key] = val
                in_list = False
            continue
        if indent >= 4 and in_list and cur is not None:
            m = re.match(r"^([A-Za-z_]\w*):\s*(.*)$", stripped)
            if m:
                cur[m.group(1)] = m.group(2)
    return fields, passives, lines


# ---------- 语义值读取 ----------
def sem_int(fields, key, default):
    raw = fields.get(key)
    if raw is None or raw.strip() == "":
        return default
    try:
        return int(raw)
    except ValueError:
        return default


def sem_bool(fields, key, default):
    raw = fields.get(key)
    if raw is None or raw.strip() == "":
        return default
    try:
        return int(raw) != 0
    except ValueError:
        return str(raw).strip().lower() == "true"


def sem_str(fields, key, default):
    raw = fields.get(key)
    if raw is None or raw.strip() == "":
        return default
    return decode_str(raw)


def sem_enum(fields, key, default, mapping):
    v = sem_int(fields, key, default)
    return v


def sem_passives(passives):
    """asset 的 passives → 规范化列表 [{className,jsonParams,passiveName,isUnique(bool),uniqueId}]"""
    out = []
    for p in passives:
        out.append({
            "className": sem_str(p, "className", ""),
            "jsonParams": sem_str(p, "jsonParams", ""),
            "passiveName": sem_str(p, "passiveName", ""),
            "isUnique": sem_bool(p, "isUnique", False),
            "uniqueId": sem_str(p, "uniqueId", ""),
        })
    return out


# ---------- Excel 值解析 ----------
def cell_str(v):
    if v is None:
        return ""
    return str(v).strip()


def xl_int(v, default):
    s = cell_str(v)
    if s == "":
        return default
    try:
        return int(float(s))
    except ValueError:
        return default


def xl_bool(v, default):
    s = cell_str(v)
    if s == "":
        return default
    return s.lower() in ("true", "1", "是")


def xl_enum(v, default, mapping):
    s = cell_str(v)
    if s == "":
        return default
    if s.isdigit():
        return int(s)
    return mapping.get(s, default)


def xl_passives(v):
    """分隔符约定：; 分隔多个被动；子字段 | 分隔 className|jsonParams|passiveName|isUnique|uniqueId"""
    s = cell_str(v)
    if s == "":
        return []
    out = []
    for tok in s.split(";"):
        parts = [p.strip() for p in tok.split("|")]
        if len(parts) == 1 and parts[0] == "":
            continue
        parts = (parts + [""] * 5)[:5]
        out.append({
            "className": parts[0],
            "jsonParams": parts[1],
            "passiveName": parts[2],
            "isUnique": parts[3].lower() in ("true", "1"),
            "uniqueId": parts[4],
        })
    return out


# ---------- 字符串写回编码（Unity YAML 兼容） ----------
def encode_str(s):
    if s == "":
        return '""'
    if any(ord(c) > 0x7F for c in s):
        esc = "".join("\\u%04X" % ord(c) if ord(c) > 0x7F else
                      ("\\\\" if c == "\\" else ('\\"' if c == '"' else c)) for c in s)
        return '"' + esc + '"'
    if s != s.strip() or any(ch in s for ch in "'\"{}:#,[]"):
        return "'" + s.replace("'", "''") + "'"
    return s


def encode_scalar(kind, value):
    if kind == "int":
        return str(int(value))
    if kind == "bool":
        return "1" if value else "0"
    if kind == "enum":
        return str(int(value))
    if kind == "float":
        f = float(value)
        return str(int(f)) if f == int(f) else repr(f)
    if kind == "str":
        return encode_str(value)
    return ""


# ---------- 字段定点替换 ----------
def replace_or_insert(lines, key, new_line_value, kind, value):
    """在 lines（列表）中替换/插入字段行。返回修改后的 lines。new_line_value 为写好的值串。"""
    for i, line in enumerate(lines):
        if re.match(r"^  %s:\s" % re.escape(key), line) or re.match(r"^  %s:$" % re.escape(key), line):
            lines[i] = "  %s: %s" % (key, new_line_value)
            return lines
    # 缺失 → 插入到最后一个顶层字段行之后（passives 块之后）
    insert_at = len(lines)
    for i in range(len(lines) - 1, -1, -1):
        stripped = lines[i].strip()
        if stripped.startswith("---"):
            insert_at = i
            continue
        if re.match(r"^[A-Za-z_]\w*:", stripped) or re.match(r"^- ", stripped):
            insert_at = i + 1
            break
    lines.insert(insert_at, "  %s: %s" % (key, new_line_value))
    return lines


def replace_passives_block(lines, new_text):
    """替换整个 passives 块（从 `  passives:` 行到下一个顶层字段/块结束）。new_text 为多行字符串。"""
    start = None
    for i, line in enumerate(lines):
        if re.match(r"^  passives:\s*$", line) or re.match(r"^  passives: \[\]", line):
            start = i
            break
    if start is None:
        # 缺失 passives → 插到块尾
        insert_at = len(lines)
        for i in range(len(lines) - 1, -1, -1):
            stripped = lines[i].strip()
            if stripped.startswith("---"):
                insert_at = i
                continue
            if re.match(r"^[A-Za-z_]\w*:", stripped) or re.match(r"^- ", stripped):
                insert_at = i + 1
                break
        return lines[:insert_at] + new_text.split("\n") + lines[insert_at:]
    end = start + 1
    while end < len(lines):
        stripped = lines[end].strip()
        if not stripped:
            end += 1
            continue
        indent = len(lines[end]) - len(lines[end].lstrip())
        if indent == 2 and not stripped.startswith("-"):
            break  # 下一个顶层字段
        end += 1
    return lines[:start] + new_text.split("\n") + lines[end:]


def build_passives_text(passives):
    if not passives:
        return "  passives: []"
    out = ["  passives:"]
    for p in passives:
        out.append("  - className: %s" % encode_str(p["className"]))
        out.append("    jsonParams: %s" % encode_str(p["jsonParams"]))
        out.append("    passiveName: %s" % encode_str(p["passiveName"]))
        out.append("    isUnique: %s" % ("1" if p["isUnique"] else "0"))
        out.append("    uniqueId: %s" % encode_str(p["uniqueId"]))
    return "\n".join(out)


# ---------- 主流程 ----------
def run(sheet_filter, dry_run):
    changes = []          # (asset_path, field, old_disp, new_disp)
    errors = []

    for sheet_name, fname, schema in GENERATORS:
        if sheet_filter and sheet_filter not in sheet_name:
            continue
        wb = openpyxl.load_workbook(os.path.join(CONFIG, fname))
        ws = wb.active
        headers = [cell_str(ws.cell(row=2, column=c).value) for c in range(1, ws.max_column + 1)]
        # 列号 → 字段：按字段名长度降序匹配，先长后短，避免 bonusAttack 误吞 bonusAttackRange
        # 匹配规则：表头 == 字段名，或 表头以「字段名 + （/空格」开头（如 passives（...））
        col_to_field = {}
        schema_sorted = sorted(schema, key=lambda s: len(s[0]), reverse=True)
        for ci, h in enumerate(headers, start=1):
            hs = h.strip()
            for (fname_, kind, default, mapping) in schema_sorted:
                if kind == "ref":
                    continue
                if hs == fname_ or hs.startswith(fname_ + "（") or hs.startswith(fname_ + " "):
                    col_to_field[ci] = (fname_, kind, default, mapping)
                    break
        path_col = next((ci for ci, h in enumerate(headers, start=1)
                         if "目标资产路径" in h), 1)

        for r in range(3, ws.max_row + 1):
            path = cell_str(ws.cell(row=r, column=path_col).value)
            if not path:
                continue
            full = os.path.normpath(os.path.join(ASSETS, path.replace("/", "\\")))
            if not os.path.isfile(full):
                errors.append(f"[{sheet_name}] 资产不存在：{path}")
                continue
            fields, passives, lines = parse_asset(full)

            for ci, (fname_, kind, default, mapping) in col_to_field.items():
                cell = ws.cell(row=r, column=ci).value
                # 当前语义值
                if kind == "int":
                    cur = sem_int(fields, fname_, default)
                    new = xl_int(cell, cur if cell_str(cell) == "" else default)
                elif kind == "bool":
                    cur = sem_bool(fields, fname_, default)
                    new = xl_bool(cell, cur)
                elif kind == "enum":
                    cur = sem_enum(fields, fname_, default, mapping)
                    new = xl_enum(cell, cur, mapping)
                elif kind == "str":
                    cur = sem_str(fields, fname_, default)
                    new = cell_str(cell) if cell_str(cell) != "" else cur
                elif kind == "passives":
                    cur = sem_passives(passives)
                    new = xl_passives(cell)
                else:
                    continue
                if new == cur:
                    continue
                changes.append((full, fname_, kind, cur, new))

        wb.close()

    # ---- 输出差异 ----
    if not changes:
        print("[writeback] 无差异（表格内容与资产一致，幂等）。")
        if errors:
            print("\n".join(errors))
        return 0

    for full, fname_, kind, cur, new in changes:
        rel = os.path.relpath(full, ASSETS).replace("\\", "/")
        if kind == "passives":
            print(f"  {rel} :: {fname_}（{len(cur)} 项→{len(new)} 项）")
        else:
            print(f"  {rel} :: {fname_}: {cur} → {new}")

    if dry_run:
        print(f"\n[dry-run] 共 {len(set(c[0] for c in changes))} 个资产、{len(changes)} 处字段差异。未写盘。")
        return 0

    # ---- 备份 ----
    ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    backup_dir = os.path.join(CONFIG, f"_backup_{ts}")
    changed_paths = sorted({c[0] for c in changes})
    for full in changed_paths:
        rel = os.path.relpath(full, ASSETS)
        dest = os.path.join(backup_dir, rel)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copy2(full, dest)
    print(f"\n[writeback] 已备份 {len(changed_paths)} 个资产 → {backup_dir}")

    # ---- 写盘（按资产分组，逐字段） ----
    by_asset = {}
    for c in changes:
        by_asset.setdefault(c[0], []).append(c)

    for full, cl in by_asset.items():
        _f, passives, lines = parse_asset(full)
        for _a, fname_, kind, cur, new in cl:
            if kind == "passives":
                lines = replace_passives_block(lines, build_passives_text(new))
            else:
                if kind == "enum":
                    # 反查枚举名 → 代码（cur 已是 int）
                    lines = replace_or_insert(lines, fname_, encode_scalar("int", new), kind, new)
                else:
                    lines = replace_or_insert(lines, fname_, encode_scalar(kind, new), kind, new)
        with open(full, "w", encoding="utf-8", newline="") as f:
            f.write("\n".join(lines))

    print(f"[writeback] 已修改 {len(by_asset)} 个资产（{len(changes)} 处字段）。可用 git 回滚或从 _backup_{ts} 恢复。")
    return 0


def main():
    args = sys.argv[1:]
    sheet_filter = None
    dry_run = "--dry-run" in args
    if "--sheet" in args:
        sheet_filter = args[args.index("--sheet") + 1]
    sys.exit(run(sheet_filter, dry_run))


if __name__ == "__main__":
    main()
