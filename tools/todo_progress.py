"""Work out docs/GAMEPLAY_TODO.md's progress figure from the list itself.

    python -I tools/todo_progress.py

Open items (`- [ ]` outside Next up, Testing and Waiting for the story port) are weighted by their size: small 1,
medium 3, large 15 (an item with no size counts as medium). Done items (the lines under Done) count 2 each, as most
were small or medium. Progress is done / (done + open), by weight.
"""
import re
import sys
from pathlib import Path

WEIGHTS = {"small": 1, "medium": 3, "large": 15}
DONE_WEIGHT = 2
SKIPPED = ("Next up", "Testing", "Waiting", "Done", "Story port")


def progress(text):
    sizes = {name: 0 for name in WEIGHTS}
    for section in re.split(r"\n## ", text)[1:]:
        title = section.split("\n", 1)[0]
        if title.startswith(SKIPPED):
            continue
        # An open item ends where the next item begins, open or done: a done item's size after it isn't its own.
        for item in (p for p in re.split(r"\n(?=- \[[ x]\] )", "\n" + section) if p.startswith("- [ ] ")):
            found = re.findall(r"\*(small|medium|large)[^*]*\*", item)
            sizes[found[-1] if found else "medium"] += 1
    done = len(re.findall(r"\n- ", text[text.index("\n## Done"):]))
    open_weight = sum(WEIGHTS[name] * count for name, count in sizes.items())
    done_weight = DONE_WEIGHT * done
    return done, sizes, round(100 * done_weight / (done_weight + open_weight))


def main():
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "docs/GAMEPLAY_TODO.md")
    done, sizes, percent = progress(path.read_text(encoding="utf-8"))
    print(f"{percent}% of gameplay: {done} done, {sum(sizes.values())} open "
          f"({sizes['small']} small, {sizes['medium']} medium, {sizes['large']} large)")


if __name__ == "__main__":
    main()
