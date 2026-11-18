"""A small MIPS R3000 (PS1) disassembler.

    decode(word, address) -> Instruction, or None if the word isn't a valid instruction

Covers the base instruction set plus the coprocessor moves the PS1 uses.
GTE commands show as `cop2 0x...`.
"""

from __future__ import annotations

from dataclasses import dataclass

REGS = [
    "zero", "at", "v0", "v1", "a0", "a1", "a2", "a3",
    "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7",
    "s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7",
    "t8", "t9", "k0", "k1", "gp", "sp", "fp", "ra",
]

SPECIAL = {
    0x00: "sll", 0x02: "srl", 0x03: "sra", 0x04: "sllv", 0x06: "srlv", 0x07: "srav",
    0x08: "jr", 0x09: "jalr", 0x0C: "syscall", 0x0D: "break",
    0x10: "mfhi", 0x11: "mthi", 0x12: "mflo", 0x13: "mtlo",
    0x18: "mult", 0x19: "multu", 0x1A: "div", 0x1B: "divu",
    0x20: "add", 0x21: "addu", 0x22: "sub", 0x23: "subu",
    0x24: "and", 0x25: "or", 0x26: "xor", 0x27: "nor", 0x2A: "slt", 0x2B: "sltu",
}
REGIMM = {0x00: "bltz", 0x01: "bgez", 0x10: "bltzal", 0x11: "bgezal"}
IMMEDIATE = {0x08: "addi", 0x09: "addiu", 0x0A: "slti", 0x0B: "sltiu", 0x0C: "andi", 0x0D: "ori", 0x0E: "xori"}
BRANCH = {0x04: "beq", 0x05: "bne", 0x06: "blez", 0x07: "bgtz"}
MEMORY = {
    0x20: "lb", 0x21: "lh", 0x22: "lwl", 0x23: "lw", 0x24: "lbu", 0x25: "lhu", 0x26: "lwr",
    0x28: "sb", 0x29: "sh", 0x2A: "swl", 0x2B: "sw", 0x2E: "swr", 0x32: "lwc2", 0x3A: "swc2",
}
LOADS = {"lb", "lh", "lwl", "lw", "lbu", "lhu", "lwr"}


@dataclass(frozen=True)
class Instruction:
    address: int
    word: int
    mnemonic: str
    operands: str = ""
    target: int | None = None  # jump or branch destination
    base: str | None = None  # memory access: base register
    offset: int | None = None  # memory access: signed offset
    rt: str | None = None  # destination/source register name
    imm: int | None = None  # raw 16-bit immediate

    def __str__(self) -> str:
        return f"{self.mnemonic} {self.operands}".strip()

    @property
    def is_call(self) -> bool:
        return self.mnemonic in ("jal", "jalr", "bltzal", "bgezal")

    @property
    def is_jump(self) -> bool:
        return self.mnemonic in ("j", "jal", "jr", "jalr") or self.target is not None


def _signed(v: int) -> int:
    return v - 0x10000 if v & 0x8000 else v


def decode(word: int, address: int = 0) -> Instruction | None:
    op = word >> 26
    rs, rt, rd = (word >> 21) & 31, (word >> 16) & 31, (word >> 11) & 31
    shamt, funct, imm = (word >> 6) & 31, word & 63, word & 0xFFFF
    simm = _signed(imm)
    I = lambda m, ops="", **kw: Instruction(address, word, m, ops, **kw)  # noqa: E731

    if word == 0:
        return I("nop")
    if op == 0:
        name = SPECIAL.get(funct)
        if name is None:
            return None
        if name in ("sll", "srl", "sra"):
            return I(name, f"{REGS[rd]}, {REGS[rt]}, {shamt}")
        if name in ("sllv", "srlv", "srav"):
            return I(name, f"{REGS[rd]}, {REGS[rt]}, {REGS[rs]}")
        if name == "jr":
            return I(name, REGS[rs])
        if name == "jalr":
            return I(name, f"{REGS[rd]}, {REGS[rs]}" if rd != 31 else REGS[rs])
        if name in ("syscall", "break"):
            return I(name, f"0x{(word >> 6) & 0xFFFFF:X}")
        if name in ("mfhi", "mflo"):
            return I(name, REGS[rd])
        if name in ("mthi", "mtlo"):
            return I(name, REGS[rs])
        if name in ("mult", "multu", "div", "divu"):
            return I(name, f"{REGS[rs]}, {REGS[rt]}")
        return I(name, f"{REGS[rd]}, {REGS[rs]}, {REGS[rt]}")
    if op == 1:
        name = REGIMM.get(rt)
        if name is None:
            return None
        target = (address + 4 + (simm << 2)) & 0xFFFFFFFF
        return I(name, f"{REGS[rs]}, 0x{target:08X}", target=target)
    if op in (2, 3):
        target = ((address + 4) & 0xF0000000) | ((word & 0x03FFFFFF) << 2)
        return I("j" if op == 2 else "jal", f"0x{target:08X}", target=target)
    if op in BRANCH:
        target = (address + 4 + (simm << 2)) & 0xFFFFFFFF
        name = BRANCH[op]
        if name in ("beq", "bne"):
            if name == "beq" and rs == rt == 0:
                return I("b", f"0x{target:08X}", target=target)
            return I(name, f"{REGS[rs]}, {REGS[rt]}, 0x{target:08X}", target=target)
        return I(name, f"{REGS[rs]}, 0x{target:08X}", target=target)
    if op in IMMEDIATE:
        name = IMMEDIATE[op]
        shown = f"0x{imm:X}" if name in ("andi", "ori", "xori") else str(simm)
        return I(name, f"{REGS[rt]}, {REGS[rs]}, {shown}", rt=REGS[rt], imm=imm)
    if op == 0x0F:
        return I("lui", f"{REGS[rt]}, 0x{imm:X}", rt=REGS[rt], imm=imm)
    if op in (0x10, 0x12):  # COP0, COP2 (GTE)
        cop = op >> 1 & 3
        if rs & 0x10:
            return I(f"cop{cop}", f"0x{word & 0x1FFFFFF:X}")
        moves = {0: "mfc", 2: "cfc", 4: "mtc", 6: "ctc"}
        if rs in moves:
            return I(f"{moves[rs]}{cop}", f"{REGS[rt]}, ${rd}")
        return None
    if op in MEMORY:
        name = MEMORY[op]
        reg = f"${rt}" if name in ("lwc2", "swc2") else REGS[rt]
        return I(name, f"{reg}, {simm}({REGS[rs]})", base=REGS[rs], offset=simm, rt=REGS[rt], imm=imm)
    return None


def words(data: bytes) -> list[int]:
    return [int.from_bytes(data[i : i + 4], "little") for i in range(0, len(data) - len(data) % 4, 4)]


def code_score(data: bytes) -> float:
    """Share of non-zero words that decode to plausible instructions (0..1)."""
    total = good = 0
    for w in words(data):
        if w == 0:
            continue
        total += 1
        ins = decode(w)
        if ins is None or ins.mnemonic in ("syscall", "break") or "k0" in ins.operands or "k1" in ins.operands:
            continue
        if ins.mnemonic.startswith(("cop", "mfc", "mtc", "cfc", "ctc", "lwc", "swc")):
            continue  # real, but rare enough that text often hits them by accident
        good += 1
    return good / total if total else 0.0


def is_prologue(word: int) -> bool:
    """addiu sp, sp, -N: how almost every PS1 function starts."""
    return (word & 0xFFFF0000) == 0x27BD0000 and word & 0x8000 != 0
