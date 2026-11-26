// 洛琪希开机动画（水王级魔导师 · 豪雷积层云）· Windows 版 —— 外置浮层小程序
// 检测 Antigravity 启动 → 盖在它的主窗口上播放洛琪希开屏动画 → 进入雨后星空循环帧后半透明、点击穿透，作为循环背景一直播放。
// 不修改 Antigravity 的任何文件。用系统自带的 .NET Framework 4.x 编译器编译（C# 5 语法），不需要装任何 SDK。
//
// Based on ai-boot-animations (https://github.com/wynneceles/ai-boot-animations)
// Copyright (C) 2026 wynneceles (adapted for Roxy Migurdia theme)
// 本程序以 GNU GPL v3 发布，见 LICENSE。
// This program is free software under the GNU General Public License v3.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using WF = System.Windows.Forms;
using SD = System.Drawing;

namespace RoxyAntigravityBoot
{
    // ───────────────────────── 配置 ─────────────────────────

    public class Subtitle
    {
        public string Text; public double Start; public double End;
        public Subtitle(string t, double s, double e) { Text = t; Start = s; End = e; }
    }

    public class Config
    {
        public bool PlayIntro = true;            // Antigravity 启动时播放开屏动画
        public bool LoopBackground = true;       // 开屏后保留半透明循环背景
        public double FadeStartSeconds = 9.90;   // 12s 开屏在 9.9s 星芒散逸后开启 2.1s 五次方丝滑淡出，12.0s 无缝接 loop-x2.mp4
        public double FadeDurationSeconds = 2.1; // 从不透明平滑过渡到半透明待机用时
        public double ResidualAlpha = 0.25;      // 循环背景不透明度
        public double IntroVolume = 0.85;        // 开屏音量（再乘系统音量）
        public bool ShowSubtitles = false;       // 成片已内嵌电影级排版，默认不另画字幕
        public double SubtitleFadeSeconds = 0.5;
        public double SubtitleWidthRatio = 0.6;
        public List<Subtitle> Subtitles = new List<Subtitle> {
            new Subtitle("Cumulonimbus — Water King Tier.", 1.0, 4.2),
            new Subtitle("Welcome back, Roxy Migurdia.", 7.5, 9.6),
        };

        public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoxyAntigravityBoot"); } }
        public static string FilePath { get { return Path.Combine(Dir, "config.json"); } }

