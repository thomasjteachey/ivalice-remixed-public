"""Work out how far the port has come, as one figure, from the lists that track it.

    python -I tools/port_progress.py            print it
    python -I tools/port_progress.py --write    also rewrite the figures in docs/PORT_PROGRESS.md and
                                                docs/GAMEPLAY_TODO.md (and today's row of the history)

The port is split into parts (PARTS), each with a fixed share of the whole; a part's progress is counted from its
list:

- Gameplay's rules: docs/GAMEPLAY_TODO.md's gameplay items, as tools/todo_progress.py counts them.
- Seen working, and the story's and the display's needs: the `- [ ]` and `- [x]` items of a section of
  docs/GAMEPLAY_TODO.md, weighted by size (small 1, medium 3, large 15; no size counts as medium).
- ReMixed's own story events: the table in docs/PORT_PROGRESS.md. An event counts half once the mod ships it (it's in
  tools/build_mod.py's REMIXED_SCENARIOS, read here without running the build) and whole once it's been seen working
  in the game (a date in its row, kept by hand).
"""
import ast
import datetime
import re
import runpy
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TODO = ROOT / "docs" / "GAMEPLAY_TODO.md"
PROGRESS = ROOT / "docs" / "PORT_PROGRESS.md"
BUILD = ROOT / "tools" / "build_mod.py"
WEIGHTS = {"small": 1, "medium": 3, "large": 15}

# (key, name, short name, share of the port in %, how it's counted). The shares are a judgment of how much of the
# whole port's work each part is (set 2026-11-06); only the lists move the figure.
PARTS = [
    ("rules", "Gameplay: ReMixed's rules ported", "Rules", 40, "gameplay"),
    ("tested", "Gameplay: seen working in the game", "Seen working", 10, "Testing"),
    ("events", "Story: ReMixed's own events", "Story events", 25, "events"),
    ("needs", "Story: what its events and battles need", "Story needs", 10,
     ("Story port progress", "Waiting for the story port")),
    ("display", "Display, text and art", "Display and art", 15, "Waiting for the display, text and art port"),
]


def read(path):
    return path.read_bytes().decode("utf-8").replace("\r\n", "\n")


def write(path, text):
    path.write_bytes(text.encode("utf-8"))


def sections(text):
    """Each `## ` section's title and body."""
    return {part.split("\n", 1)[0]: part for part in re.split(r"\n## ", text)[1:]}


def sized_items(body):
    """(done weight, open weight, done items, open items) of a section's checkbox items."""
    done = undone = done_items = open_items = 0
    for item in re.findall(r"\n- \[([ x])\] (.*?)(?=\n- |\n\n|\n#|\Z)", "\n" + body, re.S):
        found = re.findall(r"\*(small|medium|large)[^*]*\*", item[1])
        weight = WEIGHTS[found[-1] if found else "medium"]
        if item[0] == "x":
            done, done_items = done + weight, done_items + 1
        else:
            undone, open_items = undone + weight, open_items + 1
    return done, undone, done_items, open_items


def shipped_scenarios():
    """tools/build_mod.py's REMIXED_SCENARIOS (ReMixed's scenario -> the mod's), from its source."""
    dicts = {}
    for node in ast.parse(read(BUILD)).body:
        if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name):
            if isinstance(node.value, ast.Dict):
                try:
                    dicts[node.targets[0].id] = ast.literal_eval(node.value)
                except ValueError:
                    pass
        elif isinstance(node, ast.AugAssign) and isinstance(node.op, ast.BitOr) and isinstance(node.target, ast.Name):
            if isinstance(node.value, ast.Name):
                dicts.setdefault(node.target.id, {}).update(dicts.get(node.value.id, {}))
            elif isinstance(node.value, ast.Dict):
                dicts.setdefault(node.target.id, {}).update(ast.literal_eval(node.value))
    return dicts["REMIXED_SCENARIOS"]


EVENT_ROW = re.compile(r"^\| (0x[0-9A-F]{3}) \| ([^|]*?) \| ([^|]*?) \| ([^|]*?) \| ([^|]*?) \|$", re.M)


def events(progress_text, shipped):
    """The event table's rows: (event, group, what, the mod's number, seen), the mod's number from the build."""
    rows = []
    for event, group, what, _, seen in EVENT_ROW.findall(progress_text):
        ours = shipped.get(int(event, 16))
        rows.append((event, group, what, f"0x{ours:03X}" if ours is not None else "", seen))
    return rows


