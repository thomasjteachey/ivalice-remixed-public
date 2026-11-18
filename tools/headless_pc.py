"""Runs the enhanced game with the mod on this PC where it can't be seen or heard, for the mod's own tests.

    python -I tools/headless_pc.py [--seconds 180] [--script autopilot.txt]

The game starts through Reloaded-II (its launcher injects the mod) on a desktop of its own, which Windows never shows,
so its window can't appear on the screens, take the focus or change a display mode. --script copies an autopilot
script beside the installed mod (code/IvaliceRemixed/Autopilot.cs: it presses the pad's buttons and mutes the game's
sound); without one the game still runs muted only if a script is there, so a run without --script is refused. After the
time is up, or the game exits, the processes it started are ended, the script removed, and the mod's log printed from
where the run began.
"""

from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as wt
import json
import os
import shutil
import subprocess
import sys
import struct
import threading
import time
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from remixed import load_config  # noqa: E402

MOD_ID = "fftivc.gameplay.ivaliceremixed"
DESKTOP = "ivalice-headless"
GENERIC_ALL = 0x10000000


class STARTUPINFOW(ctypes.Structure):
    _fields_ = [("cb", wt.DWORD), ("lpReserved", wt.LPWSTR), ("lpDesktop", wt.LPWSTR), ("lpTitle", wt.LPWSTR),
                ("dwX", wt.DWORD), ("dwY", wt.DWORD), ("dwXSize", wt.DWORD), ("dwYSize", wt.DWORD),
                ("dwXCountChars", wt.DWORD), ("dwYCountChars", wt.DWORD), ("dwFillAttribute", wt.DWORD),
                ("dwFlags", wt.DWORD), ("wShowWindow", wt.WORD), ("cbReserved2", wt.WORD),
                ("lpReserved2", ctypes.c_void_p), ("hStdInput", wt.HANDLE), ("hStdOutput", wt.HANDLE),
                ("hStdError", wt.HANDLE)]


class PROCESS_INFORMATION(ctypes.Structure):
    _fields_ = [("hProcess", wt.HANDLE), ("hThread", wt.HANDLE), ("dwProcessId", wt.DWORD), ("dwThreadId", wt.DWORD)]


def reloaded() -> tuple[Path, Path]:
    """Reloaded-II's launcher and its Mods folder, from its own settings."""
    settings = json.loads((Path(os.environ["APPDATA"]) / "Reloaded-Mod-Loader-II" / "ReloadedII.json")
                          .read_text(encoding="utf-8-sig"))
    return Path(settings["LauncherPath"]), Path(settings["ModConfigDirectory"])


def write_png(path: Path, width: int, height: int, bgra: bytes) -> None:
    rows = b"".join(b"\0" + bytes(x for i in range(width) for x in (bgra[(y * width + i) * 4 + 2],
                                                                       bgra[(y * width + i) * 4 + 1],
                                                                       bgra[(y * width + i) * 4]))
                    for y in range(height))

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows, 6)) + chunk(b"IEND", b""))


def screenshot(desktop: int, path: Path) -> str:
    """The largest window on the hidden desktop, drawn by PrintWindow (with its DirectX content), as a PNG at half size."""
    user32, gdi32 = ctypes.windll.user32, ctypes.windll.gdi32
    found = []

    @ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
    def each(hwnd, _):
        r = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        found.append(((r.right - r.left) * (r.bottom - r.top), hwnd, r.right - r.left, r.bottom - r.top))
        return True
    user32.EnumDesktopWindows(desktop, each, 0)
    if not found:
        return "no window"
    _, hwnd, width, height = max(found)
    if width <= 0 or height <= 0:
        return "no window"
    screen = user32.GetDC(0)
    dc = gdi32.CreateCompatibleDC(screen)
    bitmap = gdi32.CreateCompatibleBitmap(screen, width, height)
    gdi32.SelectObject(dc, bitmap)
    ok = user32.PrintWindow(hwnd, dc, 2)  # PW_RENDERFULLCONTENT
    half_w, half_h = width // 2, height // 2
    small = gdi32.CreateCompatibleDC(screen)
    small_bitmap = gdi32.CreateCompatibleBitmap(screen, half_w, half_h)
    gdi32.SelectObject(small, small_bitmap)
    gdi32.SetStretchBltMode(small, 4)
    gdi32.StretchBlt(small, 0, 0, half_w, half_h, dc, 0, 0, width, height, 0x00CC0020)

    class BITMAPINFOHEADER(ctypes.Structure):
        _fields_ = [("biSize", wt.DWORD), ("biWidth", wt.LONG), ("biHeight", wt.LONG), ("biPlanes", wt.WORD),
                    ("biBitCount", wt.WORD), ("biCompression", wt.DWORD), ("biSizeImage", wt.DWORD),
                    ("biXPelsPerMeter", wt.LONG), ("biYPelsPerMeter", wt.LONG), ("biClrUsed", wt.DWORD),
                    ("biClrImportant", wt.DWORD)]
    info = BITMAPINFOHEADER(biSize=40, biWidth=half_w, biHeight=-half_h, biPlanes=1, biBitCount=32)
    buffer = ctypes.create_string_buffer(half_w * half_h * 4)
    gdi32.GetDIBits(small, small_bitmap, 0, half_h, buffer, ctypes.byref(info), 0)
    for handle in (bitmap, small_bitmap):
        gdi32.DeleteObject(handle)
    gdi32.DeleteDC(dc)
    gdi32.DeleteDC(small)
    user32.ReleaseDC(0, screen)
    write_png(path, half_w, half_h, buffer.raw)
    return f"{width}x{height}, PrintWindow {ok}"


