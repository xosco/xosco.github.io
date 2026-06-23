#NoTrayIcon
#Requires AutoHotkey v1.1+
SetBatchLines, -1
CoordMode, Pixel, Screen
CoordMode, Mouse, Screen

; -----------------------
; Settings
; -----------------------
Tolerance          := 40
StabilizationLoops := 3
ActivationKey      := "xbutton1"        ; left click mode
AltActivationKey   := "XButton2" ; Q press mode

; -----------------------
; Colors
; -----------------------
BaseColors := [0x800008, 0xCC4D4E, 0xDD5A5F]
BGRColors  := [0x85002b,0xdd5c89,0xa52b63,0x057e00,0x5ddb5b,0x28ae3b,0x85007d,0xdd5cdb,0x9b20aa,0x777e00,0xdddb5c,0xacba48]

BGRtoRGB(c) {
    return ((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF)
}

Targets := []
for _, c in BaseColors
    Targets.Push(c)
for _, c in BGRColors
    Targets.Push(BGRtoRGB(c))

; -----------------------
; State
; -----------------------
MatchStreak := 0
NonMatchStreak := 0
Triggered := false

; -----------------------
; Main loop
; -----------------------
Loop {
    keyE   := GetKeyState(ActivationKey, "P")
    keyAlt := GetKeyState(AltActivationKey, "P")

    if (keyE || keyAlt) {
        MouseGetPos, mx, my
        PixelGetColor, curColor, mx, my, RGB Fast

        if (CheckMatch(curColor, Targets, Tolerance)) {
            MatchStreak++
            NonMatchStreak := 0
        } else {
            NonMatchStreak++
            MatchStreak := 0
        }

        if (MatchStreak >= StabilizationLoops && !Triggered) {
            Triggered := true
            if (keyE)
                MouseClick, left
            else if (keyAlt)
                Send, q
        }

        if (NonMatchStreak >= StabilizationLoops)
            Triggered := false
    } else {
        MatchStreak := 0
        NonMatchStreak := 0
        Triggered := false
    }

    Sleep, 1
}

; -----------------------
; Helpers
; -----------------------
CheckMatch(c, targets, tol) {
    r := (c >> 16) & 0xFF, g := (c >> 8) & 0xFF, b := c & 0xFF
    for _, t in targets {
        tr := (t >> 16) & 0xFF, tg := (t >> 8) & 0xFF, tb := t & 0xFF
        if (Abs(r - tr) <= tol && Abs(g - tg) <= tol && Abs(b - tb) <= tol)
            return true
    }
    return false
}
