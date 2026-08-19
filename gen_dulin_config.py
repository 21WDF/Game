# -*- coding: utf-8 -*-
"""生成「杜林_配置生成器.xlsx」—— 复用「被动效果配置生成器.xlsx」的样式与公式写法。
4 张表：
1. 杜林·被动配置生成器（FlameNovaPassive 炎星溅射）
2. 杜林·大招配置生成器（FlameLanceUltimate 贯日炎枪）
3. 杜林·效果速查（被动 + 大招）
4. 杜林·棋子配置清单（PieceData asset 字段）
公式用 CHAR(34) 生成 JSON 引号，避免 Excel "" 转义。
"""
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.worksheet.datavalidation import DataValidation

OUT = r"D:\unity\Project\Chaotic Chess\杜林_配置生成器.xlsx"

# ---------- 样式（与被动效果配置生成器.xlsx 一致） ----------
F_TITLE = Font(name="微软雅黑", size=16, bold=True, color="1F3864")
F_SUB   = Font(name="微软雅黑", size=10, color="595959")
F_BLOCK = Font(name="微软雅黑", size=12, bold=True, color="FFFFFF")
F_HEAD  = Font(name="微软雅黑", size=10, bold=True, color="FFFFFF")
F_BODY  = Font(name="微软雅黑", size=10, color="222222")
F_FIELD = Font(name="Consolas", size=10, bold=True, color="1F3864")
F_GEN   = Font(name="Consolas", size=10, bold=True, color="006100")

FILL_BLOCK = PatternFill("solid", fgColor="4472C4")
FILL_HEAD  = PatternFill("solid", fgColor="5B9BD5")
FILL_INPUT = PatternFill("solid", fgColor="FFF2CC")   # 黄色 = 可填写
FILL_DEF   = PatternFill("solid", fgColor="F2F2F2")   # 灰色 = 默认参考
FILL_GEN   = PatternFill("solid", fgColor="E2EFDA")   # 绿色 = 自动生成
FILL_TITLE = PatternFill("solid", fgColor="D9E1F2")

thin = Side(style="thin", color="BFBFBF")
BORDER = Border(left=thin, right=thin, top=thin, bottom=thin)

CENTER = Alignment(horizontal="center", vertical="center")
LEFT   = Alignment(horizontal="left", vertical="center", wrap_text=True)


# ---------- 杜林数据 ----------
PASSIVE = {
    "name": "炎星溅射（杜林被动）", "className": "FlameNovaPassive",
    "desc": "普攻命中后，对自身周围 splashRadius 格内敌方造成一段物理伤害（同帧去重，一次普攻只触发一段；走统一伤害入口，不附着元素/不触发反应/不给能量）",
    "params": [
        ("baseDamage", "int", "基础伤害", 0, "溅射基础伤害数值"),
        ("attackPercent", "int", "攻击力百分比", 50, "按攻击力比例溅射(0-100)"),
        ("splashRadius", "int", "溅射半径", 2, "以自身为圆心的溅射半径(格)"),
        ("goldPercent", "float", "金币比例", 0, "溅射伤害转金币比例(0=不产金币)"),
    ],
}

ULTIMATE = {
    "name": "贯日炎枪（杜林大招）", "className": "FlameLanceUltimate",
    "desc": "指定格模式（targetMode=Tile），点选格子定方向，沿最近 hex 方向直线穿透 length 格内所有敌方（含火元素反应，受防御减伤）",
    "params": [
        ("length", "int", "射线长度", 6, "直线穿透格数(需与 ultimateConfig.tileTargetRange 一致)"),
        ("bonusDamage", "int", "附加攻击力", 0, "附加在 EffectiveAttack 上的额外攻击力"),
    ],
}

LOOKUP = [
    ("FlameNovaPassive", "炎星溅射（被动）", "✅已实现", "baseDamage, attackPercent, splashRadius, goldPercent", "普攻命中后自身周围2格溅射物理伤害"),
    ("FlameLanceUltimate", "贯日炎枪（大招）", "✅已实现", "length, bonusDamage", "指定方向直线6格穿透物理伤害(含火元素反应)"),
]

