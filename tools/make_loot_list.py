#!/usr/bin/env python3
"""Прави assets/loot_list.xlsx (master loot_list) от каталозите в кода.
Ако файлът вече съществува, теглата и настройките в него се ЗАПАЗВАТ – добавят се само
новите предмети (с тегла по подразбиране). Пусни: python3 tools/make_loot_list.py"""
import json, os, re, sys
from openpyxl import Workbook, load_workbook
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.formatting.rule import ColorScaleRule

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src", "Mecho")
OUT = os.path.join(ROOT, "assets", "loot_list.xlsx")
def read(n): return open(os.path.join(SRC, n), encoding="utf-8").read()

SOURCES = ["Цел", "Подарък", "Дървено", "Сребърно", "Златно", "Лилаво"]

# ---- каталозите ----
kitchen = read("Kitchen.cs")
consts = dict(re.findall(r'(\w+)\s*=\s*"([^"]*)"', "\n".join(l for l in kitchen.splitlines() if "const string" in l)))
items = []   # (code, name, kind, price, condition)

clothes = read("Clothes.cs")
slots = {"hat": "Шапка", "glasses": "Очила", "neck": "Аксесоар", "shirt": "Блуза", "pants": "Панталон", "shoes": "Обувки", "gloves": "Ръкавици"}
garments = {}
for m in re.finditer(r'new\("([\w]+)", "([^"]+)", "(\w+)", (\d+), [^\n]*', clothes):
    gid, name, slot, price = m.group(1), m.group(2), m.group(3), int(m.group(4))
    if slot not in slots or "Hidden = true" in m.group(0) or price <= 0: continue
    garments[gid] = (name, slot, price)
cj = open(os.path.join(ROOT, "assets", "clothes", "clothes.json"), encoding="utf-8").read()
cj = re.sub(r'^\s*//.*$', '', cj, flags=re.M); cj = re.sub(r',(\s*[\]}])', r'\1', cj)
for g in json.loads(cj).get("garments", []):
    if g.get("slot") in slots and g.get("id"):
        garments[g["id"]] = (g.get("name", g["id"]), g["slot"], int(g.get("price", 10)))
for gid, (name, slot, price) in garments.items():
    items.append((f"cloth:{gid}", name, "Дреха – " + slots[slot], price, "гардероб"))

pj = clothes[clothes.index("PajamaSets ="):]
for m in re.finditer(r'new\("(\w+)", "([^"]+)", (\d+), "', pj):
    if int(m.group(3)) > 0: items.append((f"pajama:{m.group(1)}", m.group(2), "Пижама", int(m.group(3)), "гардероб"))

bg = read("Backgrounds.cs")
for m in re.finditer(r'new\("(\w+)", "([^"]+)", (\d+)\)', bg):
    if int(m.group(3)) > 0: items.append((f"bg:{m.group(1)}", m.group(2), "Тапет", int(m.group(3)), "гардероб"))

th = read("UiThemes.cs")
for m in re.finditer(r'new\("(\w+)", "([^"]+)", (\d+),', th):
    if int(m.group(3)) > 0: items.append((f"theme:{m.group(1)}", m.group(2), "Меню (цвят)", int(m.group(3)), "гардероб"))

up_block = kitchen[kitchen.index("Upgrade[] Upgrades ="):]
up_block = up_block[:up_block.index("};")]
shelf_names = {"KitchenShelf": "Кухня", "FunShelf": "Хоби", "FurnitureShelf": "Мебел", "HelpShelf": "Подобрение"}
upgrades = {}
for m in re.finditer(r'new\((\w+), "([^"]+)", "[^"]*", (\d+), "[^"]*"(?:, (\w+))?', up_block):
    uid = consts.get(m.group(1), m.group(1)); shelf = shelf_names.get(m.group(4) or "KitchenShelf", "Кухня")
    upgrades[uid] = m.group(2)
    items.append((f"up:{uid}", m.group(2), "Уред/мебел – " + shelf, int(m.group(3)), "още го нямаш"))

fur = read("Furniture.cs")
def skin_rows(item, seg):
    for m in re.finditer(r'\("(\w+)", "([^"]+)", (\d+)\)', seg):
        if int(m.group(3)) > 0:
            items.append((f"skin:{item}:{m.group(1)}", m.group(2), "Облик – " + upgrades.get(item, item), int(m.group(3)), "имаш " + upgrades.get(item, item).lower()))