def count(todo_text, progress_text):
    found = sections(todo_text)
    gameplay = runpy.run_path(str(ROOT / "tools" / "todo_progress.py"))["progress"]
    done, sizes, rules = gameplay(todo_text)
    shipped = shipped_scenarios()
    rows = events(progress_text, shipped)
    built = sum(1 for row in rows if row[3])
    seen = sum(1 for row in rows if row[4])
    parts = {"rules": (rules, f"GAMEPLAY_TODO.md's gameplay items by size: {done} done, {sum(sizes.values())} open")}
    parts["events"] = (round(100 * (built + seen) / (2 * len(rows))) if rows else 0,
                       f"the {len(rows)} events below, half for each the mod ships ({built}) and the rest once seen "
                       f"working ({seen})")
    for key, _, _, _, source in PARTS:
        if key in parts:
            continue
        titles = source if isinstance(source, tuple) else (source,)
        totals = [0, 0, 0, 0]
        for title in titles:
            body = next((body for name, body in found.items() if name.startswith(title)), None)
            if body is None:
                raise SystemExit(f"GAMEPLAY_TODO.md has no section {title!r}")
            totals = [a + b for a, b in zip(totals, sized_items(body))]
        weight = totals[0] + totals[1]
        names = " and ".join(f'"{title}"' for title in titles)
        parts[key] = (round(100 * totals[0] / weight) if weight else 100,
                      f"GAMEPLAY_TODO.md's {names} by size: {totals[2]} done, {totals[3]} open")
    whole = round(sum(share * parts[key][0] for key, _, _, share, _ in PARTS) / 100)
    return whole, parts, rows, (built, seen, len(rows)), (done, sizes, rules)


def figures(whole, parts, today):
    lines = [f"**The port: {whole}% done** ({today}).", "",
             "| Part | Share of the port | Done | Counted from |", "|---|--:|--:|---|"]
    for key, name, _, share, _ in PARTS:
        lines.append(f"| {name} | {share}% | {parts[key][0]}% | {parts[key][1]} |")
    return "\n".join(lines)


def history(text, whole, parts, today):
    head = "| Date | The port | " + " | ".join(short for _, _, short, _, _ in PARTS) + " |"
    rule = "|---|--:|" + "--:|" * len(PARTS)
    row = f"| {today} | {whole}% | " + " | ".join(f"{parts[key][0]}%" for key, _, _, _, _ in PARTS) + " |"
    rows = [line for line in text.strip().split("\n") if re.match(r"\| \d{4}-\d\d-\d\d ", line)]
    rows = [line for line in rows if not line.startswith(f"| {today} ")] + [row]
    return "\n".join([head, rule] + rows)


def event_table(rows):
    lines = ["| Event | Group | What | The mod's number | Seen working |", "|---|---|---|---|---|"]
    lines += [f"| {event} | {group} | {what} | {ours} | {seen} |" for event, group, what, ours, seen in rows]
    return "\n".join(lines)


def replace_block(text, name, body):
    start, end = f"<!-- {name} -->\n", f"\n<!-- /{name} -->"
    a, b = text.index(start) + len(start), text.index(end)
    return text[:a] + body + text[b:]


def main():
    todo_text, progress_text = read(TODO), read(PROGRESS)
    whole, parts, rows, (built, seen, total), (done, sizes, rules) = count(todo_text, progress_text)
    today = datetime.date.today().isoformat()
    print(f"The port: {whole}% done")
    for key, name, _, share, _ in PARTS:
        print(f"  {name} ({share}% of the port): {parts[key][0]}% ({parts[key][1]})")
    if "--write" not in sys.argv[1:]:
        return
    progress_text = replace_block(progress_text, "figures", figures(whole, parts, today))
    a = progress_text.index("<!-- history -->\n") + len("<!-- history -->\n")
    old = progress_text[a:progress_text.index("\n<!-- /history -->")]
    progress_text = replace_block(progress_text, "history", history(old, whole, parts, today))
    progress_text = replace_block(progress_text, "events", event_table(rows))
    write(PROGRESS, progress_text)
    todo_text, n = re.subn(r"\*\*Progress: \d+% of gameplay\*\* \([^)]*\)",
                           f"**Progress: {rules}% of gameplay** ({done} done, {sum(sizes.values())} open: "
                           f"{sizes['small']} small, {sizes['medium']} medium, {sizes['large']} large)", todo_text)
    todo_text, m = re.subn(r"\*\*The whole port: \d+% done\*\*", f"**The whole port: {whole}% done**", todo_text)
    todo_text, k = re.subn(r"\*\*(\d+) of ReMixed's own events to bring over: \d+ built, \d+ seen working",
                           f"**{total} of ReMixed's own events to bring over: {built} built, {seen} seen working",
                           todo_text)
    if (n, m, k) != (1, 1, 1):
        raise SystemExit(f"GAMEPLAY_TODO.md's figures not found (gameplay {n}, the port {m}, the events {k})")
    write(TODO, todo_text)
    print("written: docs/PORT_PROGRESS.md, docs/GAMEPLAY_TODO.md")


if __name__ == "__main__":
    main()