# PieceData asset 配置清单（4 列：字段 / 值 / 说明）
ASSET_ROWS = [
    ("attackConfig.providerClassName", "LineAttackProvider", "普攻直线穿透（选定方向3格）"),
    ("attackConfig.baseRange", "3", "普攻直线长度"),
    ("innateElement", "Fire", "攻击时作为触发元素参与反应"),
    ("builtInPassives[0].className", "FlameNovaPassive", "内建被动：炎星溅射"),
    ("builtInPassives[0].jsonParams", '{"baseDamage":0,"attackPercent":50,"splashRadius":2,"goldPercent":0}', "被动参数（见被动配置生成器）"),
    ("ultimateConfig.targetMode", "Tile", "指定格瞄准（可点空格定方向）"),
    ("ultimateConfig.tileTargetRange", "6", "大招瞄准高亮半径（需与 length 一致）"),
    ("ultimateConfig.effectClassName", "FlameLanceUltimate", "大招效果类"),
    ("ultimateConfig.effectJsonParams", '{"length":6,"bonusDamage":0}', "大招参数（见大招配置生成器）"),
]


def build_json_formula(cells, fields, quoted_fields=None):
    quoted = set(quoted_fields or [])
    parts = ['="{"']
    for i, (cell, field) in enumerate(zip(cells, fields)):
        if i > 0:
            parts.append('","')
        parts.append('&CHAR(34)&"' + field + '"&CHAR(34)&":"&')
        if field in quoted:
            parts.append('CHAR(34)&' + cell + '&CHAR(34)&')
        else:
            parts.append(cell + '&')
    parts.append('"}"')
    return ''.join(parts)


def build_config_sheet(wb, title, effect, first):
    """生成单个效果的「配置生成器」sheet。"""
    ws = wb.active if first else wb.create_sheet(title)
    ws.title = title
    for col, w in {"A": 22, "B": 12, "C": 14, "D": 10, "E": 46}.items():
        ws.column_dimensions[col].width = w

    ws["A1"] = title
    ws["A1"].font = F_TITLE
    ws.merge_cells("A1:E1")
    ws["A1"].fill = FILL_TITLE

    ws["A2"] = "使用说明：在【填写值】列（黄色底）填数值，下方【jsonParams】自动生成配置 JSON；把 className 和 jsonParams 分别复制到 Unity 的对应配置即可。"
    ws["A2"].font = F_SUB
    ws.merge_cells("A2:E2")

    ws["A3"] = "杜林 · 火元素 · 直线穿透输出"
    ws["A3"].font = F_SUB
    ws.merge_cells("A3:E3")

    row = 5
    c = ws.cell(row, 1, f"◆ {effect['name']}  {effect['className']}")
    c.font = F_BLOCK
    for cc in range(1, 6):
        ws.cell(row, cc).fill = FILL_BLOCK
    ws.cell(row, 5, effect["desc"]).font = Font(name="微软雅黑", size=9, color="FFFFFF")
    ws.merge_cells(start_row=row, start_column=5, end_row=row, end_column=5)

    heads = ["参数", "类型", "填写值", "默认值", "说明"]
    for i, h in enumerate(heads, 1):
        cell = ws.cell(row + 1, i, h)
        cell.font = F_HEAD
        cell.fill = FILL_HEAD
        cell.alignment = CENTER
        cell.border = BORDER

    r = row + 2
    first_param_row = r
    for (field, typ, label, default, note) in effect["params"]:
        a = ws.cell(r, 1, field)
        a.font = F_FIELD
        a.border = BORDER
        ws.cell(r, 2, typ).alignment = CENTER
        ws.cell(r, 2).border = BORDER
        inp = ws.cell(r, 3, default)
        inp.fill = FILL_INPUT
        inp.alignment = CENTER
        inp.border = BORDER
        ws.cell(r, 4, default).fill = FILL_DEF
        ws.cell(r, 4).alignment = CENTER
        ws.cell(r, 4).border = BORDER
        ws.cell(r, 5, note).border = BORDER
        ws.cell(r, 5).alignment = LEFT
        r += 1

    # className 行
    ws.cell(r, 1, "className").font = Font(name="微软雅黑", size=10, bold=True)
    cls_cell = ws.cell(r, 3, effect["className"])
    cls_cell.font = F_GEN
    cls_cell.fill = FILL_GEN
    cls_cell.alignment = CENTER
    cls_cell.border = BORDER
    ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=5)
    for cc in (3, 4, 5):
        ws.cell(r, cc).fill = FILL_GEN

    # jsonParams 行（公式）
    r2 = r + 1
    fields = [pp[0] for pp in effect["params"]]
    cells = [f"C{first_param_row + i}" for i in range(len(fields))]
    formula = build_json_formula(cells, fields, [])
    ws.cell(r2, 1, "jsonParams").font = Font(name="微软雅黑", size=10, bold=True)
    jp = ws.cell(r2, 3, formula)
    jp.font = F_GEN
    jp.fill = FILL_GEN
    jp.alignment = Alignment(horizontal="left", vertical="center", wrap_text=True)
    jp.border = BORDER
    ws.merge_cells(start_row=r2, start_column=3, end_row=r2, end_column=5)
    for cc in (3, 4, 5):
        ws.cell(r2, cc).fill = FILL_GEN
    ws.row_dimensions[r2].height = 30