for arr, item in (("CouchSkins", "couch"), ("BedSkins", "bed")):
    seg = fur[fur.index(f"Skin[] {arr} ="):]; seg = seg[:seg.index("};")]
    for m in re.finditer(r'new\(Kitchen\.\w+, "(\w+)", "([^"]+)", (\d+)\)', seg):
        if int(m.group(3)) > 0:
            items.append((f"skin:{item}:{m.group(1)}", m.group(2), "Облик – " + upgrades.get(item, item), int(m.group(3)), "имаш " + upgrades.get(item, item).lower()))
for m in re.finditer(r'\[Kitchen\.(\w+)\] = Of\(Kitchen\.\w+,(.*?)\)\),\n', fur, re.S):
    skin_rows(consts[m.group(1)], m.group(2) + ")")

coupons = [("pct25", "Купон −25% (всичко)", 35), ("food", "Купон безплатно готвене", 20), ("cloth50", "Купон −50% дрехи", 15),
           ("pct50", "Купон −50% (всичко)", 10), ("home30", "Купон −30% мебели", 10), ("free", "Купон безплатно нещо", 10)]

# ---- теглата по подразбиране (както беше досега) ----
def defaults(code, kind):
    w = dict.fromkeys(SOURCES, 0.0)
    wearable = kind.startswith("Дреха") or kind in ("Тапет", "Меню (цвят)")
    n_wear = sum(1 for i in items if i[2].startswith("Дреха") or i[2] in ("Тапет", "Меню (цвят)"))
    n_cloth = sum(1 for i in items if i[2].startswith("Дреха"))
    furn = [i for i in items if i[2] == "Уред/мебел – Мебел" and i[0] not in ("up:wardrobe", "up:heater")]
    if code == "nuts": w.update({"Цел": 83, "Подарък": 0, "Дървено": 32, "Сребърно": 32, "Златно": 32, "Лилаво": 20})
    elif code == "page": w.update({"Подарък": 25, "Дървено": 28, "Сребърно": 28, "Златно": 28, "Лилаво": 20})
    elif code.startswith("coupon:"):
        p = dict((c, p) for c, _, p in coupons)[code[7:]]
        for s in ("Дървено", "Сребърно", "Златно"): w[s] = round(20 * p / 100, 2)
        if code == "coupon:pct50": w["Лилаво"] = 20
    if wearable:
        w["Цел"] = round(17 / n_wear, 3); w["Подарък"] = round(75 / n_wear, 3)
    if kind.startswith("Дреха"):
        for s in ("Дървено", "Сребърно", "Златно"): w[s] = round(17 / n_cloth, 3)
        w["Лилаво"] = round(20 / n_cloth, 3)
    if any(code == f[0] for f in furn):
        for s in ("Сребърно", "Златно"): w[s] = round(3 / len(furn), 3)
        w["Лилаво"] = round(20 / len(furn), 3)
    return w

rows = [("nuts", "Лешници", "Лешници", "", "винаги (количеството – в „Настройки“)"),
        ("page", "Страница от рецепта", "Рецепта", "", "има още рецепти")]
rows += [(f"coupon:{c}", n, "Купон", "", "винаги") for c, n, _ in coupons]
rows += items

SETTINGS = [
    ("Подарък шанс за ден %", 70, "В колко от дните има подарък (настроението леко го мести)"),
    ("Цел лешници мин", 15, "Лешници за изпълнена цел (мисия) – от"),
    ("Цел лешници макс", 25, "… до (по-често ниско)"),
    ("Подарък лешници мин", 3, "Лешници в подаръка – от"),
    ("Подарък лешници макс", 10, "… до (по-често ниско)"),
    ("шанс Сребърно %", 31, "Колко от сандъчетата в проекта са сребърни (16 етапа → 5)"),
    ("шанс Златно %", 19, "Колко са златни (→ 3; само във втората половина)"),
    ("шанс Дървено %", 44, "Дървените – останалите (→ 7); само за справка"),
    ("шанс Лилаво %", 6, "Лилавото – винаги 1, финалът; само за справка"),
    ("Дървено неща мин", 1, "Колко неща има в дървено сандъче – от"),
    ("Дървено неща макс", 2, "… до"),
    ("Сребърно неща мин", 3, ""), ("Сребърно неща макс", 3, ""),
    ("Златно неща мин", 4, ""), ("Златно неща макс", 4, ""),
    ("Лилаво неща мин", 5, "Финалът: толкова неща + кутията с играта"), ("Лилаво неща макс", 5, ""),
    ("скрито нещо %", 33, "Шанс в сандъче с 2+ неща едно да е „???“ до отваряне"),
    ("мебели от етап", 9, "Мебели/уреди от сандъчетата – чак от този етап нататък"),
]

