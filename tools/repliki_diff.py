"""Сравнява два файла с реплики (стар и нов) и казва какво е добавено, махнато
и кои раздели са нови или изчезнали.

    python tools/repliki_diff.py стар.txt нов.txt
"""
import sys


def load(path):
    sections, key = {}, None
    for raw in open(path, encoding="utf-8-sig"):
        line = raw.strip()
        if not line or line.startswith("//"):
            continue
        if line.startswith("==="):
            key = line[3:].split("|")[0].strip()
            sections.setdefault(key, [])
            continue
        if key:
            sections[key].append(line)
    return sections


old, new = load(sys.argv[1]), load(sys.argv[2])
added = removed = 0
for key in list(old) + [k for k in new if k not in old]:
    a, b = old.get(key), new.get(key)
    if a is None:
        print(f"\n=== {key}  (НОВ РАЗДЕЛ – кодът не го ползва, ако не е добавен в програмата)")
    elif b is None:
        print(f"\n=== {key}  (РАЗДЕЛЪТ Е МАХНАТ – мечокът ще мълчи тук)")
        continue
    plus = [x for x in b if x not in a]
    minus = [x for x in (a or []) if x not in b]
    if not plus and not minus and a is not None:
        continue
    if a is not None:
        print(f"\n=== {key}")
    for x in minus:
        print(f"  - {x}")
    for x in plus:
        print(f"  + {x}")
    added += len(plus)
    removed += len(minus)
print(f"\nОбщо: +{added} добавени, -{removed} махнати.")
