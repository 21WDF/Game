# -*- coding: utf-8 -*-
"""生成「被动效果配置生成器.xlsx」—— 4 张表：
1. 通用被动配置生成器 / 2. 元素被动配置生成器 / 3. 通用被动速查 / 4. 元素被动速查
公式用 CHAR(34) 生成 JSON 引号，避免 Excel 的 "" 转义语法（腾讯文档/WPS 预览兼容性更好）。
"""
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.worksheet.datavalidation import DataValidation

OUT = r"D:\unity\Project\Chaotic Chess\被动效果配置生成器.xlsx"

# ---------- 样式 ----------
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

# ---------- 被动数据 ----------
# cat: "通用" = 通用装备被动（Equipment/Effects/）；"元素城邦" = 元素城邦被动（Equipment/Effects/Elemental/）
# params: field / type(int|bool|enum*) / label / default / note
PASSIVES = [
    {
        "cat": "通用",
        "name": "附加伤害", "className": "ExtraDamagePassive",
        "desc": "攻击时追加一段独立伤害 +amount，kind 决定类型：物理吃防御减伤/触发反甲，魔法/真实不吃（默认真实）",
        "params": [
            ("amount", "int", "附加伤害值", 3, "额外伤害数值"),
            ("kind", "enum_kind", "伤害类型", "True", "下拉选 Physical=物理(吃防御减伤,触发反甲) / Magical=魔法 / True=真实(默认)"),
        ],
    },
    {
        "cat": "通用",
        "name": "吸血（饮血剑）", "className": "LifestealPassive",
        "desc": "攻击造成伤害后回血：baseHeal + 伤害×percent%",
        "params": [
            ("baseHeal", "int", "固定吸血量", 0, "每次攻击固定回血量"),
            ("percent", "int", "吸血百分比", 30, "按伤害比例吸血(0-100)"),
        ],
    },
    {
        "cat": "通用",
        "name": "减伤", "className": "DamageReductionPassive",
        "desc": "受到伤害时最终伤害 -amount（下限 1），filter 筛选生效类型（默认全类型）",
        "params": [
            ("amount", "int", "减伤值", 5, "减免的伤害数值"),
            ("filter", "enum_filter", "筛选类型", "All", "下拉选 All=全类型 / Physical=仅物理 / Magical=仅魔法 / True=仅真实"),
        ],
    },
    {
        "cat": "通用",
        "name": "再生（生命铠甲）", "className": "RegenPassive",
        "desc": "回合开始回血；连续未受伤 upgradeThreshold 回合后进化为 upgradedAmount",
        "params": [
            ("baseAmount", "int", "基础回血", 1, "每回合基础回血量"),
            ("upgradedAmount", "int", "进化回血", 3, "进化后每回合回血量"),
            ("upgradeThreshold", "int", "进化阈值", 4, "连续未受伤回合数阈值"),
        ],
    },
    {
        "cat": "通用",
        "name": "反伤（荆棘刺甲）", "className": "ThornsPassive",
        "desc": "受到物理伤害后反伤 amount + 受击伤害×percent%；可配置范围/防御减伤（只反物理，反出魔法天然防递归）",
        "params": [
            ("amount", "int", "基础反伤值", 8, "基础反伤数值"),
            ("percent", "int", "比例反伤%", 0, "按受击伤害比例反伤(0-100)"),
            ("affectedByDefense", "bool", "被防御减伤", "false", "下拉选 true/false：true=反伤被目标防御减伤；false=真实伤害"),
            ("reflectRadius", "int", "反伤范围", 0, "反伤半径(0=只反攻击者；≥1=范围反伤)"),
            ("alwaysReflectAttacker", "bool", "攻击者必反", "false", "下拉选 true/false：true=攻击者必反；false=只反半径内敌人"),
        ],
    },
    {
        "cat": "通用",
        "name": "击杀加攻（死亡之刃）", "className": "KillAttackPassive",
        "desc": "击杀棋子后攻击力永久 +amount（叠层跟随装备，卸下清除）",
        "params": [
            ("amount", "int", "击杀攻击加成", 10, "每次击杀的攻击加成"),
        ],
    },
    {
        "cat": "通用",
        "name": "石像鬼板甲", "className": "StoneSkinPassive",
        "desc": "被不同敌人攻击叠防 defenseBonus，计时 timerTurns 回合，计时结束/卸下清空",
        "params": [
            ("defenseBonus", "int", "每次叠防", 5, "每次被新敌人攻击的防御加成"),
            ("timerTurns", "int", "计时器回合", 4, "计时器持续回合数"),
            ("triggerOnNoDamage", "bool", "无论减血触发", "true", "下拉选 true/false：true=无论是否减血都叠层；false=仅减血叠层"),
        ],
    },
    {
        "cat": "通用",
        "name": "枯萎宝珠", "className": "WitherPassive",
        "desc": "攻击命中后目标防御 -defenseReduction，持续 duration 回合",
        "params": [
            ("defenseReduction", "int", "减防值", 3, "目标防御降低值"),
            ("duration", "int", "持续回合", 2, "减防持续回合数"),
        ],
    },
    {
        "cat": "通用",
        "name": "英灵幡", "className": "SpiritBannerPassive",
        "desc": "己方棋子死亡：立刻回蓝 instantEnergy + 之后每回合回蓝 regenEnergyPerTurn 持续 durationTurns 回合",
        "params": [
            ("instantEnergy", "int", "立刻回蓝", 20, "己方棋子死亡时立刻回蓝量"),
            ("regenEnergyPerTurn", "int", "回合回蓝", 5, "之后每回合回蓝量"),
            ("durationTurns", "int", "持续回合", 3, "持续回蓝的回合数"),
        ],
    },
    {
        "cat": "通用",
        "name": "免AP（探索者护臂/诸葛连弩）", "className": "FreeAPPassive",
        "desc": "移动或攻击完全免 AP（AP=0 也能用），触发后进入 cooldownTurns 回合冷却",
        "params": [
            ("freeType", "enum", "免AP类型", "Move", "下拉选 Move=移动免AP（探索者护臂）/ Attack=攻击免AP（诸葛连弩）"),
            ("cooldownTurns", "int", "冷却回合", 4, "免AP的冷却回合数"),
            ("gainEnergy", "bool", "充能开关", "false", "下拉选 true/false：免AP移动/连射攻击是否获得充能"),
        ],
    },
    {
        "cat": "通用",
        "name": "死亡之蔑", "className": "DeathDancePassive",
        "desc": "受击伤害 delayPercent% 延迟到后续 delayTurns 回合平分结算；realTimeDamage 开关控制是否随防御变化微调；延迟期间禁止卸下",
        "params": [
            ("delayPercent", "int", "延迟比例", 50, "受击伤害的 X% 延迟(1-99)"),
            ("delayTurns", "int", "延迟回合", 2, "受击后 N 回合内平分结算"),
            ("realTimeDamage", "bool", "实时伤害", "false", "下拉选 true/false：true=随防御变化微调；false=固定"),
        ],
    },
    {
        "cat": "通用",
        "name": "首伤=1（中娅悖论）", "className": "FirstDamageToOnePassive",
        "desc": "冷却完毕时受击伤害降为 1，进入冷却（中娅悖论两条被动之一）",
        "params": [
            ("cooldownTurns", "int", "冷却回合", 4, "首伤=1 的冷却回合数"),
        ],
    },
    {
        "cat": "通用",
        "name": "免死（中娅悖论）", "className": "DeathDefyPassive",
        "desc": "受到致命伤时回 1 HP 免死一次，进入冷却（中娅悖论两条被动之一）",
        "params": [
            ("cooldownTurns", "int", "冷却回合", 16, "免死的冷却回合数"),
        ],
    },
    {
        "cat": "通用",
        "name": "军团圣盾（光环+进化）", "className": "LegionAegisPassive",
        "desc": "光环给范围内友军/敌军加属性；连续未受伤 evolveTurns 回合进化。唯一被动需在 PassiveConfig 另填 isUnique=true + uniqueId + passiveName（不进 jsonParams）",
        "params": [
            ("affectDirection", "enum_dir", "作用方向", "Ally", "下拉选 Ally=己方 / Enemy=敌方"),
            ("affectRange", "int", "作用范围", 2, "HexCoord 距离 ≤ 该值"),
            ("bonusStat", "enum_stat", "加成属性", "Defense", "下拉选 Attack / Defense"),
            ("bonusAmount", "int", "加成数值", 5, "加成数值(可为负值)"),
            ("evolveTurns", "int", "进化回合", 4, "-1=不进化；0=立即进化；>0=连续未受伤回合数"),
            ("evolvedDirection", "enum_dir", "进化方向", "Ally", "进化后作用方向"),
            ("evolvedRange", "int", "进化范围", 3, "进化后作用范围"),
            ("evolvedStat", "enum_stat", "进化属性", "Defense", "进化后加成属性"),
            ("evolvedAmount", "int", "进化数值", 10, "进化后加成数值(可为负值)"),
            ("evolveOverrides", "bool", "覆盖开关", "true", "下拉选 true=覆盖旧光环；false=新旧叠加"),
        ],
    },
    {
        "cat": "元素城邦",
        "name": "光界之力", "className": "ReactionBoostPassive",
        "desc": "发生元素反应时倍率 +percent%（加法叠加，非唯一可叠加；无反应普攻不加成）",
        "params": [
            ("percent", "int", "倍率加成%", 25, "反应倍率加成百分比(加法叠加)"),
        ],
    },
    {
        "cat": "元素城邦",
        "name": "四元素之力（潮涌/灼火/陨雷/凝冰）", "className": "ElementalCorePassive",
        "desc": "四件元素之力共用：按 element 区分元素，反应增强 + 元素量加成 + 超载爆炸。唯一被动需在 PassiveConfig 另填 isUnique=true + uniqueId(四件都填 ElementalCore) + passiveName",
        "params": [
            ("element", "enum_element", "元素", "Water", "下拉选 Water=潮涌 / Fire=灼火 / Thunder=陨雷 / Ice=凝冰"),
            ("multiplierPercent", "int", "倍率加成%", 20, "对应反应倍率加成百分比"),
            ("electroChargeTurns", "int", "感电延长", 2, "感电 DoT 延长回合"),
            ("freezeTurns", "int", "冻结延长", 1, "冻结延长回合"),
            ("superconductDefense", "int", "超导减防", 5, "超导减防加成值"),
            ("superconductTurns", "int", "超导持续", 2, "超导减防持续延长回合"),
            ("extraGauge", "int", "元素量加成", 1, "对应元素普攻额外元素量"),
            ("explosionRadius", "int", "爆炸范围", 1, "超载爆炸范围(距离≤该值)"),
            ("explosionBase", "int", "爆炸基础", 1, "爆炸基础伤害"),
            ("explosionDivisor", "int", "爆炸除数", 5, "每 divisor 点伤害 +1"),
        ],
    },
]