# ---- запазва старите стойности ----
old_w, old_s = {}, {}
if os.path.exists(OUT):
    wb0 = load_workbook(OUT)
    if "Предмети" in wb0.sheetnames:
        ws0 = wb0["Предмети"]; head = None
        for r in ws0.iter_rows(values_only=True):
            if head is None:
                if r and r[0] == "Код": head = list(r)
                continue
            if r and r[0] and not str(r[0]).startswith("#"):
                old_w[r[0]] = {s: (r[head.index(s)] or 0) for s in SOURCES if s in head}
    if "Настройки" in wb0.sheetnames:
        for r in wb0["Настройки"].iter_rows(min_row=4, values_only=True):
            if r and r[0]: old_s[r[0]] = r[1]

# ---- пише ----
wb = Workbook()
H = PatternFill("solid", fgColor="8B5A2B"); HF = Font(bold=True, color="FFFFFF")
thin = Side(style="thin", color="C9A877"); BR = Border(left=thin, right=thin, top=thin, bottom=thin)
KIND_FILL = {"Лешници": "F6E3B4", "Рецепта": "F1D9C4", "Купон": "E6F0D8", "Пижама": "E3E0F3", "Тапет": "D8E8F3", "Меню (цвят)": "F3DCE6"}
def kfill(kind):
    for k, c in KIND_FILL.items():
        if kind.startswith(k): return PatternFill("solid", fgColor=c)
    if kind.startswith("Дреха"): return PatternFill("solid", fgColor="FBEFD9")
    if kind.startswith("Облик"): return PatternFill("solid", fgColor="EAE3D6")
    return PatternFill("solid", fgColor="EFE6D2")

ws = wb.active; ws.title = "Предмети"
ws["A1"] = "Master loot_list – какво може да се падне и с какво тегло"; ws["A1"].font = Font(bold=True, size=14)
ws["A2"] = "Пиши само числа (без %). 0 или празно = никога. Нещата, които вече имаш, се пропускат и останалите се преразпределят. Виж лист „Легенда“."
ws["A2"].font = Font(italic=True, color="7A5A3A")
cols = ["Код", "Име", "Вид", "Цена", "Условие"] + SOURCES
HR = 4
for c, h in enumerate(cols, 1):
    x = ws.cell(HR, c, h); x.font = HF; x.fill = H; x.border = BR; x.alignment = Alignment(horizontal="center")
ws.cell(3, 5, "Сума →").font = Font(bold=True)
first = HR + 1
for i, (code, name, kind, price, cond) in enumerate(rows):
    r = first + i
    w = defaults(code, kind)
    if code in old_w: w.update({k: v for k, v in old_w[code].items()})
    vals = [code, name, kind, price, cond] + [w[s] if w[s] else None for s in SOURCES]
    for c, v in enumerate(vals, 1):
        x = ws.cell(r, c, v); x.border = BR
        if c <= 5: x.fill = kfill(kind)
        else: x.number_format = "0.###"; x.alignment = Alignment(horizontal="center")
last = first + len(rows) - 1
for k, s in enumerate(SOURCES):
    col = 6 + k; L = ws.cell(HR, col).column_letter
    x = ws.cell(3, col, f"=SUM({L}{first}:{L}{last})"); x.font = Font(bold=True); x.number_format = "0.#"; x.alignment = Alignment(horizontal="center")
    ws.conditional_formatting.add(f"{L}{first}:{L}{last}", ColorScaleRule(start_type="num", start_value=0, start_color="FFFFFF", end_type="max", end_color="7BB662"))