def build_lookup_sheet(wb, title, lookup):
    ws = wb.create_sheet(title)
    for col, width in {"A": 24, "B": 22, "C": 12, "D": 44, "E": 44}.items():
        ws.column_dimensions[col].width = width
    ws["A1"] = title
    ws["A1"].font = F_TITLE
    ws.merge_cells("A1:E1")
    ws["A1"].fill = FILL_TITLE
    heads = ["类名", "中文名", "状态", "参数", "说明"]
    for i, h in enumerate(heads, 1):
        cell = ws.cell(2, i, h)
        cell.font = F_HEAD
        cell.fill = FILL_HEAD
        cell.alignment = CENTER
        cell.border = BORDER
    for ri, rec in enumerate(lookup, start=3):
        for ci, val in enumerate(rec, 1):
            cell = ws.cell(ri, ci, val)
            cell.font = F_BODY
            cell.border = BORDER
            cell.alignment = LEFT if ci in (1, 2, 4, 5) else CENTER
    ws.freeze_panes = "A3"


def build_asset_sheet(wb, title, rows):
    ws = wb.create_sheet(title)
    for col, width in {"A": 40, "B": 46, "C": 46}.items():
        ws.column_dimensions[col].width = width
    ws["A1"] = title
    ws["A1"].font = F_TITLE
    ws.merge_cells("A1:C1")
    ws["A1"].fill = FILL_TITLE
    ws["A2"] = "杜林 PieceData.asset 在 Unity Inspector 中的字段填写清单（代码无需改动，仅配置）"
    ws["A2"].font = F_SUB
    ws.merge_cells("A2:C2")
    heads = ["字段", "填写值", "说明"]
    for i, h in enumerate(heads, 1):
        cell = ws.cell(3, i, h)
        cell.font = F_HEAD
        cell.fill = FILL_HEAD
        cell.alignment = CENTER
        cell.border = BORDER
    for ri, rec in enumerate(rows, start=4):
        for ci, val in enumerate(rec, 1):
            cell = ws.cell(ri, ci, val)
            cell.font = F_BODY
            cell.border = BORDER
            cell.alignment = LEFT
    ws.freeze_panes = "A4"


# ============================================================
#  生成 4 张表
# ============================================================
wb = openpyxl.Workbook()
build_config_sheet(wb, "杜林·被动配置生成器", PASSIVE, first=True)
build_config_sheet(wb, "杜林·大招配置生成器", ULTIMATE, first=False)
build_lookup_sheet(wb, "杜林·效果速查", LOOKUP)
build_asset_sheet(wb, "杜林·棋子配置清单", ASSET_ROWS)

wb.save(OUT)
print("SAVED:", OUT)