# ---------- 速查表（按分类分两张） ----------
LOOKUP_COMMON = [
    ("ExtraDamagePassive", "附加伤害", "✅已实现", "amount, kind", "攻击追加独立伤害，kind 决定类型(默认真实)"),
    ("LifestealPassive", "吸血/饮血剑", "✅已实现", "baseHeal, percent", "攻击后回 baseHeal+伤害×percent%"),
    ("DamageReductionPassive", "减伤", "✅已实现", "amount, filter", "受击时伤害-amount(下限1)，filter 筛选类型"),
    ("RegenPassive", "再生/生命铠甲", "✅已实现", "baseAmount, upgradedAmount, upgradeThreshold", "回合回血，连续未受伤N回合进化"),
    ("ThornsPassive", "反伤/荆棘刺甲", "✅已实现", "amount, percent, affectedByDefense, reflectRadius, alwaysReflectAttacker", "只反物理伤害，可配置范围/防御减伤"),
    ("KillAttackPassive", "击杀加攻/死亡之刃", "✅已实现", "amount", "击杀后攻击永久+amount(叠层随装备)"),
    ("StoneSkinPassive", "石像鬼板甲", "✅已实现", "defenseBonus, timerTurns, triggerOnNoDamage", "被不同敌人攻击叠防，计时归零清空"),
    ("WitherPassive", "枯萎宝珠", "✅已实现", "defenseReduction, duration", "攻击命中目标减防(debuff)"),
    ("SpiritBannerPassive", "英灵幡", "✅已实现", "instantEnergy, regenEnergyPerTurn, durationTurns", "己方死亡立刻回蓝+持续回蓝"),
    ("FreeAPPassive", "免AP/探索者护臂·诸葛连弩", "✅已实现", "freeType, cooldownTurns, gainEnergy", "移动免AP/攻击连射，4回合冷却，可开关充能"),
    ("DeathDancePassive", "死亡之蔑", "✅已实现", "delayPercent, delayTurns, realTimeDamage", "受击伤害延迟到后续回合平分结算，可随防御实时微调"),
    ("FirstDamageToOnePassive", "首伤=1/中娅悖论", "✅已实现", "cooldownTurns", "冷却内受击伤害降为1"),
    ("DeathDefyPassive", "免死/中娅悖论", "✅已实现", "cooldownTurns", "致命伤回1HP免死一次，冷却"),
    ("LegionAegisPassive", "军团圣盾", "✅已实现", "affectDirection, affectRange, bonusStat, bonusAmount, evolveTurns, evolved* , evolveOverrides", "光环+进化+唯一被动"),
]