for col, wdt in zip("ABCDEFGHIJK", (26, 30, 24, 7, 26, 9, 10, 10, 10, 10, 9)): ws.column_dimensions[col].width = wdt
ws.freeze_panes = ws.cell(first, 3)
ws.auto_filter.ref = f"A{HR}:K{last}"

st = wb.create_sheet("Настройки")
st["A1"] = "Настройки – числата около наградите"; st["A1"].font = Font(bold=True, size=14)
for c, h in enumerate(["Ключ (не го променяй)", "Стойност", "Какво е"], 1):
    x = st.cell(3, c, h); x.font = HF; x.fill = H; x.border = BR
for i, (k, v, d) in enumerate(SETTINGS):
    for c, val in enumerate([k, old_s.get(k, v), d], 1):
        x = st.cell(4 + i, c, val); x.border = BR
st.column_dimensions["A"].width = 26; st.column_dimensions["B"].width = 10; st.column_dimensions["C"].width = 70

lg = wb.create_sheet("Легенда")
lg["A1"] = "Легенда"; lg["A1"].font = Font(bold=True, size=14)
legend = [
    ("КОЛОНИ", ""),
    ("Код", "Как играта познава предмета. Не го променяй (иначе предметът няма да се намери)."),
    ("Име / Вид / Цена", "За справка (името и цената идват от играта)."),
    ("Условие", "Кога предметът изобщо може да се падне. Нещата, които вече имаш, никога не се падат."),
    ("Цел", "Наградата за изпълнена цел (мисия). Лешници или едно нещо."),
    ("Подарък", "Подаръкът на деня. Едно нещо; ако няма какво – лешници."),
    ("Дървено / Сребърно / Златно / Лилаво", "Сандъчетата в проекта. Всяко нещо в сандъчето се тегли отделно от тази колона (едно и също не се повтаря)."),
    ("Сума", "Ред 3 събира колоната. Ако е 100 – числата са точно проценти; ако не е – пак работят като тегла (дял от сумата)."),
    ("", ""),
    ("ВИДОВЕ ПРЕДМЕТИ (код)", ""),
    ("nuts – Лешници", "Количеството е в „Настройки“ (за сандъчетата – според етапа: 3–7, финалът ×3)."),
    ("page – Страница от рецепта", "Събираш страници → нова рецепта. Само докато има рецепти за събиране."),
    ("coupon:… – Купон", "pct25 −25% всичко · pct50 −50% всичко · cloth50 −50% дрехи · home30 −30% мебели · food безплатно готвене · free едно нещо безплатно."),
    ("cloth:… – Дреха", "Шапки, очила, аксесоари, блузи, панталони, обувки, ръкавици. Само с гардероб."),
    ("pajama:… – Пижама", "Сет пижама. Само с гардероб."),
    ("bg:… – Тапет", "Фон зад мечока. Само с гардероб."),
    ("theme:… – Меню", "Цветовете на менюто. Само с гардероб."),
    ("up:… – Уред/мебел", "Кухня, хоби, мебели, подобрения. Идва с доставка. Само ако още го нямаш."),
    ("skin:предмет:… – Облик", "Друг вид на мебел/уред, който вече имаш."),
    ("", ""),
    ("ПРАВИЛА, КОИТО НЕ СА В ТАБЛИЦАТА", ""),
    ("Нужна мебел", "Ако следващият етап на проекта иска мебел, която нямаш (бюро, масичка, статив, компютър), тя е в сандъчето допълнително."),
    ("Финалът", "Лилавото сандъче винаги има и кутията с играта."),
    ("Едно нещо", "Ако в дървено сандъче има само едно нещо – по-голямо: лешници ×2+2 или купон −50%."),
    ("Нови предмети", "Пусни tools/make_loot_list.py – добавя новите предмети в края, без да пипа твоите числа."),
]
for i, (a, b) in enumerate(legend):
    x = lg.cell(3 + i, 1, a); y = lg.cell(3 + i, 2, b)
    y.alignment = Alignment(wrap_text=True, vertical="top")
    if b == "" and a: x.font = Font(bold=True, color="FFFFFF"); x.fill = H; lg.cell(3 + i, 2).fill = H
    elif a: x.font = Font(bold=True)
lg.column_dimensions["A"].width = 36; lg.column_dimensions["B"].width = 100

wb.save(OUT)
print(f"{OUT}: {len(rows)} предмета")
