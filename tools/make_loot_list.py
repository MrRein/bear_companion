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

# Купоните: тегло в обикновените сандъчета (общо ~12). Ориентир: −15% често, −25% нечесто, −50% рядко;
# дрехи често, храна нечесто (−50% нечесто, безплатно рядко), мебели рядко, безплатно нещо – много рядко.
coupons = [("pct15", "Купон −15% (всичко)", 3.1), ("pct25", "Купон −25% (всичко)", 1.6), ("pct50", "Купон −50% (всичко)", 0.8),
           ("cloth50", "Купон −50% дрехи", 3.1), ("food50", "Купон −50% храна", 1.6), ("food", "Купон безплатно готвене", 0.8),
           ("home30", "Купон −30% мебели", 0.8), ("free", "Купон безплатно нещо", 0.2)]

# ---- теглата по подразбиране (както беше досега) ----
def defaults(code, kind):
    w = dict.fromkeys(SOURCES, 0.0)
    wearable = kind.startswith("Дреха") or kind in ("Тапет", "Меню (цвят)")
    n_wear = sum(1 for i in items if i[2].startswith("Дреха") or i[2] in ("Тапет", "Меню (цвят)"))
    n_cloth = sum(1 for i in items if i[2].startswith("Дреха"))
    furn = [i for i in items if i[2] == "Уред/мебел – Мебел" and i[0] not in ("up:wardrobe", "up:heater")]
    if code == "nuts": w.update({"Цел": 83, "Подарък": 0, "Дървено": 32, "Сребърно": 32, "Златно": 32, "Лилаво": 25})
    elif code == "page": w.update({"Подарък": 25, "Дървено": 28, "Сребърно": 28, "Златно": 28, "Лилаво": 25})
    elif code.startswith("coupon:"):
        p = dict((c, p) for c, _, p in coupons)[code[7:]]
        for s in ("Дървено", "Сребърно", "Златно"): w[s] = p
        if code == "coupon:pct50": w["Лилаво"] = 10
        if code == "coupon:free": w["Лилаво"] = 3
    if wearable:
        w["Цел"] = round(17 / n_wear, 3); w["Подарък"] = round(75 / n_wear, 3)
    # Тапети и менюта – и от сандъчетата, но рядко (общо ~3 срещу ~17 за дрехите).
    n_bg = sum(1 for i in items if i[2] == "Тапет"); n_th = sum(1 for i in items if i[2] == "Меню (цвят)")
    if kind == "Тапет":
        w["Дървено"] = round(1.5 / n_bg, 3)
        for s in ("Сребърно", "Златно"): w[s] = round(2.5 / n_bg, 3)
        w["Лилаво"] = round(6 / n_bg, 3)
    if kind == "Меню (цвят)":
        w["Дървено"] = round(0.8 / n_th, 3)
        for s in ("Сребърно", "Златно"): w[s] = round(1.5 / n_th, 3)
        w["Лилаво"] = round(4 / n_th, 3)
    if kind.startswith("Дреха"):
        for s in ("Дървено", "Сребърно", "Златно"): w[s] = round(17 / n_cloth, 3)
        w["Лилаво"] = round(20 / n_cloth, 3)
    if any(code == f[0] for f in furn):
        for s in ("Сребърно", "Златно"): w[s] = round(3 / len(furn), 3)
        w["Лилаво"] = round(30 / len(furn), 3)
    return w

# ---- рядкостта: по раздели, по цената (най-скъпите – най-редки) ----
RAR = ["Обикновено", "Хубаво", "Страхотно", "Безценно"]
def quotas(n, ratio):
    t = sum(ratio); raw = [n * r / t for r in ratio]; q = [int(x) for x in raw]
    for i in sorted(range(4), key=lambda i: raw[i] - q[i], reverse=True)[: n - sum(q)]: q[i] += 1
    return q
rarity = {}
sections = {}
for it in items:
    sec = it[2] if not it[0].startswith(("up:",)) else None
    if sec: sections.setdefault(sec, []).append(it)
for sec, lst in sections.items():
    ratio = (7, 5, 3, 1) if sec.startswith("Дреха") or sec == "Пижама" else (4, 3, 2, 1)
    q = quotas(len(lst), ratio)
    order = sorted(range(len(lst)), key=lambda i: (lst[i][3], i))   # евтините – обикновени
    k = 0
    for level, cnt in enumerate(q):
        for _ in range(cnt):
            rarity[lst[order[k]][0]] = RAR[level]; k += 1

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
PRICES = {"дрехи": (20, 30, 50, 70), "пижами": (20, 30, 50, 70), "тапети": (35, 45, 65, 90), "менюта": (35, 45, 65, 90), "облици": (35, 45, 65, 90)}
for g, ps in PRICES.items():
    for rname, pr in zip(RAR, ps): SETTINGS.append((f"цена {g} {rname}", pr, f"Цена в магазина: {g}, {rname.lower()}"))
# Безценното – наистина рядко: средно под 1 на проект (финалът – най-вероятно).
RTAB = {"Оферта": (62, 28, 9, 1), "Подарък": (60, 30, 9, 1), "Цел": (50, 35, 13, 2), "Дървено": (72, 24, 4, 0),
        "Сребърно": (50, 38, 11, 1), "Златно": (25, 45, 27, 3), "Лилаво": (5, 35, 48, 12)}