def game_running() -> bool:
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq FFT_enhanced.exe", "/NH"], capture_output=True, text=True).stdout
    return "FFT_enhanced.exe" in out


def end(image: str) -> None:
    subprocess.run(["taskkill", "/F", "/IM", image], capture_output=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--seconds", type=int, default=180)
    parser.add_argument("--script", type=Path, required=True, help="the autopilot script (it also mutes the game)")
    parser.add_argument("--shots", type=Path, help="a folder for a screenshot of the game's window every --every seconds")
    parser.add_argument("--every", type=float, default=5)
    args = parser.parse_args()
    cfg = load_config()
    game = Path(cfg["target"]["tic_dir"]) / "FFT_enhanced.exe"
    launcher, mods = reloaded()
    mod = mods / MOD_ID
    if game_running():
        sys.exit("the game is already running")
    logs = Path(os.environ["APPDATA"]) / "Reloaded-Mod-Loader-II" / "Logs"
    before = set(logs.glob("*.txt"))
    crash = mod / "crash.txt"
    crash.unlink(missing_ok=True)
    shutil.copy(args.script, mod / "autopilot.txt")

    user32, kernel32 = ctypes.WinDLL("user32", use_last_error=True), ctypes.WinDLL("kernel32", use_last_error=True)
    user32.CreateDesktopW.restype = wt.HANDLE
    desktop = user32.CreateDesktopW(DESKTOP, None, None, 0, GENERIC_ALL, None)
    if not desktop:
        sys.exit(f"CreateDesktop failed: {ctypes.get_last_error()}")
    started = time.time()
    try:
        si = STARTUPINFOW(cb=ctypes.sizeof(STARTUPINFOW), lpDesktop=DESKTOP)
        pi = PROCESS_INFORMATION()
        command = f'"{launcher}" --launch "{game}"'
        if not kernel32.CreateProcessW(None, ctypes.create_unicode_buffer(command), None, None, False, 0, None,
                                       str(launcher.parent), ctypes.byref(si), ctypes.byref(pi)):
            sys.exit(f"CreateProcess failed: {ctypes.get_last_error()}")
        kernel32.CloseHandle(pi.hThread)
        kernel32.CloseHandle(pi.hProcess)
        print(f"started on the hidden desktop: {command}")
        stop = threading.Event()

        def shoot() -> None:
            k = 0
            while not stop.wait(args.every):
                k += 1
                try:
                    what = screenshot(desktop, args.shots / f"{k:03d}.png")
                except Exception as e:  # noqa: BLE001
                    what = f"failed: {e}"
                if k <= 2 or what.startswith(("no", "failed")):
                    print(f"screenshot {k} at {time.time() - started:.0f} s: {what}")
        if args.shots:
            shutil.rmtree(args.shots, ignore_errors=True)
            args.shots.mkdir(parents=True)
            threading.Thread(target=shoot, daemon=True).start()
        seen = False
        while time.time() - started < args.seconds:
            time.sleep(2)
            running = game_running()
            seen = seen or running
            if seen and not running:
                print(f"the game exited after {time.time() - started:.0f} s")
                break
        else:
            print(f"stopped after {args.seconds} s")
    finally:
        if args.shots:
            stop.set()
        end("FFT_enhanced.exe")
        end("Reloaded-II.exe")
        time.sleep(1)
        user32.CloseDesktop(desktop)
        (mod / "autopilot.txt").unlink(missing_ok=True)
    new = sorted(set(logs.glob("*.txt")) - before, key=lambda p: p.stat().st_mtime)
    for log in new:
        print(f"--- {log.name}")
        for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            if "[Ivalice ReMixed]" in line or "error" in line.lower() or "autopilot" in line:
                print(line[:300])
    if crash.exists():
        print("--- crash.txt")
        print(crash.read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
