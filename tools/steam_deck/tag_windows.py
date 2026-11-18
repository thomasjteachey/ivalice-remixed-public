"""Tags a program's windows for Gaming Mode on the Deck (docs/STEAM_DECK.md).

    python3 -I tag_windows.py <app id>

Gaming Mode only shows windows whose STEAM_GAME property is the running entry's app id. Steam's overlay sets it for
games, but it doesn't reach a program run through the Protontricks Flatpak (Reloaded-II, from reloaded.sh). Proton
names those windows' class steam_app_<app id>, so this tags every top-level window with that class as it appears,
dropdowns included, until the process that started it ends. One process, talking to X directly, so it stays cheap at
ten checks a second.
"""
import ctypes
import ctypes.util
import os
import sys
import time

app_id = int(sys.argv[1])
wanted = b"steam_app_%d" % app_id
parent = os.getppid()

x = ctypes.cdll.LoadLibrary(ctypes.util.find_library("X11"))
x.XOpenDisplay.restype = ctypes.c_void_p
x.XOpenDisplay.argtypes = [ctypes.c_char_p]
x.XDefaultRootWindow.restype = ctypes.c_ulong
x.XDefaultRootWindow.argtypes = [ctypes.c_void_p]
x.XInternAtom.restype = ctypes.c_ulong
x.XInternAtom.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_int]
x.XQueryTree.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.POINTER(ctypes.c_ulong), ctypes.POINTER(ctypes.c_ulong),
                         ctypes.POINTER(ctypes.POINTER(ctypes.c_ulong)), ctypes.POINTER(ctypes.c_uint)]


class ClassHint(ctypes.Structure):
    _fields_ = [("name", ctypes.c_void_p), ("cls", ctypes.c_void_p)]


x.XGetClassHint.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.POINTER(ClassHint)]
x.XChangeProperty.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_int, ctypes.c_int,
                              ctypes.c_void_p, ctypes.c_int]
x.XFree.argtypes = [ctypes.c_void_p]
x.XFlush.argtypes = [ctypes.c_void_p]

# A window can close between listing and reading it; X's default error handler would end the process then.
IGNORE = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p)(lambda display, event: 0)
x.XSetErrorHandler(IGNORE)

display = x.XOpenDisplay(None)
if not display:
    sys.exit("tag_windows: no X display")
root = x.XDefaultRootWindow(display)
steam_game = x.XInternAtom(display, b"STEAM_GAME", 0)
CARDINAL, REPLACE = 6, 0
value = (ctypes.c_ulong * 1)(app_id)
done: set[int] = set()

while True:
    try:
        os.kill(parent, 0)
    except OSError:
        break
    root_ret, parent_ret = ctypes.c_ulong(), ctypes.c_ulong()
    children, count = ctypes.POINTER(ctypes.c_ulong)(), ctypes.c_uint()
    if x.XQueryTree(display, root, ctypes.byref(root_ret), ctypes.byref(parent_ret), ctypes.byref(children),
                    ctypes.byref(count)):
        current = {children[i] for i in range(count.value)}
        if children:
            x.XFree(children)
        for window in current - done:
            hint = ClassHint()
            if x.XGetClassHint(display, window, ctypes.byref(hint)):
                ours = ctypes.string_at(hint.cls) == wanted if hint.cls else False
                for p in (hint.name, hint.cls):
                    if p:
                        x.XFree(p)
                if ours:
                    x.XChangeProperty(display, window, steam_game, CARDINAL, 32, REPLACE, value, 1)
                done.add(window)  # a window without its class yet is read again next time
        done &= current
        x.XFlush(display)
    time.sleep(0.1)
