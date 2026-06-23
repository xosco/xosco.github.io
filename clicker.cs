using System;
using System.Runtime.InteropServices;
using System.Threading;

class Program
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("kernel32.dll")]
    static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport("kernel32.dll")]
    static extern bool QueryPerformanceFrequency(out long lpFrequency);

    // Управление точностью системного таймера (для точного Sleep в гибридном режиме)
    [DllImport("winmm.dll")]
    static extern uint timeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll")]
    static extern uint timeEndPeriod(uint uMilliseconds);

    // Привязка потока к конкретному ядру CPU
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

    // Явное выравнивание структуры для 64-битных систем
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_SCANCODE = 0x0008; // Флаг аппаратного нажатия (обязательно для ИГР)
    const int  VK_F6 = 0x75;

    // Аппаратные скан-коды (DirectInput) вместо виртуальных
    const ushort DIK_F = 0x21; // Скан-код клавиши F
    const ushort DIK_G = 0x22; // Скан-код клавиши G

    static volatile bool g_running     = true;
    static volatile bool g_active      = false;
    static volatile bool g_configuring = true;
    static volatile int  g_cps         = 50;
    static volatile int  g_toggleKey   = 0;
    // Режим клавиш: false = чередование F/G (меньше событий, выше FPS),
    //              true  = одновременно F+G (вдвое больше событий)
    static volatile bool g_simultaneous = false;

    // Совместное нажатие F+G (старый режим)
    static INPUT[] g_inputsDown = new INPUT[2];
    static INPUT[] g_inputsUp   = new INPUT[2];

    // Одиночные нажатия для режима чередования
    static INPUT[] g_fDown = new INPUT[1];
    static INPUT[] g_fUp   = new INPUT[1];
    static INPUT[] g_gDown = new INPUT[1];
    static INPUT[] g_gUp   = new INPUT[1];

    static int     g_inputSize = 0;

    static void Main(string[] args)
    {
        Console.Title = "Roblox Fixed X64 Clicker [F + G]";

        g_inputSize = Marshal.SizeOf(typeof(INPUT));
        
        // КЛАВИША F (Нажатие)
        g_inputsDown[0].type = INPUT_KEYBOARD;
        g_inputsDown[0].ki.wScan = DIK_F; // Используем скан-код
        g_inputsDown[0].ki.dwFlags = KEYEVENTF_SCANCODE; 

        // КЛАВИША F (Отпускание)
        g_inputsUp[0].type = INPUT_KEYBOARD;
        g_inputsUp[0].ki.wScan = DIK_F;
        g_inputsUp[0].ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;

        // КЛАВИША G (Нажатие)
        g_inputsDown[1].type = INPUT_KEYBOARD;
        g_inputsDown[1].ki.wScan = DIK_G;
        g_inputsDown[1].ki.dwFlags = KEYEVENTF_SCANCODE;

        // КЛАВИША G (Отпускание)
        g_inputsUp[1].type = INPUT_KEYBOARD;
        g_inputsUp[1].ki.wScan = DIK_G;
        g_inputsUp[1].ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;

        // Одиночные F (для режима чередования)
        g_fDown[0] = g_inputsDown[0];
        g_fUp[0]   = g_inputsUp[0];
        // Одиночные G
        g_gDown[0] = g_inputsDown[1];
        g_gUp[0]   = g_inputsUp[1];

        // Поднимаем точность системного таймера до 1 мс на всё время работы.
        // Без этого Sleep(1) реально спит 15.6 мс, и гибридный таймер не будет точным.
        timeBeginPeriod(1);

        Thread clickThread = new Thread(ClickerThread);
        // AboveNormal вместо Highest: не душим рендер-поток Roblox, но держим стабильный тайминг.
        clickThread.Priority     = ThreadPriority.AboveNormal;
        clickThread.IsBackground = true;
        clickThread.Start();

        bool keyWasPressed = false;

        while (g_running)
        {
            if (g_configuring)
            {
                ConfigureSettings();
                // ИСПРАВЛЕНО: Считываем текущее состояние клавиши, чтобы избежать моментального двойного клика
                keyWasPressed = (GetAsyncKeyState(g_toggleKey) & 0x8000) != 0;
            }

            if ((GetAsyncKeyState(VK_F6) & 0x8000) != 0)
            {
                g_active      = false;
                g_configuring = true;
                Thread.Sleep(500);
                continue;
            }

            if (g_toggleKey != 0)
            {
                bool keyIsPressed = (GetAsyncKeyState(g_toggleKey) & 0x8000) != 0;

                if (keyIsPressed && !keyWasPressed)
                {
                    g_active = !g_active;
                    Console.ForegroundColor = g_active ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.WriteLine(g_active ? "[СТАТУС: РАБОТАЕТ]" : "[СТАТУС: ПАУЗА]");
                    Console.ResetColor();
                }

                keyWasPressed = keyIsPressed;
            }

            Thread.Sleep(10);
        }

        timeEndPeriod(1);
    }

    static void ConfigureSettings()
    {
        g_active = false;
        Console.Clear();
        Console.WriteLine("=== НАСТРОЙКИ КЛИКЕРА (X64 СТРУКТУРА) ===");
        Console.WriteLine("Будут кликать: F и G");
        Console.WriteLine("\n1. Нажмите ЛЮБУЮ клавишу для ВКЛ/ВЫКЛ...");

        Thread.Sleep(500);

        bool found = false;
        while (!found)
        {
            for (int i = 8; i < 190; i++)
            {
                if (i == VK_F6) continue;
                if ((GetAsyncKeyState(i) & 0x8000) != 0)
                {
                    g_toggleKey = i;
                    Console.WriteLine("Клавиша выбрана! (Код: " + i + ")");
                    found = true;
                    break;
                }
            }
            Thread.Sleep(10);
        }

        Thread.Sleep(500);

        Console.Write("\n2. Введите CPS (Рекомендуется 20-50 для игр): ");
        string input = Console.ReadLine();

        int rawCps;
        if (!int.TryParse(input, out rawCps) || rawCps <= 0)
            rawCps = 50; // Ставим безопасный дефолт

        g_cps = rawCps;

        Console.WriteLine("\n3. Режим клавиш:");
        Console.WriteLine("   [1] Чередование F/G  (рекомендуется — вдвое меньше нагрузки, выше FPS)");
        Console.WriteLine("   [2] Одновременно F+G (старый режим, больше событий)");
        Console.Write("Выбор (Enter = 1): ");
        string modeInput = Console.ReadLine();
        g_simultaneous = (modeInput != null && modeInput.Trim() == "2");

        Console.Clear();
        Console.WriteLine("=== КЛИКЕР ГОТОВ ===");
        Console.WriteLine("Активация      : [Код " + g_toggleKey + "]");
        Console.WriteLine("Кнопки спама   : F + G (" + (g_simultaneous ? "одновременно" : "чередование") + ")");
        Console.WriteLine("Скорость (CPS) : ~" + g_cps);
        Console.WriteLine("-------------------------------------------");
        Console.WriteLine("1. Запустите игру от ИМЕНИ АДМИНИСТРАТОРА (важно!).");
        Console.WriteLine("2. Нажмите кнопку активации для старта.");
        Console.WriteLine("-------------------------------------------");

        g_configuring = false;
    }

    static void ClickerThread()
    {
        long frequency;
        QueryPerformanceFrequency(out frequency);

        // Привязываем поток кликера к последнему ядру CPU, чтобы он не конкурировал
        // за одно ядро с рендер-потоком Roblox (обычно ядро 0). Это главный фактор
        // сохранения FPS при высоком CPS.
        int cores = Environment.ProcessorCount;
        if (cores > 1)
        {
            UIntPtr mask = (UIntPtr)(1UL << (cores - 1)); // только старшее ядро
            SetThreadAffinityMask(GetCurrentThread(), mask);
        }

        bool useF = true; // какую клавишу жать в режиме чередования

        while (g_running)
        {
            if (g_active && !g_configuring)
            {
                double totalCycleTime = 1.0 / g_cps;
                double holdTime = totalCycleTime / 2.0;

                long t1;

                INPUT[] down, up;
                if (g_simultaneous)
                {
                    // Старый режим: F и G одновременно
                    down = g_inputsDown;
                    up   = g_inputsUp;
                }
                else
                {
                    // Чередование: один цикл F, следующий G. Оба бинда задействованы,
                    // но событий ввода вдвое меньше при том же CPS.
                    down = useF ? g_fDown : g_gDown;
                    up   = useF ? g_fUp   : g_gUp;
                    useF = !useF;
                }

                // Нажатие
                QueryPerformanceCounter(out t1);
                SendInput((uint)down.Length, down, g_inputSize);
                WaitPrecise(t1, holdTime, frequency);

                // Отпускание
                QueryPerformanceCounter(out t1);
                SendInput((uint)up.Length, up, g_inputSize);
                WaitPrecise(t1, totalCycleTime - holdTime, frequency);
            }
            else
            {
                Thread.Sleep(10);
            }
        }
    }

    // Гибридное ожидание: спим почти весь интервал (отдаём ядро системе и Roblox),
    // а последние ~0.6 мс добиваем спином ради точности. Так мы НЕ держим целое
    // ядро на 100%, что и освобождает CPU для рендера игры.
    // 2 мс: Sleep(1) даже при timeBeginPeriod(1) может длиться ~1–1.5 мс, поэтому
    // спим только пока до цели остаётся заметно больше. При высоком CPS (полупериод
    // < 2 мс) цикл сразу уходит в чистый спин — точность КПС не страдает.
    const double SPIN_MARGIN = 0.002;

    static void WaitPrecise(long startTicks, double targetSeconds, long frequency)
    {
        long t2;

        // Фаза сна: пока до цели остаётся больше запаса на спин — спим по 1 мс.
        if (targetSeconds > SPIN_MARGIN)
        {
            while (true)
            {
                QueryPerformanceCounter(out t2);
                double elapsed = (double)(t2 - startTicks) / frequency;
                if (elapsed >= targetSeconds - SPIN_MARGIN) break;
                Thread.Sleep(1);
            }
        }

        // Фаза спина: точное добивание оставшегося хвоста.
        do { QueryPerformanceCounter(out t2); }
        while ((double)(t2 - startTicks) / frequency < targetSeconds);
    }
}