for src, ws_ in RTAB.items():
    for rname, wv in zip(RAR, ws_): SETTINGS.append((f"рядкост {src} {rname}", wv, f"Колко често: {rname.lower()} от {src.lower()}" + (" (офертата на деня в магазина)" if src == "Оферта" else "")))

# ---- запазва старите стойности ----
old_w, old_s, old_r = {}, {}, {}
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
                if "Рядкост" in head and r[head.index("Рядкост")]: old_r[r[0]] = r[head.index("Рядкост")]
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
cols = ["Код", "Име", "Вид", "Рядкост", "Цена", "Условие"] + SOURCES
HR = 4
for c, h in enumerate(cols, 1):
    x = ws.cell(HR, c, h); x.font = HF; x.fill = H; x.border = BR; x.alignment = Alignment(horizontal="center")
ws.cell(3, 6, "Сума →").font = Font(bold=True)
RCOL = {"Обикновено": "8B643E", "Хубаво": "6080A8", "Страхотно": "C48C18", "Безценно": "8C46B4"}
from openpyxl.worksheet.datavalidation import DataValidation
dv = DataValidation(type="list", formula1='"' + ",".join(RAR) + '"', allow_blank=True); ws.add_data_validation(dv)
first = HR + 1
for i, (code, name, kind, price, cond) in enumerate(rows):
    r = first + i
    w = defaults(code, kind)
    if code in old_w: w.update({k: v for k, v in old_w[code].items()})
    rar = old_r.get(code, rarity.get(code)) if code in rarity or code in old_r else None
    grp = {"cloth": "дрехи", "pajama": "пижами", "bg": "тапети", "theme": "менюта", "skin": "облици"}.get(code.split(":")[0])
    price_v = (f'=IFERROR(VLOOKUP("цена {grp} "&D{r},Настройки!$A:$B,2,FALSE),"")' if grp else (price if price != "" else None))
    vals = [code, name, kind, rar, price_v, cond] + [w[s] if w[s] else None for s in SOURCES]
    for c, v in enumerate(vals, 1):
        x = ws.cell(r, c, v); x.border = BR
        if c <= 6: x.fill = kfill(kind)
        else: x.number_format = "0.###"; x.alignment = Alignment(horizontal="center")
    if rar:
        ws.cell(r, 4).font = Font(bold=True, color=RCOL.get(rar, "000000")); dv.add(ws.cell(r, 4))
last = first + len(rows) - 1
for k, s in enumerate(SOURCES):
    col = 7 + k; L = ws.cell(HR, col).column_letter
    x = ws.cell(3, col, f"=SUM({L}{first}:{L}{last})"); x.font = Font(bold=True); x.number_format = "0.#"; x.alignment = Alignment(horizontal="center")
    ws.conditional_formatting.add(f"{L}{first}:{L}{last}", ColorScaleRule(start_type="num", start_value=0, start_color="FFFFFF", end_type="max", end_color="7BB662"))
for col, wdt in zip("ABCDEFGHIJKL", (26, 30, 24, 13, 7, 26, 9, 10, 10, 10, 10, 9)): ws.column_dimensions[col].width = wdt
ws.freeze_panes = ws.cell(first, 3)
ws.auto_filter.ref = f"A{HR}:L{last}"

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
    ("Рядкост", "Обикновено · Хубаво · Страхотно · Безценно (избираш от списъка). От нея идват цената и колко често излиза."),
    ("Име / Вид / Цена", "За справка (името и цената идват от играта)."),
    ("Условие", "Кога предметът изобщо може да се падне. Нещата, които вече имаш, никога не се падат."),
    ("Цел", "Наградата за изпълнена цел (мисия). Лешници или едно нещо."),
    ("Подарък", "Подаръкът на деня. Едно нещо; ако няма какво – лешници."),
    ("Дървено / Сребърно / Златно / Лилаво", "Сандъчетата в проекта. Всяко нещо в сандъчето се тегли отделно от тази колона (едно и също не се повтаря)."),
    ("Как работи рядкостта", "Колоната дава общия шанс на вида (напр. всички дрехи заедно). Вътре в него рядкостта се тегли по таблиците „рядкост …“ в „Настройки“, после – предмет от тази рядкост. Ако нямаш повече от дадена рядкост, шансът се дели между другите."),
    ("Цена", "Изчислява се от „Настройки“ → „цена … “ според рядкостта. Безплатните (началните) неща остават безплатни."),
    ("Сума", "Ред 3 събира колоната. Ако е 100 – числата са точно проценти; ако не е – пак работят като тегла (дял от сумата)."),
    ("", ""),
    ("ВИДОВЕ ПРЕДМЕТИ (код)", ""),
    ("nuts – Лешници", "Количеството е в „Настройки“ (за сандъчетата – според етапа: 3–7, финалът ×3)."),
    ("page – Страница от рецепта", "Събираш страници → нова рецепта. Само докато има рецепти за събиране."),
    ("coupon:… – Купон", "Червени (всичко): pct15 −15% · pct25 −25% · pct50 −50%. Син: cloth50 −50% дрехи. Зелен: home30 −30% мебели. Оранжеви: food50 −50% храна · food безплатно готвене. Лилав: free едно нещо безплатно."),
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