LOOKUP_ELEMENTAL = [
    ("ReactionBoostPassive", "光界之力", "✅已实现", "percent", "反应倍率+percent%(加法叠加，非唯一)"),
    ("ElementalCorePassive", "四元素之力(潮涌/灼火/陨雷/凝冰)", "✅已实现", "element, multiplierPercent, electroChargeTurns, freezeTurns, superconductDefense, superconductTurns, extraGauge, explosion*", "反应增强+元素量加成+超载爆炸，唯一(uniqueId=ElementalCore)"),
]


def build_json_formula(cells, fields, quoted_fields=None):
    """用 CHAR(34) 生成 JSON 引号，避免 Excel "" 转义。
    quoted_fields 里的字段值会额外包双引号（字符串/枚举值，如 freeType:"Move"）。
    例：cells=['C7'], fields=['amount']
    → ="{"&CHAR(34)&"amount"&CHAR(34)&":"&C7&"}"
    """
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


# ============================================================
#  生成函数
# ============================================================
def build_config_sheet(wb, title, passives, first, note=""):
    """生成「配置生成器」sheet：填参数 → 公式生成 jsonParams。first=True 用 wb.active。"""
    ws = wb.active if first else wb.create_sheet(title)
    ws.title = title
    widths = {"A": 22, "B": 12, "C": 14, "D": 10, "E": 46}
    for col, w in widths.items():
        ws.column_dimensions[col].width = w

    ws["A1"] = title
    ws["A1"].font = F_TITLE
    ws.merge_cells("A1:E1")
    ws["A1"].fill = FILL_TITLE

    ws["A2"] = "使用说明：在【填写值】列（黄色底）填数值，下方【jsonParams】自动生成配置 JSON；把 className 和 jsonParams 分别复制到 Unity 的 PassiveConfig 即可。"
    ws["A2"].font = F_SUB
    ws.merge_cells("A2:E2")

    ws["A3"] = "bool 字段直接填 true 或 false（有下拉可选）；enum 字段下拉选择后 jsonParams 会自动带引号（如 kind 的 \"True\"）。" + note
    ws["A3"].font = F_SUB
    ws.merge_cells("A3:E3")

    dv_bool = DataValidation(type="list", formula1='"true,false"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_bool)
    dv_enum = DataValidation(type="list", formula1='"Move,Attack"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_enum)
    dv_dir = DataValidation(type="list", formula1='"Ally,Enemy"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_dir)
    dv_stat = DataValidation(type="list", formula1='"Attack,Defense"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_stat)
    dv_element = DataValidation(type="list", formula1='"Water,Fire,Thunder,Ice"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_element)
    dv_kind = DataValidation(type="list", formula1='"Physical,Magical,True"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_kind)
    dv_filter = DataValidation(type="list", formula1='"All,Physical,Magical,True"', allow_blank=True, showDropDown=False)
    ws.add_data_validation(dv_filter)

    row = 5
    for p in passives:
        # 块标题
        c = ws.cell(row, 1, f"◆ {p['name']}  {p['className']}")
        c.font = F_BLOCK
        c.fill = FILL_BLOCK
        for cc in range(1, 6):
            ws.cell(row, cc).fill = FILL_BLOCK
        ws.cell(row, 5, p["desc"]).font = Font(name="微软雅黑", size=9, color="FFFFFF")
        ws.merge_cells(start_row=row, start_column=5, end_row=row, end_column=5)

        # 表头
        heads = ["参数", "类型", "填写值", "默认值", "说明"]
        for i, h in enumerate(heads, 1):
            cell = ws.cell(row + 1, i, h)
            cell.font = F_HEAD
            cell.fill = FILL_HEAD
            cell.alignment = CENTER
            cell.border = BORDER

        # 参数行
        r = row + 2
        first_param_row = r
        for (field, typ, label, default, note) in p["params"]:
            a = ws.cell(r, 1, field)
            a.font = F_FIELD
            a.border = BORDER
            ws.cell(r, 2, typ).alignment = CENTER
            ws.cell(r, 2).border = BORDER
            inp = ws.cell(r, 3, default)
            inp.fill = FILL_INPUT
            inp.alignment = CENTER
            inp.border = BORDER
            if typ == "bool":
                dv_bool.add(inp)
            elif typ == "enum":
                dv_enum.add(inp)
            elif typ == "enum_dir":
                dv_dir.add(inp)
            elif typ == "enum_stat":
                dv_stat.add(inp)
            elif typ == "enum_element":
                dv_element.add(inp)
            elif typ == "enum_kind":
                dv_kind.add(inp)
            elif typ == "enum_filter":
                dv_filter.add(inp)
            ws.cell(r, 4, default).fill = FILL_DEF
            ws.cell(r, 4).alignment = CENTER
            ws.cell(r, 4).border = BORDER
            ws.cell(r, 5, note).border = BORDER
            ws.cell(r, 5).alignment = LEFT
            r += 1

        # className 行
        ws.cell(r, 1, "className").font = Font(name="微软雅黑", size=10, bold=True)
        cls_cell = ws.cell(r, 3, p["className"])
        cls_cell.font = F_GEN
        cls_cell.fill = FILL_GEN
        cls_cell.alignment = CENTER
        cls_cell.border = BORDER
        ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=5)
        for cc in (3, 4, 5):
            ws.cell(r, cc).fill = FILL_GEN

        # jsonParams 行（公式）
        r2 = r + 1
        fields = [pp[0] for pp in p["params"]]
        cells = [f"C{first_param_row + i}" for i in range(len(fields))]
        quoted = [pp[0] for pp in p["params"] if pp[1].startswith("enum")]
        formula = build_json_formula(cells, fields, quoted)
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

        row = r2 + 2  # 下一块，空一行


def build_lookup_sheet(wb, title, lookup):
    """生成「速查」sheet：类名/中文名/状态/参数/说明。"""
    ws = wb.create_sheet(title)
    w = {"A": 22, "B": 20, "C": 12, "D": 42, "E": 40}
    for col, width in w.items():
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


# ============================================================
#  生成 4 张表
# ============================================================
wb = openpyxl.Workbook()

common = [p for p in PASSIVES if p["cat"] == "通用"]
elemental = [p for p in PASSIVES if p["cat"] == "元素城邦"]

build_config_sheet(wb, "通用被动配置生成器", common, first=True)
build_config_sheet(wb, "元素被动配置生成器", elemental, first=False,
                   note="；元素城邦被动脚本位于 Equipment/Effects/Elemental/ 目录")
build_lookup_sheet(wb, "通用被动速查", LOOKUP_COMMON)
build_lookup_sheet(wb, "元素被动速查", LOOKUP_ELEMENTAL)

wb.save(OUT)
print("SAVED:", OUT)