        static double D(Dictionary<string, object> d, string k, double def)
        {
            object v; if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { } }
            return def;
        }
        static bool B(Dictionary<string, object> d, string k, bool def)
        {
            object v; if (d.TryGetValue(k, out v) && v is bool) return (bool)v;
            return def;
        }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (File.Exists(FilePath))
                {
                    var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                    if (d != null)
                    {
                        c.PlayIntro = B(d, "playIntro", c.PlayIntro);
                        c.LoopBackground = B(d, "loopBackground", c.LoopBackground);
                        c.FadeStartSeconds = D(d, "fadeStartSeconds", c.FadeStartSeconds);
                        c.FadeDurationSeconds = D(d, "fadeDurationSeconds", c.FadeDurationSeconds);
                        c.ResidualAlpha = D(d, "residualAlpha", c.ResidualAlpha);
                        c.IntroVolume = D(d, "introVolume", c.IntroVolume);
                        c.ShowSubtitles = B(d, "showSubtitles", c.ShowSubtitles);
                        c.SubtitleFadeSeconds = D(d, "subtitleFadeSeconds", c.SubtitleFadeSeconds);
                        c.SubtitleWidthRatio = D(d, "subtitleWidthRatio", c.SubtitleWidthRatio);
                        object subs;
                        if (d.TryGetValue("subtitles", out subs) && subs is object[])
                        {
                            var list = new List<Subtitle>();
                            foreach (var o in (object[])subs)
                            {
                                var s = o as Dictionary<string, object>;
                                if (s == null || !s.ContainsKey("text")) continue;
                                list.Add(new Subtitle(Convert.ToString(s["text"]), D(s, "start", 0), D(s, "end", 0)));
                            }
                            c.Subtitles = list;
                        }
                    }
                }
            }
            catch (Exception e) { Log.W("设置文件读不了，用默认值：" + e.Message); }
            c.Save(); // 补齐新字段
            return c;
        }

        static string N(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        static string L(bool v) { return v ? "true" : "false"; }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var js = new JavaScriptSerializer();
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"fadeDurationSeconds\" : " + N(FadeDurationSeconds) + ",\n");
                sb.Append("  \"fadeStartSeconds\" : " + N(FadeStartSeconds) + ",\n");
                sb.Append("  \"introVolume\" : " + N(IntroVolume) + ",\n");
                sb.Append("  \"loopBackground\" : " + L(LoopBackground) + ",\n");
                sb.Append("  \"playIntro\" : " + L(PlayIntro) + ",\n");
                sb.Append("  \"residualAlpha\" : " + N(ResidualAlpha) + ",\n");
                sb.Append("  \"showSubtitles\" : " + L(ShowSubtitles) + ",\n");
                sb.Append("  \"subtitleFadeSeconds\" : " + N(SubtitleFadeSeconds) + ",\n");
                sb.Append("  \"subtitleWidthRatio\" : " + N(SubtitleWidthRatio) + ",\n");
                sb.Append("  \"subtitles\" : [\n");
                for (int i = 0; i < Subtitles.Count; i++)
                {
                    var s = Subtitles[i];
                    sb.Append("    {\n      \"end\" : " + N(s.End) + ",\n      \"start\" : " + N(s.Start) + ",\n      \"text\" : " + js.Serialize(s.Text) + "\n    }");
                    sb.Append(i < Subtitles.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ]\n}\n");
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e) { Log.W("设置文件写不了：" + e.Message); }
        }
    }

    // ───────────────────────── 日志 ─────────────────────────

    /// 双显卡笔记本上 Windows 默认让新程序用核显，播 4K 视频会掉帧。
    /// 启动时检查有没有两块以上显卡：有，就在「设置 → 系统 → 显示 → 图形」里把本程序设为「高性能」（等同用户手动去选）。
    /// 用户自己在那里选过（不管选了什么）就不再动；只有一块显卡什么都不做。卸载时 uninstall.ps1 会删掉这一项。
    public static class GpuPref
    {
        const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";
        public static void Ensure()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                using (var k = Registry.CurrentUser.CreateSubKey(Key))
                {
                    if (k == null) return;
                    var cur = k.GetValue(exe) as string;
                    if (cur != null) { Log.W("显卡偏好已有设置（" + cur + "），不改动"); return; }
                    var gpus = ListGpus();
                    if (gpus.Count < 2) { Log.W("显卡：" + (gpus.Count == 0 ? "未识别" : string.Join("、", gpus)) + "，不用切换独显"); return; }
                    k.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
                    Log.W("检测到多块显卡（" + string.Join("、", gpus) + "），已设为用高性能显卡渲染");
                }
            }
            catch (Exception ex) { Log.W("独显设置失败：" + ex.Message); }
        }

        /// 列出真实显卡（排除远程桌面、虚拟显示器这类假适配器）
        static List<string> ListGpus()
        {
            var list = new List<string>();
            using (var q = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
            {
                foreach (System.Management.ManagementBaseObject o in q.Get())
                {
                    var name = (o["Name"] as string ?? "").Trim();
                    if (System.Text.RegularExpressions.Regex.IsMatch(name, "NVIDIA|GeForce|Quadro|RTX|AMD|Radeon|Intel|Arc", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        list.Add(name);
                }
            }
            return list;
        }
    }

    public static class Log
    {
        public static string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RoxyAntigravityBoot", "log.txt");
        static readonly object Lock = new object();
        public static void W(string msg)
        {
            try
            {
                lock (Lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024) fi.Delete();
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + msg + "\r\n", Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    // ───────────────────────── Win32 ─────────────────────────

    public static class Native
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);
        public static void EnablePerMonitorDpi()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { } // PerMonitorV2
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }                  // PerMonitor
            try { SetProcessDPIAware(); } catch { }
        }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong32(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GetWindowLongPtr64(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr h, int i, int v);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr h, int i, IntPtr v);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
        [DllImport("user32.dll")] public static extern IntPtr RegisterPowerSettingNotification(IntPtr h, ref Guid g, int flags);
        [DllImport("user32.dll")] public static extern bool UnregisterPowerSettingNotification(IntPtr h);
        /// 显示器亮 / 灭 / 变暗的通知
        public static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        public const int GWL_EXSTYLE = -20, GWLP_HWNDPARENT = -8;
        public const long WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x8000000, WS_EX_APPWINDOW = 0x40000;
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_NOOWNERZORDER = 0x200;
        public const uint LWA_ALPHA = 2, GW_OWNER = 4;

        public static long GetLong(IntPtr h, int i) { return IntPtr.Size == 8 ? GetWindowLongPtr64(h, i).ToInt64() : GetWindowLong32(h, i); }
        public static void SetLong(IntPtr h, int i, long v)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(h, i, new IntPtr(v)); else SetWindowLong32(h, i, (int)v);
        }

        /// 窗口在屏幕上实际占的矩形（去掉 Windows 10/11 看不见的阴影边框），物理像素
        public static bool Bounds(IntPtr h, out RECT r)
        {
            if (DwmGetWindowAttribute(h, 9 /*DWMWA_EXTENDED_FRAME_BOUNDS*/, out r, Marshal.SizeOf(typeof(RECT))) == 0 && r.Right > r.Left) return true;
            return GetWindowRect(h, out r);
        }
        public static bool Cloaked(IntPtr h)
        {
            int v; return DwmGetWindowAttribute(h, 14 /*DWMWA_CLOAKED*/, out v, 4) == 0 && v != 0;
        }
        public static string ClassOf(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
        public static string TitleOf(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
    }

    // ───────────────────────── 找 Antigravity 的主窗口 ─────────────────────────

    public struct Target { public IntPtr Hwnd; public uint Pid; public bool Valid { get { return Hwnd != IntPtr.Zero; } } }

    public static class Finder
    {
        /// 测试用：按窗口标题找（比如记事本），不找 Antigravity
        public static string TestTitle;
        static readonly Dictionary<uint, string> NameCache = new Dictionary<uint, string>();

        static string ProcName(uint pid)
        {
            string n;
            if (NameCache.TryGetValue(pid, out n)) return n;
            try { n = Process.GetProcessById((int)pid).ProcessName; } catch { n = ""; }
            if (NameCache.Count > 500) NameCache.Clear();
            NameCache[pid] = n;
            return n;
        }

        /// 找 Antigravity 最大的那个可见主窗口。Antigravity 是 Electron 程序，会有好几个进程和隐藏窗口，只挑看得见、够大的顶层窗口
        public static Target Find()
        {
            Target best = new Target(); long bestArea = 0;
            uint self = (uint)Process.GetCurrentProcess().Id;
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h) || Native.Cloaked(h)) return true;
                if (Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == self) return true;
                if (TestTitle != null)
                {
                    if (Native.TitleOf(h).IndexOf(TestTitle, StringComparison.OrdinalIgnoreCase) < 0) return true;
                }
                else
                {
                    // Antigravity（VS Code 系的 Electron 程序），窗口进程 Antigravity.exe
                    string pn = ProcName(pid);
                    if (!string.Equals(pn, "antigravity", StringComparison.OrdinalIgnoreCase)) return true;
                    if (Native.GetWindowTextLength(h) == 0) return true;
                }
                Native.RECT r; if (!Native.Bounds(h, out r)) return true;
                int w = r.Right - r.Left, ht = r.Bottom - r.Top;
                if (w < 400 || ht < 300) return true;
                long area = (long)w * ht;
                if (area > bestArea) { bestArea = area; best.Hwnd = h; best.Pid = pid; }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        public static bool ProcessAlive(uint pid)
        {
            try { return !Process.GetProcessById((int)pid).HasExited; } catch { return false; }
        }
    }

    // ───────────────────────── 字幕 ─────────────────────────

    /// 英文花体字幕：Libre Bodoni 斜体，白字 + 黑色描边 + 柔和阴影
    public class SubtitleElement : FrameworkElement
    {
        public static FontFamily Font = new FontFamily("Georgia");
        string text;
        public double WidthRatio = 0.6;
        public string RefText = "Under the moonlight... the blade awakens.";

        public SubtitleElement() { IsHitTestVisible = false; Opacity = 0; }
        public void SetText(string t) { text = t; InvalidateVisual(); }

        FormattedText Make(string s, double size)
        {
#pragma warning disable 618
            return new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(Font, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal), size, Brushes.White);
#pragma warning restore 618
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (string.IsNullOrEmpty(text) || ActualWidth < 10) return;
            double W = ActualWidth, H = ActualHeight;
            // 字号：让第一句约占窗口宽度的 WidthRatio；窗口特别扁时按高度限一下
            double refW = Make(RefText, 100).WidthIncludingTrailingWhitespace;
            double size = refW > 0 ? W * WidthRatio * 100 / refW : 24;
            size = Math.Max(18, Math.Min(size, H * 0.09));
            var ft = Make(text, size);
            double x = (W - ft.Width) / 2, y = H * 0.85 - ft.Height / 2; // 字的中线在离底边 15% 处
            var geo = ft.BuildGeometry(new Point(x, y));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 0, 0)), size * 0.1) { LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, pen, geo);
            dc.DrawGeometry(Brushes.White, null, geo);
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = size * 0.25, ShadowDepth = 0, Opacity = 0.75 };
        }
    }

    // ───────────────────────── 开发用参数（排查循环背景卡住）─────────────────────────

    public static class Dev
    {
        public static double LoopCutSec = 0;    // --loop-cut 秒：循环段放到这里就提前换播放器，接缝次数成倍增加，几分钟跑完平时几小时的接缝
        public static double StallTestSec = 0;  // --stall-test 秒：每隔这么久故意把正在放的播放器暂停，看自愈能不能接回来
        public static int StallMs = 3000;       // 画面多久没动算卡住
        public static int RecycleEvery = 10;    // --recycle-every 圈：每放这么多圈，把后台待命的播放器换成新的（0 = 不换）
        public static double HeartbeatSec = 600; // --heartbeat 秒：循环背景每隔多久往日志里报一次平安
    }

    // ───────────────────────── 一次播放 ─────────────────────────

    public class Show
    {
        enum Phase { Intro, Revealed, Ending, Done }
        Phase phase = Phase.Intro;

        public readonly Target Target;
        Config config;
        readonly bool withIntro;
        readonly Action onDone;

        Window overlay, top;
        IntPtr overlayH, topH;
        Grid grid, root;
        int styleFixes;
        MediaElement intro, loopA, loopB, activeLoop;
        // 换播放器时：新的那个真正出帧之前，旧的一直盖在上面（不然机器忙的时候会闪黑）
        MediaElement swapFrom, swapTo; DateTime swapT0; bool swapKicked;
        // 重叠交接：loop-x2.mp4 是循环段连放两遍，前后两半画面一模一样。
        // 正在放的播放器走到中点（第二遍开头）时，另一个从头开始放，两个同时放着同样的画面；
        // 新的稳稳走起来后再换到上面、旧的倒回开头待命。谁都放不到文件末尾，也就没有「放完再接」那一下闪。
        public static bool ForceLegacyLoop;   // 测试用：--legacy-loop 强制老办法
        bool overlapLoop;
        string loopFile = "loop.mp4";
        MediaElement handFrom, handTo; DateTime handT0;
        double handLead;                      // 新播放器起步慢多少秒，下次提前这么多开播（自动学）
        int handoffs;

        // ── 循环背景自愈 ──
        // 循环靠「放完 → 换另一个播放器接着放」一环扣一环，任何一环断了（没收到放完通知、新播放器没动起来、
        // 锁屏 / 睡眠 / 显示器休眠后画面冻住）以前都会永远停在一帧。现在每 0.25 秒看一眼进度，卡住就重建播放器。
        readonly Stopwatch clock = Stopwatch.StartNew();
        long nextSampleMs, lastSampleMs, lastMoveMs, lastRecoverMs = -1, nextBeatMs, nextStallTestMs, pendingAt;
        double lastPos = -1;
        int laps, recoveries, failStreak, missedEnds;
        bool verifying;
        string pendingWhy;
        MediaElement retiring; long retireT0;   // 重建时旧画面先盖着，新播放器动起来再撤
        Dispatcher disp;
        SessionSwitchEventHandler onSession;
        EventHandler onDisplay;
        IntPtr powerNotify;
        int displayState = -1;

        SubtitleElement sub;
        Border skipBtn;
        DispatcherTimer tick;
        int currentSub = -1;
        bool introGone, hidden, introFadeInDone;
        DateTime introStartWait;
        Native.RECT lastRect;

        // 整窗透明度的渐变
        double alpha, alphaFrom, alphaTo; DateTime alphaT0; double alphaDur = -1; Action alphaThen;
        bool clickThrough;
        // 开屏音量渐变
        double volFrom, volTo; DateTime volT0; double volDur = -1;

        static string Asset(string name) { return Path.Combine(App.AssetsDir, name); }

        public Show(Target t, Config c, bool withIntro, Action onDone)
        {
            Target = t; config = c; this.withIntro = withIntro; this.onDone = onDone;

            grid = new Grid { Background = withIntro ? Brushes.Black : Brushes.Transparent, ClipToBounds = true };
            // 有 loop-x2.mp4（循环段连放两遍）就用「重叠交接」；
            // 用户放了自己的 loop.mp4（比 loop-x2.mp4 新）或只有 loop.mp4，就用老办法，免得换的视频被无视
            string x2 = Asset("loop-x2.mp4"), single = Asset("loop.mp4");
            overlapLoop = !ForceLegacyLoop && File.Exists(x2)
                && !(File.Exists(single) && File.GetLastWriteTimeUtc(single) > File.GetLastWriteTimeUtc(x2).AddSeconds(2));
            loopFile = overlapLoop ? "loop-x2.mp4" : "loop.mp4";
            Log.W(overlapLoop ? "循环段：重叠交接（loop-x2.mp4）" : "循环段：放完再接（loop.mp4）");
            loopA = MakeLoopPlayer();
            loopB = MakeLoopPlayer();
            Panel.SetZIndex(loopA, 1); Panel.SetZIndex(loopB, 0);
            grid.Children.Add(loopA); grid.Children.Add(loopB);
            activeLoop = loopA;

            if (withIntro)
            {
                intro = MakePlayer("intro.mp4", false);
                intro.Volume = config.IntroVolume;
                Panel.SetZIndex(intro, 2);
                grid.Children.Add(intro);
                intro.MediaEnded += delegate { IntroEnded(); };
                intro.MediaFailed += delegate (object s, ExceptionRoutedEventArgs e) { Log.W("开屏视频播不了：" + e.ErrorException.Message); };
            }
            else introGone = true;

            // 一个窗口三层：最下面视频（整体调透明度做半透明背景），上面字幕和「跳过」（不跟着变淡）
            root = new Grid { Background = Brushes.Transparent };
            root.Children.Add(grid);
            overlay = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
                ShowActivated = false, Background = Brushes.Transparent, Content = root,
                Left = -32000, Top = -32000, Width = 10, Height = 10, Title = "Roxy Boot Animation"
            };
            clickThrough = !withIntro;
            alpha = 0; // 开屏也从透明淡入（0.3 秒），不再一出现就整块黑屏盖上去
            grid.Opacity = alpha;
            overlay.SourceInitialized += delegate
            {
                overlayH = new WindowInteropHelper(overlay).Handle;
                EnsureOverlayStyle(true);
                // 收系统的睡眠唤醒、显示器亮灭通知（这两种之后视频画面最容易冻住）
                var hs = HwndSource.FromHwnd(overlayH);
                if (hs != null) hs.AddHook(WndProc);
                try { Guid g = Native.GUID_CONSOLE_DISPLAY_STATE; powerNotify = Native.RegisterPowerSettingNotification(overlayH, ref g, 0); }
                catch (Exception ex) { Log.W("显示器亮灭通知注册失败：" + ex.Message); }
                // 设成 Antigravity 窗口的「附属窗口」：系统自动让它一直在 Antigravity 上面，Antigravity 被别的窗口挡住时它也一起被挡住
                Native.SetLong(overlayH, Native.GWLP_HWNDPARENT, Target.Hwnd.ToInt64());
            };

            if (withIntro)
            {
                var topGrid = root;
                sub = new SubtitleElement { WidthRatio = config.SubtitleWidthRatio };
                if (config.Subtitles.Count > 0) sub.RefText = config.Subtitles[0].Text;
                topGrid.Children.Add(sub);
                skipBtn = new Border
                {
                    Width = 72, Height = 28, CornerRadius = new CornerRadius(14),
                    Background = new SolidColorBrush(Color.FromArgb(115, 0, 0, 0)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 44, 18, 0), Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = "跳过 ›", Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Medium,
                        FontFamily = new FontFamily("Microsoft YaHei UI"),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    }
                };
                skipBtn.MouseLeftButtonUp += delegate { SkipIntro(); };
                topGrid.Children.Add(skipBtn);
            }

            overlay.Show();
            EnsureOverlayStyle(true); // WPF 显示窗口时会重写扩展样式，显示后再补一次
            if (top != null) top.Show();
            Follow(true);
            Log.W("浮层样式 0x" + Native.GetLong(overlayH, Native.GWL_EXSTYLE).ToString("X"));

            if (withIntro)
            {
                introStartWait = DateTime.Now;
                intro.Play();
                // 等视频解码器真正出帧后再淡入，彻底消除启动瞬间的黑屏等待
                Log.W("准备播放开屏动画（等待首帧解码），窗口 " + Target.Hwnd + " " + RectStr(lastRect));
            }
            else
            {
                loopA.Play();
                phase = Phase.Revealed;
                RampAlpha(config.ResidualAlpha, config.FadeDurationSeconds, null);
                Log.W("直接开启循环背景，窗口 " + Target.Hwnd);
            }

            // 解锁、远程桌面切回、换显示器 / 改分辨率之后，重建一次播放器
            disp = Dispatcher.CurrentDispatcher;
            onSession = delegate (object s, SessionSwitchEventArgs e)
            {
                var r = e.Reason;
                if (r == SessionSwitchReason.SessionUnlock || r == SessionSwitchReason.ConsoleConnect || r == SessionSwitchReason.RemoteConnect)
                    disp.BeginInvoke(new Action(delegate { LaterRebuild("解锁 / 切回桌面（" + r + "）"); }));
            };
            onDisplay = delegate { disp.BeginInvoke(new Action(delegate { LaterRebuild("显示器设置变了"); })); };
            try { SystemEvents.SessionSwitch += onSession; SystemEvents.DisplaySettingsChanged += onDisplay; }
            catch (Exception ex) { Log.W("系统事件订阅失败：" + ex.Message); }
            nextBeatMs = (long)(Dev.HeartbeatSec * 1000);
            nextStallTestMs = (long)(Dev.StallTestSec * 1000);

            tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            tick.Tick += delegate { Tick(); };
            tick.Start();
        }

        IntPtr WndProc(IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x02E0 /*WM_DPICHANGED*/)
            {
                // 跨不同缩放比例显示器拖动时，让 WPF 正常更新当前屏幕 DPI，并在下一拍立即把浮层贴回 Antigravity 的物理像素坐标
                if (disp != null) disp.BeginInvoke(new Action(delegate { Follow(true); }));
                return IntPtr.Zero;
            }
            if (msg != 0x218 /*WM_POWERBROADCAST*/) return IntPtr.Zero;
            try
            {
                long ev = wParam.ToInt64();
                if (ev == 0x7 /*PBT_APMRESUMESUSPEND*/ || ev == 0x12 /*PBT_APMRESUMEAUTOMATIC*/) LaterRebuild("电脑从睡眠中唤醒");
                else if (ev == 0x8013 /*PBT_POWERSETTINGCHANGE*/ && lParam != IntPtr.Zero)
                {
                    // POWERBROADCAST_SETTING：16 字节 GUID + 4 字节长度 + 数据（0 灭、1 亮、2 变暗）
                    var g = (Guid)Marshal.PtrToStructure(lParam, typeof(Guid));
                    if (g == Native.GUID_CONSOLE_DISPLAY_STATE)
                    {
                        int state = Marshal.ReadInt32(lParam, 20);
                        // 刚注册时系统会先报一次当前状态，那次不算；只有从灭 / 暗变成亮才重建
                        if (state == 1 && displayState >= 0 && displayState != 1) LaterRebuild("显示器重新亮起");
                        if (state != displayState && displayState >= 0) Log.W("显示器状态 " + displayState + " → " + state);
                        displayState = state;
                    }
                }
            }
            catch (Exception ex) { Log.W("电源通知处理出错：" + ex.Message); }
            return IntPtr.Zero;
        }

        static string RectStr(Native.RECT r) { return "(" + r.Left + "," + r.Top + " " + (r.Right - r.Left) + "x" + (r.Bottom - r.Top) + ")"; }

        MediaElement MakePlayer(string file, bool muted)
        {
            var m = new MediaElement
            {
                LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.UniformToFill, IsMuted = muted, ScrubbingEnabled = muted,
                Source = new Uri(Asset(file), UriKind.Absolute)
            };
            if (muted)
            {
                // 预先停在第一帧，轮到它时立刻接上，不会黑一下
                bool primed = false;
                m.MediaOpened += delegate
                {
                    if (primed) return; primed = true;
                    if ((m != activeLoop && m != handTo) || phase == Phase.Intro) { m.Pause(); m.Position = TimeSpan.Zero; }
                };
                m.Play();
            }
            return m;
        }

        MediaElement MakeLoopPlayer()
        {
            var m = MakePlayer(loopFile, true);
            m.MediaEnded += OnLoopEnded;
            m.MediaFailed += OnLoopFailed;
            return m;
        }

        MediaElement OtherLoop(MediaElement m) { return m == loopA ? loopB : loopA; }

        void OnLoopEnded(object s, RoutedEventArgs e)
        {
            var m = s as MediaElement;
            if (m == null || m != activeLoop || phase == Phase.Done) return; // 已经换下去的、或已经重建掉的旧播放器，不理它
            SwapLoop(m, OtherLoop(m));
        }

        void OnLoopFailed(object s, ExceptionRoutedEventArgs e)
        {
            if (s != loopA && s != loopB) return;
            Log.W("循环背景播放器报错：" + (e.ErrorException != null ? e.ErrorException.Message : "未知"));
            LaterRebuild("播放器报错", 500);
        }

        /// 关掉一个播放器并从画面里拿走
        void DisposePlayer(MediaElement m)
        {
            if (m == null) return;
            m.MediaEnded -= OnLoopEnded;
            m.MediaFailed -= OnLoopFailed;
            try { m.Stop(); m.Close(); } catch { }
            grid.Children.Remove(m);
        }

        /// 两个播放器轮流放循环段：一个放完，另一个已经停在第一帧，马上接上
        void SwapLoop(MediaElement done, MediaElement next)
        {
            if (phase == Phase.Done) return;
            handFrom = null; handTo = null; // 重叠交接没赶上、退回老办法时，交接状态作废
            next.Play();
            activeLoop = next;
            BeginSwap(done, next); // done 停在最后一帧盖着，等 next 动起来再换上去
        }

        void BeginSwap(MediaElement from, MediaElement to)
        {
            if (swapFrom != null && swapFrom != from) FinishSwap();
            swapFrom = from; swapTo = to; swapT0 = DateTime.Now; swapKicked = false;
        }

        // 每帧检查：新播放器的进度走起来了（画面已经出来），才把旧的撤掉。
        // 以前等 1.5 秒没动也照样换上去——换上去的是个没在放的播放器，它永远不会「放完」，循环就此停住。
        // 现在没动就再催一次 Play，旧画面继续盖着；还不动交给自愈去重建。
        void CheckSwap()
        {
            if (swapFrom == null) return;
            if (swapTo.Position.TotalSeconds > (swapFrom == intro ? 0.15 : 0.06)) { FinishSwap(); return; }
            if (!swapKicked && (DateTime.Now - swapT0).TotalSeconds > 1.5)
            {
                swapKicked = true;
                Log.W("接缝：新播放器 1.5 秒还没动，再催一次（第 " + laps + " 圈）");
                swapTo.Play();
            }
        }

        void FinishSwap()
        {
            var from = swapFrom; var to = swapTo;
            swapFrom = null; swapTo = null;
            if (from == intro)
            {
                intro.Visibility = Visibility.Collapsed;
                intro.Stop(); intro.Close();
                return;
            }
            Promote(from, to);
        }

        /// to 换到上面成为正在放的，from 退回第一帧待命（每隔几圈换成新的）
        void Promote(MediaElement from, MediaElement to)
        {
            Panel.SetZIndex(to, 1); Panel.SetZIndex(from, 0);
            activeLoop = to;
            laps++;
            if (Dev.RecycleEvery > 0 && laps % Dev.RecycleEvery == 0) ReplaceStandby(from);
            else { from.Pause(); from.Position = TimeSpan.Zero; } // 退回第一帧备用（ScrubbingEnabled 让它暂停时也解出这一帧）
        }

        // 每帧检查重叠交接（只在有 loop-x2.mp4 时）
        void CheckHandoff()
        {
            if (!overlapLoop || phase == Phase.Done || !introGone || swapFrom != null || activeLoop == null) return;
            if (handFrom != null && handFrom != activeLoop) { handFrom = null; handTo = null; } // 被重建 / 老办法接走了
            var cur = activeLoop;
            if (!cur.NaturalDuration.HasTimeSpan) return;
            double dur = cur.NaturalDuration.TimeSpan.TotalSeconds, half = dur / 2;
            double pos = cur.Position.TotalSeconds;

            if (handTo == null)
            {
                if (pos < 0.05 || pos < half - handLead) return;
                var next = OtherLoop(cur);
                next.Position = TimeSpan.Zero;
                next.Play();
                handFrom = cur; handTo = next; handT0 = DateTime.Now;
                return;
            }

            // 新的走过 0.8 秒（画面肯定已经稳了），旧的还在第二遍里，此时两边画面相同，换上去看不出来。
            // 新的一直没走起来就不换，旧的放到头会走「放完再接」的老路，那时新的已经在放了
            double toPos = handTo.Position.TotalSeconds;
            if (toPos < 0.8) return;
            double fromPos = handFrom.Position.TotalSeconds;
            double offset = (fromPos - half) - toPos;   // 正数 = 新的比旧的慢
            handLead = Math.Max(0, Math.Min(0.5, handLead + offset));
            var from = handFrom; var to = handTo;
            handFrom = null; handTo = null;
            Promote(from, to);
            handoffs++;
            if (handoffs <= 5 || handoffs % 200 == 0)
                Log.W("循环交接（第 " + handoffs + " 次）：错位 " + Math.Round(offset * 1000) + "ms，下次提前 " + Math.Round(handLead * 1000) + "ms");
        }

        /// 后台待命的那个播放器定期换成新的：同一个播放器暂停、倒回、再放成百上千次，不让它一直用下去。
        /// 它此刻垫在下面看不见，换掉不影响画面；新的自己会停在第一帧等着
        void ReplaceStandby(MediaElement old)
        {
            var fresh = MakeLoopPlayer();
            Panel.SetZIndex(fresh, 0);
            grid.Children.Add(fresh);
            if (old == loopA) loopA = fresh; else loopB = fresh;
            DisposePlayer(old);
        }

        // ── 自愈 ──

        /// 过一会儿再重建（系统刚唤醒、刚解锁时显卡可能还没缓过来）。连着来好几次只算最后一次
        void LaterRebuild(string why) { LaterRebuild(why, 2000); }
        void LaterRebuild(string why, int delayMs)
        {
            if (phase == Phase.Done) return;
            pendingWhy = why;
            pendingAt = clock.ElapsedMilliseconds + delayMs;
        }

        /// 把两个循环播放器都换成新的。旧的正在显示的那个先盖着，新的动起来再撤，不闪黑
        /// 不管是谁要求的重建（卡住、报错、解锁唤醒），都走这同一道门：连着修不好就放慢，
        /// 前三次隔 8 秒，之后隔 1 分钟（比如显示器关着、或者这台电脑根本解不了这个视频），返回 false 表示这次先不重建
        bool RebuildLoops(string why)
        {
            if (phase == Phase.Done || phase == Phase.Ending) return true;
            long now = clock.ElapsedMilliseconds;
            long gap = failStreak < 3 ? 8000 : 60000;
            if (lastRecoverMs >= 0 && now - lastRecoverMs < gap) return false;
            failStreak++;
            recoveries++; lastRecoverMs = now;
            double p = -1;
            try { if (activeLoop != null) p = activeLoop.Position.TotalSeconds; } catch { }
            Log.W("循环背景自动恢复（" + why + "）：第 " + recoveries + " 次，已循环 " + laps + " 圈，当时进度 " + p.ToString("0.00", CultureInfo.InvariantCulture) + " 秒");
            if (swapFrom != null && swapFrom == intro) FinishSwap(); // 开屏还盖着就收掉
            // 正在接缝的话，屏幕上看到的是还盖着的旧播放器，留它盖着；
            // 上一次重建的新播放器还没动起来、旧画面还盖着的话，屏幕上看到的是那个旧画面，继续让它盖着
            var old = retiring != null ? retiring : swapFrom != null ? swapFrom : activeLoop;
            swapFrom = null; swapTo = null; handFrom = null; handTo = null;
            if (retiring != null && retiring != old) DisposePlayer(retiring);
            retiring = null;
            if (loopA != old) DisposePlayer(loopA);
            if (loopB != old) DisposePlayer(loopB);
            if (old != null)
            {
                old.MediaEnded -= OnLoopEnded; old.MediaFailed -= OnLoopFailed;
                Panel.SetZIndex(old, 3);
                retiring = old; retireT0 = now;
            }
            loopA = MakeLoopPlayer();
            loopB = MakeLoopPlayer();
            Panel.SetZIndex(loopA, 1); Panel.SetZIndex(loopB, 0);
            grid.Children.Add(loopA); grid.Children.Add(loopB);
            activeLoop = loopA; // MakePlayer 里已经 Play 了；activeLoop 是它，打开后不会被停在第一帧
            verifying = true;
            lastMoveMs = now; lastPos = -1;
            return true;
        }

        void CheckRetire(long now)
        {
            if (retiring == null) return;
            // 新播放器稳稳走过 1 秒才撤旧画面，而且 0.25 秒淡出、不是一下拿掉。
            // 云端实测：新播放器进度刚过 0.3 秒时画面还会黑一两帧，那时撤掉旧画面就闪黑；以前 2.5 秒没动也照撤，撤完是一块空白
            if (activeLoop.Position.TotalSeconds < 1.0 && now - retireT0 < 20000) return;
            var r = retiring; retiring = null;
            var fade = new DoubleAnimation(0, TimeSpan.FromSeconds(0.25));
            fade.Completed += delegate { DisposePlayer(r); };
            r.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        /// 每 0.25 秒看一眼正在放的循环段进度有没有往前走
        void Watchdog(long now)
        {
            if (now < nextSampleMs) return;
            nextSampleMs = now + 250;
            bool timerGap = lastSampleMs > 0 && now - lastSampleMs > 2000; // 电脑睡眠、整个程序卡住时计时器没跑，那段不算画面卡住
            lastSampleMs = now;
            if (timerGap || phase != Phase.Revealed || !introGone || hidden || activeLoop == null)
            {
                lastMoveMs = now; lastPos = -1; // 藏着、还在开屏时不算；回来后重新计时
                return;
            }
            double p = activeLoop.Position.TotalSeconds;
            if (now >= nextBeatMs)
            {
                nextBeatMs = now + (long)(Math.Max(1, Dev.HeartbeatSec) * 1000);
                string mem = "";
                try { using (var pr = Process.GetCurrentProcess()) mem = "，内存 " + (pr.WorkingSet64 / 1048576) + "MB，句柄 " + pr.HandleCount; } catch { }
                Log.W("循环背景运行中：已循环 " + laps + " 圈，自动恢复 " + recoveries + " 次，进度 " + p.ToString("0.00", CultureInfo.InvariantCulture) + " 秒" + mem);
            }
            if (Math.Abs(p - lastPos) > 0.0005)
            {
                lastPos = p; lastMoveMs = now;
                if (verifying && p > 0.3) { verifying = false; failStreak = 0; Log.W("恢复成功：循环背景又动起来了"); }
                return;
            }
            long stuck = now - lastMoveMs;
            // 停在最后一帧却没收到「放完」通知：直接手动接到另一个播放器，跟正常接缝一样无缝
            if (stuck >= 400 && swapFrom == null && activeLoop.NaturalDuration.HasTimeSpan
                && p >= activeLoop.NaturalDuration.TimeSpan.TotalSeconds - 0.08)
            {
                missedEnds++;
                if (missedEnds <= 20 || missedEnds % 100 == 0) Log.W("循环段放到头没收到放完通知，手动接上（第 " + missedEnds + " 次）");
                SwapLoop(activeLoop, OtherLoop(activeLoop));
                lastMoveMs = now; lastPos = -1;
                return;
            }
            if (stuck < Dev.StallMs) return;
            RebuildLoops("画面 " + (stuck / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " 秒没动");
        }

        /// 开发用：提前换播放器（--loop-cut）、故意制造卡住（--stall-test）
        void DevHooks(long now)
        {
            if (phase != Phase.Revealed || !introGone || activeLoop == null) return;
            if (Dev.LoopCutSec > 0 && !overlapLoop && swapFrom == null && activeLoop.Position.TotalSeconds >= Dev.LoopCutSec)
                SwapLoop(activeLoop, OtherLoop(activeLoop));
            if (Dev.StallTestSec > 0 && now >= nextStallTestMs)
            {
                nextStallTestMs = now + (long)(Dev.StallTestSec * 1000);
                Log.W("测试：故意把正在放的循环播放器暂停，等自愈接回来");
                activeLoop.Pause();
            }
        }

        void IntroEnded()
        {
            if (introGone) return;
            introGone = true;
            Log.W("开屏放完，接循环段");
            loopA.Play();
            activeLoop = loopA;
            BeginSwap(intro, loopA); // 开屏停在最后一帧盖着，循环段动起来再撤
            if (phase == Phase.Intro) Reveal(false);
            CloseTop();
        }

        void CloseTop()
        {
            if (skipBtn != null) skipBtn.Visibility = Visibility.Collapsed;
        }

        // ── 每 15ms：跟随窗口、字幕、渐变 ──
        void Tick()
        {
            if (phase == Phase.Done) return;
            Follow(false);
            if (phase == Phase.Done) return;
            EnsureOverlayStyle(false);
            CheckSwap();
            CheckHandoff();
            long now = clock.ElapsedMilliseconds;
            CheckRetire(now);
            DevHooks(now);
            if (pendingAt > 0 && now >= pendingAt && !hidden && phase == Phase.Revealed && introGone)
            {
                pendingAt = RebuildLoops(pendingWhy) ? 0 : now + 1000; // 被退避挡住就过一秒再问
            }
            Watchdog(now);

            if (!introGone && intro != null)
            {
                double t = intro.Position.TotalSeconds;
                if (!introFadeInDone && phase == Phase.Intro && (t >= 0.04 || (DateTime.Now - introStartWait).TotalSeconds > 1.8))
                {
                    introFadeInDone = true;
                    RampAlpha(1, 0.28, null);
                    Log.W("开屏首帧已就绪（t=" + t.ToString("0.000", CultureInfo.InvariantCulture) + "s），平滑淡入");
                }
                if (phase == Phase.Intro && t >= config.FadeStartSeconds) Reveal(false);
                UpdateSubtitle(t);
            }
            if (alphaDur >= 0)
            {
                double k = alphaDur <= 0 ? 1 : Math.Min(1, (DateTime.Now - alphaT0).TotalSeconds / alphaDur);
                // 五次方 smootherstep 缓动：起步和落点加速度均为 0，淡出更丝滑自然
                double s = k * k * k * (k * (k * 6 - 15) + 10);
                SetAlpha(alphaFrom + (alphaTo - alphaFrom) * s);
                if (k >= 1) { alphaDur = -1; var then = alphaThen; alphaThen = null; if (then != null) then(); }
            }
            if (volDur >= 0 && intro != null)
            {
                double k = volDur <= 0 ? 1 : Math.Min(1, (DateTime.Now - volT0).TotalSeconds / volDur);
                double s = k * k * (3 - 2 * k);
                intro.Volume = volFrom + (volTo - volFrom) * s;
                if (k >= 1) volDur = -1;
            }
        }

        /// 跟着 Antigravity 窗口移动、缩放；Antigravity 最小化或藏到托盘时一起藏起来；Antigravity 关了就收工
        void Follow(bool force)
        {
            if (!Native.IsWindow(Target.Hwnd))
            {
                Log.W("Antigravity 窗口没了，收工");
                Teardown(); return;
            }
            bool vis = Native.IsWindowVisible(Target.Hwnd) && !Native.IsIconic(Target.Hwnd) && !Native.Cloaked(Target.Hwnd);
            if (!vis)
            {
                if (!hidden) { hidden = true; ShowAll(false); }
                return;
            }
            if (hidden)
            {
                hidden = false; ShowAll(true); force = true;
                // 循环背景阶段：先透明再 0.35 秒淡回去，盖住 Claude 还原动画和视频层刚恢复时的那一下
                if (phase == Phase.Revealed && alphaDur < 0) { SetAlpha(0); RampAlpha(config.ResidualAlpha, 0.35, null); }
            }
            Native.RECT r;
            if (!Native.Bounds(Target.Hwnd, out r)) return;
            bool needMove = force || r.Left != lastRect.Left || r.Top != lastRect.Top || r.Right != lastRect.Right || r.Bottom != lastRect.Bottom;
            if (!needMove && overlayH != IntPtr.Zero)
            {
                Native.RECT cur;
                if (Native.GetWindowRect(overlayH, out cur) && (cur.Left != r.Left || cur.Top != r.Top || cur.Right != r.Right || cur.Bottom != r.Bottom))
                    needMove = true;
            }
            if (!needMove) return;
            lastRect = r;
            uint f = Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER;
            if (overlayH != IntPtr.Zero) Native.SetWindowPos(overlayH, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, f);
            if (topH != IntPtr.Zero) Native.SetWindowPos(topH, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, f);
        }

        void ShowAll(bool show)
        {
            int cmd = show ? 4 /*SW_SHOWNOACTIVATE*/ : 0 /*SW_HIDE*/;
            if (overlayH != IntPtr.Zero) Native.ShowWindow(overlayH, cmd);
            if (topH != IntPtr.Zero) Native.ShowWindow(topH, cmd);
        }

        /// 分层（整窗半透明）+ 不出现在任务栏 + 不抢焦点；透出后再加点击穿透。被冲掉就补回来
        void EnsureOverlayStyle(bool force)
        {
            if (overlayH == IntPtr.Zero) return;
            long ex = Native.GetLong(overlayH, Native.GWL_EXSTYLE);
            long want = (ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE) & ~Native.WS_EX_APPWINDOW;
            if (clickThrough) want |= Native.WS_EX_TRANSPARENT;
            if (want != ex && styleFixes < 20)
            {
                if (!force) { styleFixes++; Log.W("浮层样式被改过，已补回 0x" + ex.ToString("X") + " → 0x" + want.ToString("X")); }
                Native.SetLong(overlayH, Native.GWL_EXSTYLE, want);
            }
        }

        void SetAlpha(double a)
        {
            alpha = Math.Max(0, Math.Min(1, a));
            grid.Opacity = alpha;
        }

        void RampAlpha(double to, double seconds, Action then)
        {
            alphaFrom = alpha; alphaTo = to; alphaT0 = DateTime.Now; alphaDur = Math.Max(0, seconds); alphaThen = then;
        }

        void RampVolume(double to, double seconds)
        {
            if (intro == null) return;
            volFrom = intro.Volume; volTo = to; volT0 = DateTime.Now; volDur = seconds;
        }

        void SetClickThrough(IntPtr h)
        {
            if (h == IntPtr.Zero) return;
            Native.SetLong(h, Native.GWL_EXSTYLE, Native.GetLong(h, Native.GWL_EXSTYLE) | Native.WS_EX_TRANSPARENT);
        }

        // 字幕：按播放时间算透明度，入场、出场各渐变 SubtitleFadeSeconds
        void UpdateSubtitle(double t)
        {
            if (sub == null || !config.ShowSubtitles) return;
            double fade = Math.Max(0.01, config.SubtitleFadeSeconds);
            int idx = -1;
            for (int i = 0; i < config.Subtitles.Count; i++)
                if (t >= config.Subtitles[i].Start && t <= config.Subtitles[i].End) { idx = i; break; }
            if (idx < 0)
            {
                if (currentSub >= 0 && sub.Opacity > 0) Log.W("字幕 " + (currentSub + 1) + " 消失，视频时间 " + t.ToString("0.000"));
                sub.Opacity = 0; currentSub = idx >= 0 ? idx : currentSub;
                if (currentSub == config.Subtitles.Count - 1 && phase != Phase.Intro) CloseTop(); // 最后一句放完，字幕层收掉
                return;
            }
            var s = config.Subtitles[idx];
            if (idx != currentSub) { currentSub = idx; sub.SetText(s.Text); Log.W("字幕 " + (idx + 1) + " 出现，视频时间 " + t.ToString("0.000")); }
            sub.Opacity = Math.Max(0, Math.Min(1, Math.Min((t - s.Start) / fade, (s.End - t) / fade)));
        }

        void HideSubtitles()
        {
            if (sub == null) return;
            sub.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.4)));
            sub = null;
        }

        /// 进入循环帧：透出 Antigravity 界面，点击穿透
        void Reveal(bool quick)
        {
            if (phase != Phase.Intro) return;
            phase = Phase.Revealed;
            clickThrough = true; EnsureOverlayStyle(true);
            SetClickThrough(topH);
            grid.Background = Brushes.Transparent;
            if (skipBtn != null)
            {
                var sbFade = new DoubleAnimation(0, TimeSpan.FromSeconds(0.35));
                var btn = skipBtn;
                sbFade.Completed += delegate { btn.Visibility = Visibility.Collapsed; };
                btn.BeginAnimation(UIElement.OpacityProperty, sbFade);
            }
            if (!config.LoopBackground)
            {
                Log.W("进入循环帧，循环背景已关，淡出");
                Finish(false); return;
            }
            double fadeSec = quick ? 0.9 : config.FadeDurationSeconds;
            Log.W("进入循环帧，五次方丝滑透出界面（" + fadeSec.ToString("0.0", CultureInfo.InvariantCulture) + "s）");
            RampAlpha(config.ResidualAlpha, fadeSec, null);
            RampVolume(0, fadeSec);
        }

        /// 点「跳过」：开着循环背景就淡出开屏、接到循环段，否则整个关掉
        public void SkipIntro()
        {
            if (phase != Phase.Intro) return;
            Log.W("点了跳过开屏");
            if (!config.LoopBackground) { Finish(true); return; }
            HideSubtitles();
            loopA.Play(); activeLoop = loopA;
            introGone = true;
            var fade = new DoubleAnimation(0, TimeSpan.FromSeconds(0.9)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            var iv = intro;
            fade.Completed += delegate { iv.Visibility = Visibility.Collapsed; iv.Stop(); iv.Close(); };
            intro.BeginAnimation(UIElement.OpacityProperty, fade);
            RampVolume(0, 0.9); // 开屏音效随画面一起淡出
            Reveal(true);
            var closeTop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            closeTop.Tick += delegate { closeTop.Stop(); CloseTop(); };
            closeTop.Start();
        }

        /// 菜单、设置窗口里拖滑块时实时生效
        public void Apply(Config c)
        {
            config = c;
            if (phase == Phase.Ending || phase == Phase.Done) return;
            if (phase == Phase.Revealed && alphaDur < 0) SetAlpha(c.ResidualAlpha);
            if (intro != null && !introGone) { volDur = -1; intro.Volume = c.IntroVolume; }
        }

        public void Finish(bool fast)
        {
            if (phase == Phase.Ending || phase == Phase.Done) return;
            phase = Phase.Ending;
            clickThrough = true; EnsureOverlayStyle(true);
            double d = fast ? 0.4 : 1.2;
            Log.W(fast ? "关闭（跳过）" : "淡出");
            if (skipBtn != null) skipBtn.Visibility = Visibility.Collapsed;
            HideSubtitles();
            RampVolume(0, d);
            RampAlpha(0, d, Teardown);
        }

        public void Teardown()
        {
            if (phase == Phase.Done) return;
            phase = Phase.Done;
            if (tick != null) tick.Stop();
            try { SystemEvents.SessionSwitch -= onSession; SystemEvents.DisplaySettingsChanged -= onDisplay; } catch { }
            if (powerNotify != IntPtr.Zero) { try { Native.UnregisterPowerSettingNotification(powerNotify); } catch { } powerNotify = IntPtr.Zero; }
            foreach (var m in new[] { intro, loopA, loopB, retiring })
                if (m != null) { try { m.Stop(); m.Close(); } catch { } }
            CloseTop();
            try { overlay.Close(); } catch { }
            Log.W("播放结束");
            if (onDone != null) onDone();
        }

        public bool Active { get { return phase != Phase.Done; } }

        /// 测试用：把 Antigravity 窗口那块屏幕截下来
        public void Snapshot(string path)
        {
            Native.RECT r = lastRect;
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return;
            using (var bmp = new SD.Bitmap(w, h))
            {
                using (var g = SD.Graphics.FromImage(bmp))
                {
                    IntPtr dst = g.GetHdc(), src = Native.GetDC(IntPtr.Zero);
                    Native.BitBlt(dst, 0, 0, w, h, src, r.Left, r.Top, 0x00CC0020 | 0x40000000); // SRCCOPY | CAPTUREBLT：分层窗口也拍进去
                    Native.ReleaseDC(IntPtr.Zero, src); g.ReleaseHdc(dst);
                }
                bmp.Save(path, SD.Imaging.ImageFormat.Png);
            }
            // 点击穿透检查：窗口中心这一点，鼠标点下去会落到哪个窗口
            IntPtr hit = Native.WindowFromPoint(new Native.POINT { X = r.Left + w / 2, Y = r.Top + h / 2 });
            IntPtr root = Native.GetAncestor(hit, 2 /*GA_ROOT*/);
            string who = root == overlayH ? "浮层" : root == Target.Hwnd ? "目标窗口" : root == topH ? "字幕层" : ("其他 " + Native.ClassOf(root));
            Log.W("截图 " + path + "  播放阶段=" + phase + " 透明度=" + alpha.ToString("0.00") + " 样式=0x" + Native.GetLong(overlayH, Native.GWL_EXSTYLE).ToString("X") + " 中心点击落到=" + who);
        }
    }

    // ───────────────────────── 设置窗口 ─────────────────────────

    public class SettingsWindow : Window
    {
        readonly App app;
        CheckBox introBox, loopBox;
        Slider alphaSlider, volSlider;
        TextBlock alphaVal, volVal;
        bool loading;

        public SettingsWindow(App app)
        {
            this.app = app;
            Title = "洛琪希开机动画 · 设置";
            Width = 420; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
            var sp = new StackPanel { Margin = new Thickness(22, 18, 22, 20) };

            sp.Children.Add(Header("打开 Antigravity 时"));
            introBox = new CheckBox { Content = "播放开屏动画", Margin = new Thickness(0, 6, 0, 4) };
            loopBox = new CheckBox { Content = "保留循环背景（半透明，点击穿透）", Margin = new Thickness(0, 4, 0, 4) };
            introBox.Click += delegate { Changed(); };
            loopBox.Click += delegate { Changed(); };
            sp.Children.Add(introBox); sp.Children.Add(loopBox);
            sp.Children.Add(new TextBlock { Text = "两个都不勾 = 原装 Antigravity", Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0, 0, 0, 10) });

            alphaSlider = MakeSlider(sp, "背景不透明度", 0.8, out alphaVal);
            volSlider = MakeSlider(sp, "开屏音量（再乘系统音量，系统静音就没声）", 1, out volVal);

            sp.Children.Add(Header("现在，在 Antigravity 窗口上"));
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            row.Children.Add(Btn("播放一次开屏动画", delegate { app.PreviewIntro(); }));
            row.Children.Add(Btn("开启循环背景", delegate { app.PreviewLoop(); }));
            row.Children.Add(Btn("关掉", delegate { app.StopShow(); }));
            sp.Children.Add(row);
            var row2 = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            row2.Children.Add(Btn("打开设置文件…", delegate { app.OpenConfig(); }));
            row2.Children.Add(Btn("卸载…", delegate { app.Uninstall(); }));
            sp.Children.Add(row2);
            Content = sp;
            Refresh();
        }

        static TextBlock Header(string t) { return new TextBlock { Text = t, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 6, 0, 0) }; }
        static Button Btn(string t, RoutedEventHandler h)
        {
            var b = new Button { Content = t, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 6) };
            b.Click += h; return b;
        }
        Slider MakeSlider(StackPanel sp, string title, double max, out TextBlock val)
        {
            var g = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var v = new TextBlock { Width = 44, TextAlignment = TextAlignment.Right };
            DockPanel.SetDock(v, Dock.Right);
            g.Children.Add(v);
            g.Children.Add(new TextBlock { Text = title });
            sp.Children.Add(g);
            var s = new Slider { Minimum = 0, Maximum = max, Margin = new Thickness(0, 4, 0, 8), IsMoveToPointEnabled = true };
            s.ValueChanged += delegate { v.Text = Math.Round(s.Value * 100) + "%"; if (!loading) Changed(); };
            sp.Children.Add(s);
            val = v;
            return s;
        }

        public void Refresh()
        {
            loading = true;
            var c = app.Config;
            introBox.IsChecked = c.PlayIntro; loopBox.IsChecked = c.LoopBackground;
            alphaSlider.Value = c.ResidualAlpha; volSlider.Value = c.IntroVolume;
            alphaVal.Text = Math.Round(c.ResidualAlpha * 100) + "%"; volVal.Text = Math.Round(c.IntroVolume * 100) + "%";
            loading = false;
        }

        void Changed()
        {
            if (loading) return;
            app.Update(delegate (Config c)
            {
                c.PlayIntro = introBox.IsChecked == true; c.LoopBackground = loopBox.IsChecked == true;
                c.ResidualAlpha = alphaSlider.Value; c.IntroVolume = volSlider.Value;
            }, this);
        }
    }

    // ───────────────────────── 主程序 ─────────────────────────

    public class App : Application
    {
        public static string AssetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
        public Config Config;
        Show show;
        WF.NotifyIcon tray;
        WF.ToolStripMenuItem introItem, loopItem, stopItem;
        WF.ToolStripLabel alphaLabel, volLabel;
        WF.TrackBar alphaBar, volBar;
        SettingsWindow settings;
        readonly HashSet<uint> seenPids = new HashSet<uint>();
        DispatcherTimer watch;
        readonly string[] args;
        bool testMode;
        bool menuLoading;

        public App(string[] args) { this.args = args; ShutdownMode = ShutdownMode.OnExplicitShutdown; }

        string Arg(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        bool Has(string name) { return Array.IndexOf(args, name) >= 0; }
        double ArgD(string name, double def)
        {
            double v; var s = Arg(name);
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Show.ForceLegacyLoop = Has("--legacy-loop");
            Config = Config.Load();
            LoadFont();
            Finder.TestTitle = Arg("--target-title");
            testMode = Finder.TestTitle != null;
            Dev.LoopCutSec = ArgD("--loop-cut", Dev.LoopCutSec);
            Dev.StallTestSec = ArgD("--stall-test", Dev.StallTestSec);
            Dev.RecycleEvery = (int)ArgD("--recycle-every", Dev.RecycleEvery);
            Dev.HeartbeatSec = ArgD("--heartbeat", Dev.HeartbeatSec);
            Log.W("启动 " + string.Join(" ", args) + "  程序目录 " + AppDomain.CurrentDomain.BaseDirectory);

            if (Arg("--render-subtitle") != null) { RenderSubtitleTest(); return; }

            SetupTray();
            if (Has("--settings")) OpenSettings();

            // 开机时 Antigravity 已经开着：不补播（除非显式带 --play-now）
            var t0 = Finder.Find();
            if (t0.Valid && !testMode) seenPids.Add(t0.Pid);
            if (Has("--play-now")) PreviewIntro();

            watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            watch.Tick += delegate { Watch(); };
            watch.Start();

            if (testMode) RunTest();
            else
            {
                var promote = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                promote.Tick += delegate { promote.Stop(); PromoteTrayIcon(); };
                promote.Start();
            }
        }

        void LoadFont()
        {
            try
            {
                if (File.Exists(Path.Combine(AssetsDir, "LibreBodoni-Italic.ttf")))
                    SubtitleElement.Font = new FontFamily(new Uri(AssetsDir.TrimEnd('\\') + "\\"), "./#Libre Bodoni");
            }
            catch (Exception ex) { Log.W("字体载入失败，用 Georgia：" + ex.Message); }
        }

        // 每 0.4 秒看一眼：Antigravity 有没有新开（新的进程号 = 新启动）
        void Watch()
        {
            var t = Finder.Find();
            if (!t.Valid || seenPids.Contains(t.Pid)) return;
            seenPids.Add(t.Pid);
            if (testMode) return;
            Config = Config.Load();
            Log.W("检测到 Antigravity 启动 pid=" + t.Pid + "，开屏=" + Config.PlayIntro + " 循环背景=" + Config.LoopBackground);
            if (!Config.PlayIntro && !Config.LoopBackground) return;
            Start(t, Config.PlayIntro);
        }

        void Start(Target t, bool withIntro)
        {
            if (show != null) show.Teardown();
            Show s = null;
            s = new Show(t, Config, withIntro, delegate { if (show == s) show = null; RefreshMenu(); });
            show = s;
            RefreshMenu();
        }

        bool StartOnCurrent(bool withIntro)
        {
            var t = Finder.Find();
            if (!t.Valid)
            {
                if (tray != null) tray.ShowBalloonTip(3000, "洛琪希开机动画", "没找到 Antigravity 窗口，先把 Antigravity 打开吧。", WF.ToolTipIcon.Info);
                return false;
            }
            seenPids.Add(t.Pid);
            Start(t, withIntro);
            return true;
        }

        public void PreviewIntro() { StartOnCurrent(true); }
        public void PreviewLoop() { StartOnCurrent(false); }
        public void StopShow() { if (show != null) show.Finish(true); }
        public void OpenConfig() { Config.Save(); try { Process.Start("notepad.exe", "\"" + Config.FilePath + "\""); } catch { } }

        public void Update(Action<Config> change, object from)
        {
            change(Config);
            Config.Save();
            if (show != null) show.Apply(Config);
            if (from != settings && settings != null) settings.Refresh();
            if (from != tray) RefreshMenu();
        }

        public void OpenSettings()
        {
            Config = Config.Load();
            if (settings == null)
            {
                settings = new SettingsWindow(this);
                settings.Closed += delegate { settings = null; };
            }
            settings.Refresh();
            settings.Show();
            if (settings.WindowState == WindowState.Minimized) settings.WindowState = WindowState.Normal;
            settings.Activate();
        }

        public void Uninstall()
        {
            var r = MessageBox.Show("会删除这个小程序、开机自启、设置和日志。Antigravity 本身没有被改过，卸载后就是原装的样子。",
                "卸载洛琪希开机动画？", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            if (show != null) show.Teardown();
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uninstall.ps1");
            if (File.Exists(script))
            {
                Process.Start(new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\" -Quiet")
                { UseShellExecute = false, CreateNoWindow = true });
            }
            Quit();
        }

        void Quit()
        {
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            Shutdown();
        }

        // ── 托盘图标和菜单 ──
        static SD.Icon MakeIcon()
        {
            var bmp = new SD.Bitmap(32, 32);
            using (var g = SD.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SD.Drawing2D.SmoothingMode.AntiAlias;
                // 一颗弧边四芒星（像 Gemini 标志），蓝→紫渐变，和 Claude 的金色 ✨、GPT 的月牙区分开
                using (var path = new SD.Drawing2D.GraphicsPath())
                {
                    float c = 16, r = 15, q = 1.5f;   // 中心、尖端半径、弧线往中心收的程度
                    path.StartFigure();
                    path.AddBezier(c, c - r, c + q, c - q, c + q, c - q, c + r, c);
                    path.AddBezier(c + r, c, c + q, c + q, c + q, c + q, c, c + r);
                    path.AddBezier(c, c + r, c - q, c + q, c - q, c + q, c - r, c);
                    path.AddBezier(c - r, c, c - q, c - q, c - q, c - q, c, c - r);
                    path.CloseFigure();
                    using (var b = new SD.Drawing2D.LinearGradientBrush(new SD.PointF(2, 30), new SD.PointF(30, 2),
                        SD.Color.FromArgb(255, 91, 115, 232), SD.Color.FromArgb(255, 118, 232, 255)))
                        g.FillPath(b, path);
                }
            }
            return SD.Icon.FromHandle(bmp.GetHicon());
        }

        void SetupTray()
        {
            tray = new WF.NotifyIcon { Icon = MakeIcon(), Text = "洛琪希开机动画", Visible = true };
            var m = new WF.ContextMenuStrip();
            m.Items.Add(new WF.ToolStripLabel("打开 Antigravity 时") { Enabled = false });
            introItem = new WF.ToolStripMenuItem("播放开屏动画");
            introItem.Click += delegate { Update(delegate (Config c) { c.PlayIntro = !c.PlayIntro; }, null); };
            loopItem = new WF.ToolStripMenuItem("保留循环背景");
            loopItem.Click += delegate { Update(delegate (Config c) { c.LoopBackground = !c.LoopBackground; }, null); };
            m.Items.Add(introItem); m.Items.Add(loopItem);
            m.Items.Add(new WF.ToolStripSeparator());

            alphaLabel = new WF.ToolStripLabel("背景不透明度");
            alphaBar = MakeBar(80);
            alphaBar.ValueChanged += delegate
            {
                alphaLabel.Text = "背景不透明度  " + alphaBar.Value + "%";
                if (!menuLoading) Update(delegate (Config c) { c.ResidualAlpha = alphaBar.Value / 100.0; }, tray);
            };
            volLabel = new WF.ToolStripLabel("开屏音量");
            volBar = MakeBar(100);
            volBar.ValueChanged += delegate
            {
                volLabel.Text = "开屏音量  " + volBar.Value + "%";
                if (!menuLoading) Update(delegate (Config c) { c.IntroVolume = volBar.Value / 100.0; }, tray);
            };
            m.Items.Add(alphaLabel); m.Items.Add(new WF.ToolStripControlHost(alphaBar));
            m.Items.Add(volLabel); m.Items.Add(new WF.ToolStripControlHost(volBar));
            m.Items.Add(new WF.ToolStripSeparator());

            m.Items.Add(new WF.ToolStripLabel("现在，在 Antigravity 窗口上") { Enabled = false });
            m.Items.Add("播放一次开屏动画", null, delegate { PreviewIntro(); });
            m.Items.Add("开启循环背景", null, delegate { PreviewLoop(); });
            stopItem = new WF.ToolStripMenuItem("关掉动画 / 背景");
            stopItem.Click += delegate { StopShow(); };
            m.Items.Add(stopItem);
            m.Items.Add(new WF.ToolStripSeparator());
            m.Items.Add("设置窗口…", null, delegate { OpenSettings(); });
            m.Items.Add("打开设置文件…", null, delegate { OpenConfig(); });
            m.Items.Add("卸载洛琪希开机动画…", null, delegate { Uninstall(); });
            m.Opening += delegate { Config = Config.Load(); RefreshMenu(); };
            tray.ContextMenuStrip = m;
            tray.MouseClick += delegate (object s, WF.MouseEventArgs ev) { if (ev.Button == WF.MouseButtons.Left) OpenSettings(); };
            RefreshMenu();
        }

        static WF.TrackBar MakeBar(int max)
        {
            return new WF.TrackBar { Minimum = 0, Maximum = max, TickStyle = WF.TickStyle.None, Width = 200, AutoSize = false, Height = 28, SmallChange = 1, LargeChange = 5 };
        }

        void RefreshMenu()
        {
            if (tray == null) return;
            menuLoading = true;
            introItem.Checked = Config.PlayIntro;
            loopItem.Checked = Config.LoopBackground;
            stopItem.Enabled = show != null;
            alphaBar.Value = Math.Max(0, Math.Min(80, (int)Math.Round(Config.ResidualAlpha * 100)));
            volBar.Value = Math.Max(0, Math.Min(100, (int)Math.Round(Config.IntroVolume * 100)));
            alphaLabel.Text = "背景不透明度  " + alphaBar.Value + "%";
            volLabel.Text = "开屏音量  " + volBar.Value + "%";
            menuLoading = false;
        }

        /// Windows 11 默认把新托盘图标收进「^」里。第一次运行时把自己的图标设为常显（用户以后手动藏起来就不再动它）
        void PromoteTrayIcon()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                using (var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings"))
                {
                    if (root == null) return;
                    foreach (var name in root.GetSubKeyNames())
                    {
                        using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings\" + name, true))
                        {
                            if (k == null) continue;
                            var path = k.GetValue("ExecutablePath") as string;
                            if (path == null || !string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;
                            if (k.GetValue("IsPromoted") == null) { k.SetValue("IsPromoted", 1, RegistryValueKind.DWord); Log.W("托盘图标设为常显"); }
                        }
                    }
                }
            }
            catch (Exception ex) { Log.W("托盘图标常显设置失败：" + ex.Message); }
        }

        // ── 测试 ──
        void RunTest()
        {
            string mode = Arg("--play") ?? "intro";
            double exitAfter = double.Parse(Arg("--exit-after") ?? "30", CultureInfo.InvariantCulture);
            string shotDir = Arg("--shot-dir");
            string shots = Arg("--shot-at");
            string skip = Arg("--test-skip");
            var startAt = DateTime.Now;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            bool started = false, skipped = false;
            var pending = new List<double>();
            if (shots != null) foreach (var s in shots.Split(',')) pending.Add(double.Parse(s, CultureInfo.InvariantCulture));
            DateTime showStart = DateTime.Now;
            timer.Tick += delegate
            {
                if (!started)
                {
                    if (StartOnCurrent(mode != "loop")) { started = true; showStart = DateTime.Now; }
                    else if ((DateTime.Now - startAt).TotalSeconds > 15) { Log.W("测试：找不到目标窗口"); Quit(); }
                    return;
                }
                double el = (DateTime.Now - showStart).TotalSeconds;
                if (skip != null && !skipped && el >= double.Parse(skip, CultureInfo.InvariantCulture) && show != null) { skipped = true; show.SkipIntro(); }
                for (int i = pending.Count - 1; i >= 0; i--)
                    if (el >= pending[i] && show != null && shotDir != null)
                    {
                        double at = pending[i]; pending.RemoveAt(i);
                        try
                        {
                            Directory.CreateDirectory(shotDir);
                            show.Snapshot(Path.Combine(shotDir, "shot_" + at.ToString("00.0", CultureInfo.InvariantCulture) + ".png"));
                        }
                        catch (Exception ex) { Log.W("截图失败：" + ex.Message); }
                    }
                if (el >= exitAfter) { timer.Stop(); if (show != null) show.Teardown(); Log.W("测试结束"); Quit(); }
            };
            timer.Start();
        }

        /// 离屏渲染字幕到图片（不需要显示器），测字体、大小、描边
        void RenderSubtitleTest()
        {
            try
            {
                string bg = Arg("--render-subtitle");
                string outPath = args[Array.IndexOf(args, "--render-subtitle") + 2];
                int idx = args.Length > Array.IndexOf(args, "--render-subtitle") + 3 ? int.Parse(args[Array.IndexOf(args, "--render-subtitle") + 3]) : 1;
                var img = new BitmapImage(new Uri(Path.GetFullPath(bg)));
                int W = img.PixelWidth, H = img.PixelHeight;
                var g = new Grid { Width = W, Height = H };
                g.Children.Add(new Image { Source = img, Stretch = Stretch.Fill });
                var s = new SubtitleElement { WidthRatio = Config.SubtitleWidthRatio, Opacity = 1 };
                s.RefText = Config.Subtitles[0].Text;
                s.SetText(Config.Subtitles[Math.Max(0, Math.Min(Config.Subtitles.Count - 1, idx - 1))].Text);
                g.Children.Add(s);
                g.Measure(new Size(W, H)); g.Arrange(new Rect(0, 0, W, H)); g.UpdateLayout();
                var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(g);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var f = File.Create(outPath)) enc.Save(f);
                Log.W("字幕渲染到 " + outPath + "  字体=" + SubtitleElement.Font.Source);
            }
            catch (Exception ex) { Log.W("字幕渲染失败：" + ex); }
            Shutdown();
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            Native.EnablePerMonitorDpi();
            GpuPref.Ensure();   // 要在 WPF 建立显卡渲染之前设好
            bool test = Array.IndexOf(args, "--target-title") >= 0 || Array.IndexOf(args, "--render-subtitle") >= 0;
            Mutex mutex = null;
            EventWaitHandle showEvent = null;
            if (!test)
            {
                bool created;
                mutex = new Mutex(true, @"Local\roxy-migurdia.antigravity-boot-animation", out created);
                showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\io.github.celestewynne0506-pixel.antigravity-boot-animation.show");
                if (!created)
                {
                    // 已经在运行：再打开一次 = 弹出设置窗口
                    showEvent.Set();
                    return 0;
                }
            }
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e) { Log.W("崩溃：" + e.ExceptionObject); };
            var app = new App(args);
            app.DispatcherUnhandledException += delegate (object s, DispatcherUnhandledExceptionEventArgs e) { Log.W("出错：" + e.Exception); e.Handled = true; };
            if (showEvent != null)
            {
                var ev = showEvent;
                var th = new Thread(delegate ()
                {
                    while (true) { ev.WaitOne(); app.Dispatcher.BeginInvoke(new Action(delegate { app.OpenSettings(); })); }
                }) { IsBackground = true };
                th.Start();
            }
            int code = app.Run();
            GC.KeepAlive(mutex);
            return code;
        }
    }
}
