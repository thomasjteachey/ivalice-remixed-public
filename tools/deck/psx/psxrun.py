"""A headless PlayStation for scripted runs of the PS1's ReMixed: a small libretro frontend over ctypes.

It runs a libretro PSX core with no window and no sound: frames, button presses, RAM reads and writes, save states,
the memory card and screenshots. It uses RetroArch's own cores (the Deck's Flatpak, Steam's RetroArch on the PC);
SwanStation works (and runs about 850 frames a second on the Deck), Beetle PSX (mednafen_psx) stalls on a black
screen after the opening movie. docs/TESTING.md, "The PS1's ReMixed on the Deck and the PC", has the setup;
psxcommon.py and step.py drive it.

    emu = Emu(core, system_dir, "game.cue", card="card.mcd")
    emu.run(600); emu.tap("START"); emu.shot("title.png"); emu.save("title.state")
"""
from __future__ import annotations

import ctypes as C
import os
import struct
import zlib

JOYPAD, MASK_ID = 1, 256
BUTTONS = {"CROSS": 0, "SQUARE": 1, "SELECT": 2, "START": 3, "UP": 4, "DOWN": 5, "LEFT": 6, "RIGHT": 7,
           "CIRCLE": 8, "TRIANGLE": 9, "L1": 10, "R1": 11, "L2": 12, "R2": 13, "L3": 14, "R3": 15}
SAVE_RAM, SYSTEM_RAM = 0, 2
EXPERIMENTAL = 0x10000

ENV = C.CFUNCTYPE(C.c_bool, C.c_uint, C.c_void_p)
VIDEO = C.CFUNCTYPE(None, C.c_void_p, C.c_uint, C.c_uint, C.c_size_t)
AUDIO = C.CFUNCTYPE(None, C.c_int16, C.c_int16)
AUDIO_BATCH = C.CFUNCTYPE(C.c_size_t, C.c_void_p, C.c_size_t)
POLL = C.CFUNCTYPE(None)
INPUT = C.CFUNCTYPE(C.c_int16, C.c_uint, C.c_uint, C.c_uint, C.c_uint)
LOG = C.CFUNCTYPE(None, C.c_int, C.c_char_p)


class Variable(C.Structure):
    _fields_ = [("key", C.c_char_p), ("value", C.c_char_p)]


class GameInfo(C.Structure):
    _fields_ = [("path", C.c_char_p), ("data", C.c_void_p), ("size", C.c_size_t), ("meta", C.c_char_p)]


