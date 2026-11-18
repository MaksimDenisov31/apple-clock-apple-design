using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace AppleClock
{
    // One looping player serves all active alerts. Acknowledging one must not silence another.
    internal sealed class AlarmNotifications : IDisposable
    {
        internal readonly List<AlarmPopup> Active = new List<AlarmPopup>();
        SoundPlayer player;
        Stream audio;
        readonly byte[] originalAudio;
        internal int VolumePercent { get; private set; }
        readonly bool silentTest;
        bool playing, disposed;
        internal bool SoundActive { get { return playing; } }
        internal AlarmNotifications(bool silentTest)
        {
            this.silentTest = silentTest;
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("AppleClock.TimerChime"))
            {
                if (resource == null) throw new InvalidDataException("Не найдена мелодия таймера.");
                using (var buffer = new MemoryStream()) { resource.CopyTo(buffer); originalAudio = buffer.ToArray(); }
            }
            VolumePercent = 100; audio = new MemoryStream(originalAudio, false);
            player = new SoundPlayer(audio); player.Load();
        }
        internal static byte[] ScalePcm(byte[] source, int volume)
        {
            if (volume < 0 || volume > 100) throw new ArgumentOutOfRangeException("volume");
            byte[] result = (byte[])source.Clone(); bool pcm16 = false;
            if (result.Length < 12 || System.Text.Encoding.ASCII.GetString(result, 0, 4) != "RIFF") throw new InvalidDataException("Ожидается WAV");
            for (int offset = 12; offset + 8 <= result.Length; )
            {
                int length = BitConverter.ToInt32(result, offset + 4); int start = offset + 8;
                if (length < 0 || length > result.Length - start) throw new InvalidDataException("Повреждён WAV");
                string kind = System.Text.Encoding.ASCII.GetString(result, offset, 4);
                if (kind == "fmt " && length >= 16) pcm16 = BitConverter.ToUInt16(result, start) == 1 && BitConverter.ToUInt16(result, start + 14) == 16;
                if (kind == "data")
                {
                    if (!pcm16 || length % 2 != 0) throw new InvalidDataException("Ожидается PCM 16 бит");
                    for (int i = start; i < start + length; i += 2) { short sample = (short)(BitConverter.ToInt16(result, i) * volume / 100); result[i] = (byte)(sample & 255); result[i + 1] = (byte)((sample >> 8) & 255); }
                    return result;
                }
                offset = start + length + (length & 1);
            }
            throw new InvalidDataException("Не найдены аудиоданные WAV");
        }
        internal void SetVolume(int percent)
        {
            if (disposed) throw new ObjectDisposedException("AlarmNotifications");
            if (percent == VolumePercent) return;
            var nextAudio = new MemoryStream(ScalePcm(originalAudio, percent), false);
            var nextPlayer = new SoundPlayer(nextAudio); nextPlayer.Load();
            player.Stop(); player.Dispose(); audio.Dispose(); player = nextPlayer; audio = nextAudio;
            VolumePercent = percent; playing = false; UpdateSound();
        }
        internal void SetSoundEnabled(bool enabled) { foreach (var popup in Active) popup.SoundEnabled = enabled; UpdateSound(); }
        internal AlarmPopup Show(string name, string message, bool sound, Brush background, Brush foreground, Brush secondary,
            ImageSource icon, Func<string, Action, bool, Button> button, Action stopped, Action snooze)
        {
            if (disposed) throw new ObjectDisposedException("AlarmNotifications");
            AlarmPopup popup = null;
            popup = new AlarmPopup(name, message, background, foreground, secondary, icon, button,
                delegate { Dismiss(popup); if (stopped != null) stopped(); },
                snooze == null ? (Action)null : delegate { Dismiss(popup); snooze(); });
            popup.SoundEnabled = sound; Active.Add(popup); UpdateSound(); popup.Show(); return popup;
        }
        void Dismiss(AlarmPopup popup)
        {
            Active.Remove(popup); popup.Dismiss(); UpdateSound();
        }
        void UpdateSound()
        {
            bool needed = VolumePercent > 0 && Active.Exists(p => p.SoundEnabled);
            if (needed == playing) return;
            if (needed) { if (!silentTest) player.PlayLooping(); } else player.Stop();
            playing = needed;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var popup in Active.ToArray()) popup.Dismiss();
            Active.Clear(); player.Stop(); playing = false; player.Dispose(); audio.Dispose();
        }
    }
    internal sealed class AlarmPopup : Window
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out RectNative rect);
        [StructLayout(LayoutKind.Sequential)] struct RectNative { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public RectNative Monitor, Work; public uint Flags; }
        readonly DispatcherTimer keepAbove = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        readonly IntPtr targetMonitor;
        bool acknowledged;
        internal bool SoundEnabled;
        internal AlarmPopup(string name, string message, Brush background, Brush foreground, Brush secondary, ImageSource icon,
            Func<string, Action, bool, Button> button, Action stopped, Action snooze)
        {
            Title = "Apple Clock · " + name; Width = 440; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = true; ShowActivated = true;
            Background = background; Foreground = foreground; Icon = icon; FontFamily = new FontFamily("Segoe UI");
            targetMonitor = MonitorFromWindow(GetForegroundWindow(), 2);
            var panel = new StackPanel { Margin = new Thickness(30) };
            panel.Children.Add(new TextBlock { Text = "СИГНАЛ · APPLE CLOCK", FontSize = 11, Foreground = secondary, Margin = new Thickness(0, 0, 0, 18) });
            panel.Children.Add(new TextBlock { Text = name, FontSize = 29, Foreground = foreground, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock { Text = message, FontSize = 17, Foreground = secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 8) });
            panel.Children.Add(new TextBlock { Text = "Сигнал активен до остановки", FontSize = 13, Foreground = secondary, Margin = new Thickness(0, 0, 0, 26) });
            var buttons = new WrapPanel(); var stop = button("Остановить", stopped, true); stop.MinWidth = 160;
            buttons.Children.Add(stop); if (snooze != null) buttons.Children.Add(button("Ещё 5 минут", snooze, false)); panel.Children.Add(buttons);
            Content = new Border { Child = panel, BorderBrush = new SolidColorBrush(Color.FromRgb(120,120,125)), BorderThickness = new Thickness(1) };
            SourceInitialized += delegate { var chrome = new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(0), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(12) }; System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome); };
            Loaded += delegate { PlaceOnActiveMonitor(); RaiseAbove(); Activate(); stop.Focus(); keepAbove.Start(); };
            keepAbove.Tick += delegate { RaiseAbove(); };
            StateChanged += delegate { if (!acknowledged && WindowState != WindowState.Normal) WindowState = WindowState.Normal; };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!acknowledged) { e.Cancel = true; RaiseAbove(); } };
            Closed += delegate { keepAbove.Stop(); };
        }
        void PlaceOnActiveMonitor()
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) }; RectNative window;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (GetMonitorInfo(targetMonitor, ref info) && GetWindowRect(handle, out window))
                SetWindowPos(handle, new IntPtr(-1), info.Work.Left + (info.Work.Right - info.Work.Left - window.Right + window.Left) / 2,
                    info.Work.Top + (info.Work.Bottom - info.Work.Top - window.Bottom + window.Top) / 2, 0, 0, 0x0001 | 0x0010);
        }
        internal void RaiseAbove()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero) SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
        }
        internal void Dismiss() { acknowledged = true; Close(); }
    }
}