class Emu:
    def __init__(self, core: str, system: str, game: str, options: dict | None = None, card: str | None = None,
                 verbose: bool = False):
        self.lib = lib = C.CDLL(core)
        self.verbose = verbose
        self.options = dict(options or {})
        self.defaults: dict[str, str] = {}
        self.cstrings: dict[bytes, C.Array] = {}
        self.system = self._cstr(system)
        self.pixel_format = 0
        self.held = 0
        self.frame = None
        self.want_frame = False
        self.frames = 0
        self.card = card
        # The callbacks stay referenced from self, or ctypes would free them.
        self._cb = [ENV(self._environment), VIDEO(self._video), AUDIO(lambda left, right: None),
                    AUDIO_BATCH(lambda data, frames: frames), POLL(lambda: None), INPUT(self._input), LOG(self._log)]
        lib.retro_set_environment(self._cb[0])
        lib.retro_set_video_refresh(self._cb[1])
        lib.retro_set_audio_sample(self._cb[2])
        lib.retro_set_audio_sample_batch(self._cb[3])
        lib.retro_set_input_poll(self._cb[4])
        lib.retro_set_input_state(self._cb[5])
        for name, restype in (("retro_load_game", C.c_bool), ("retro_get_memory_data", C.c_void_p),
                              ("retro_get_memory_size", C.c_size_t), ("retro_serialize_size", C.c_size_t),
                              ("retro_serialize", C.c_bool), ("retro_unserialize", C.c_bool)):
            getattr(lib, name).restype = restype
        lib.retro_init()
        info = GameInfo(os.path.abspath(game).encode(), None, 0, None)
        if not lib.retro_load_game(C.byref(info)):
            raise SystemExit(f"the core couldn't load {game}")
        lib.retro_set_controller_port_device(0, JOYPAD)
        if card and os.path.exists(card):
            ptr, size = self._memory(SAVE_RAM)
            data = open(card, "rb").read()[:size]
            C.memmove(ptr, data, len(data))

    # --- the core's callbacks

    def _cstr(self, text: str | bytes) -> int:
        raw = text.encode() if isinstance(text, str) else text
        if raw not in self.cstrings:
            self.cstrings[raw] = C.create_string_buffer(raw)
        return C.cast(self.cstrings[raw], C.c_void_p).value

    def _environment(self, cmd: int, data: int) -> bool:
        base = cmd & 0xFFFF
        at = lambda kind: C.cast(data, C.POINTER(kind))  # noqa: E731
        if base == 2 or base == 3:  # GET_OVERSCAN, GET_CAN_DUPE
            at(C.c_bool)[0] = False
            return True
        if base in (9, 30, 31):  # the system, assets and save directories
            at(C.c_void_p)[0] = self.system
            return True
        if base == 10:  # SET_PIXEL_FORMAT
            self.pixel_format = at(C.c_int)[0]
            return self.pixel_format in (0, 1, 2)
        if base == 15:  # GET_VARIABLE
            key = at(Variable)[0].key.decode()
            value = self.options.get(key, self.defaults.get(key))
            if value is None:
                return False
            at(C.c_void_p)[1] = self._cstr(value)
            return True
        if base == 16:  # SET_VARIABLES: "Description; default|other|..."
            table, i = at(Variable), 0
            while table[i].key:
                text = table[i].value.decode(errors="replace")
                self.defaults[table[i].key.decode()] = text.split(";", 1)[-1].strip().split("|")[0]
                i += 1
            return True
        if base == 17:  # GET_VARIABLE_UPDATE
            at(C.c_bool)[0] = False
            return True
        if base == 27:  # GET_LOG_INTERFACE
            at(C.c_void_p)[0] = C.cast(self._cb[6], C.c_void_p).value
            return True
        if base == 39:  # GET_LANGUAGE: English
            at(C.c_uint)[0] = 0
            return True
        if base == 51 and cmd & EXPERIMENTAL:  # GET_INPUT_BITMASKS
            return True
        if base == 6 and data and self.verbose:  # SET_MESSAGE
            print("core message:", C.cast(data, C.POINTER(C.c_char_p))[0])
        return base in (1, 6, 8, 11, 12, 13, 18, 32, 33, 34, 35, 36, 37, 42, 44, 55, 58, 63, 65)

    def _video(self, data: int, width: int, height: int, pitch: int) -> None:
        if data and self.want_frame:
            self.frame = (C.string_at(data, pitch * height), width, height, pitch, self.pixel_format)

    def _input(self, port: int, device: int, index: int, ident: int) -> int:
        if port != 0 or device & 0xFF != JOYPAD:
            return 0
        return self.held if ident == MASK_ID else (self.held >> ident) & 1

    def _log(self, level: int, text: bytes) -> None:
        if self.verbose or level >= 3:
            print("core:", (text or b"").decode(errors="replace").rstrip())

    # --- driving it

    @staticmethod
    def mask(names: str) -> int:
        bits = 0
        for name in names.upper().replace(" ", "").split("+"):
            if name:
                bits |= 1 << BUTTONS[name]
        return bits

    def run(self, frames: int = 1, buttons: str = "") -> None:
        self.held = self.mask(buttons)
        for _ in range(frames):
            self.lib.retro_run()
        self.frames += frames
        self.held = 0

    def hold(self, buttons: str, frames: int) -> None:
        self.run(frames, buttons)

    def tap(self, buttons: str, after: int = 20, down: int = 4) -> None:
        self.run(down, buttons)
        self.run(after)

    def shot(self, path: str, buttons: str = "") -> None:
        self.held = self.mask(buttons)
        self.want_frame = True
        self.lib.retro_run()
        self.held = 0
        self.frames += 1
        self.want_frame = False
        if self.frame:
            write_png(path, *self.frame)

    def _memory(self, kind: int) -> tuple[int, int]:
        return self.lib.retro_get_memory_data(kind), self.lib.retro_get_memory_size(kind)

    def peek(self, address: int, count: int) -> bytes:
        ptr, size = self._memory(SYSTEM_RAM)
        return C.string_at(ptr + (address & 0x1FFFFF), count)

    def u8(self, address: int) -> int:
        return self.peek(address, 1)[0]

    def u16(self, address: int) -> int:
        return struct.unpack("<H", self.peek(address, 2))[0]

    def u32(self, address: int) -> int:
        return struct.unpack("<I", self.peek(address, 4))[0]

    def poke(self, address: int, data: bytes) -> None:
        ptr, size = self._memory(SYSTEM_RAM)
        C.memmove(ptr + (address & 0x1FFFFF), data, len(data))

    def save(self, path: str) -> None:
        size = self.lib.retro_serialize_size()
        buf = C.create_string_buffer(size)
        if not self.lib.retro_serialize(buf, size):
            raise SystemExit("save state failed")
        with open(path, "wb") as f:
            f.write(buf.raw)

    def load(self, path: str) -> None:
        data = open(path, "rb").read()
        if not self.lib.retro_unserialize(data, len(data)):
            raise SystemExit(f"loading {path} failed")

    def save_card(self, path: str | None = None) -> None:
        ptr, size = self._memory(SAVE_RAM)
        if ptr and size:
            with open(path or self.card, "wb") as f:
                f.write(C.string_at(ptr, size))


def write_png(path: str, raw: bytes, width: int, height: int, pitch: int, fmt: int) -> None:
    rows = []
    for y in range(height):
        line = raw[y * pitch:y * pitch + width * (4 if fmt == 1 else 2)]
        if fmt == 1:  # XRGB8888: B, G, R, X in memory
            rgb = bytearray(width * 3)
            rgb[0::3], rgb[1::3], rgb[2::3] = line[2::4], line[1::4], line[0::4]
        else:  # RGB565 (2) or 0RGB1555 (0)
            rgb = bytearray()
            for v in struct.unpack(f"<{width}H", line):
                if fmt == 2:
                    r, g, b = (v >> 11) & 31, (v >> 5) & 63, v & 31
                    rgb += bytes((r << 3 | r >> 2, g << 2 | g >> 4, b << 3 | b >> 2))
                else:
                    r, g, b = (v >> 10) & 31, (v >> 5) & 31, v & 31
                    rgb += bytes((r << 3 | r >> 2, g << 3 | g >> 2, b << 3 | b >> 2))
        rows.append(b"\x00" + bytes(rgb))

    def chunk(kind: bytes, body: bytes) -> bytes:
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body))

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(b"".join(rows), 6)) + chunk(b"IEND", b""))
