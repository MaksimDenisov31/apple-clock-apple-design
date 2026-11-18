using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace AppleClock
{
    public class Countdown
    {
        public string Name = "Таймер";
        public double Duration = 300, Remaining = 300;
        public bool Running;
        public DateTime DeadlineUtc;
        public void Start(DateTime utc) { if (Remaining <= 0) Remaining = Duration; DeadlineUtc = utc.AddSeconds(Remaining); Running = true; }
        public bool Tick(DateTime utc) { if (!Running) return false; Remaining = Math.Max(0, (DeadlineUtc - utc).TotalSeconds); if (Remaining > 0) return false; Running = false; return true; }
        public void Pause(DateTime utc) { Tick(utc); Running = false; }
        public void Reset() { Running = false; Remaining = Duration; }
    }
    public class Alarm
    {
        public string Name = "Будильник";
        public int Hour = 7, Minute = 30;
        public bool Enabled = true;
        // Monday is bit 0, Sunday bit 6. Zero means next occurrence only.
        public int Days;
        public DateTime NextUtc;
        public void Schedule(DateTime local)
        {
            for (int i = 0; i < 9; i++)
            {
                DateTime candidate = local.Date.AddDays(i).AddHours(Hour).AddMinutes(Minute);
                int bit = ((int)candidate.DayOfWeek + 6) % 7;
                if (candidate > local && (Days == 0 || (Days & (1 << bit)) != 0)) { NextUtc = candidate.ToUniversalTime(); return; }
            }
        }
        public bool Tick(DateTime utc)
        {
            if (!Enabled || NextUtc > utc) return false;
            // Do not ring stale alarms after a long shutdown; keep recurring schedules current.
            bool ring = utc - NextUtc < TimeSpan.FromMinutes(10);
            if (Days == 0) Enabled = false; else Schedule(utc.ToLocalTime());
            return ring;
        }
    }
    public class City
    {
        public string Name, Zone;
        public City() { }
        public City(string name, string zone) { Name = name; Zone = zone; }
        public DateTime At(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(Zone)); }
        public static City[] Catalog = {
            new City("Москва", "Russian Standard Time"), new City("Лондон", "GMT Standard Time"),
            new City("Нью-Йорк", "Eastern Standard Time"), new City("Токио", "Tokyo Standard Time"),
            new City("Париж", "Romance Standard Time"), new City("Дубай", "Arabian Standard Time"),
            new City("Лос-Анджелес", "Pacific Standard Time"), new City("Сидней", "AUS Eastern Standard Time"),
            new City("Пекин", "China Standard Time"), new City("Берлин", "W. Europe Standard Time"),
            new City("Екатеринбург", "Ekaterinburg Standard Time"), new City("Новосибирск", "N. Central Asia Standard Time"),
            new City("Владивосток", "Vladivostok Standard Time"), new City("Калининград", "Kaliningrad Standard Time"),
            new City("Стамбул", "Turkey Standard Time"), new City("Дели", "India Standard Time") };
    }
    public class State
    {
        public bool Dark, Sound = true;
        public int VolumePercent = 100, TimerStepSeconds = 10;
        public double WindowWidth = 1010, WindowHeight = 790;
        public bool WindowMaximized;
        public int FocusMinutes = 25, BreakMinutes = 5, FocusCompleted;
        public string FocusDay = "", FocusTask = "";
        public bool OnBreak;
        public Countdown Focus = new Countdown { Name = "Фокусировка", Duration = 1500, Remaining = 1500 };
        public List<Countdown> Timers = new List<Countdown>();
        public List<Alarm> Alarms = new List<Alarm>();
        public List<City> Cities = new List<City>();
        public static State Fresh() { State s = new State(); s.Timers.Add(new Countdown { Name = "Чай", Duration = 180, Remaining = 180 }); s.Timers.Add(new Countdown { Name = "Обратный отсчёт", Duration = 300, Remaining = 300 }); s.Cities.Add(City.Catalog[0]); s.Cities.Add(City.Catalog[1]); s.Cities.Add(City.Catalog[3]); return s; }
    }
    public static class Storage
    {
        public static string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppleClock");
        public static string LastError = "";
        public static State Load()
        {
            string path = Path.Combine(DirectoryPath, "settings.xml");
            if (!File.Exists(path)) return State.Fresh();
            try { using (var input = File.OpenRead(path)) { State s = (State)new XmlSerializer(typeof(State)).Deserialize(input); Validate(s); return s; } }
            catch (Exception ex) { LastError = "Не удалось прочитать настройки: " + ex.Message; return State.Fresh(); }
        }
        static void Validate(State s)
        {
            if (double.IsNaN(s.WindowWidth) || double.IsInfinity(s.WindowWidth) || s.WindowWidth < 880 || s.WindowWidth > 10000) s.WindowWidth = 1010;
            if (double.IsNaN(s.WindowHeight) || double.IsInfinity(s.WindowHeight) || s.WindowHeight < 640 || s.WindowHeight > 10000) s.WindowHeight = 790;
            if (s.VolumePercent < 0 || s.VolumePercent > 100 || s.TimerStepSeconds < 1 || s.TimerStepSeconds > 3600) throw new InvalidDataException("Некорректная громкость или шаг времени");
            if (s.Timers == null || s.Alarms == null || s.Cities == null || s.Focus == null || s.FocusMinutes < 1 || s.FocusMinutes > 180 || s.BreakMinutes < 1 || s.BreakMinutes > 60) throw new InvalidDataException("Некорректные настройки");
            foreach (Countdown t in s.Timers) if (t.Duration < 1 || t.Duration > 359999 || double.IsNaN(t.Duration) || double.IsNaN(t.Remaining) || t.Remaining < 0 || t.Remaining > t.Duration) throw new InvalidDataException("Некорректный таймер");
            foreach (Alarm a in s.Alarms) if (a.Hour < 0 || a.Hour > 23 || a.Minute < 0 || a.Minute > 59 || a.Days < 0 || a.Days > 127) throw new InvalidDataException("Некорректный будильник");
            foreach (City c in s.Cities) TimeZoneInfo.FindSystemTimeZoneById(c.Zone);
        }
        public static bool Save(State s)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath); string path = Path.Combine(DirectoryPath, "settings.xml"), temp = path + ".tmp";
                using (var output = File.Create(temp)) new XmlSerializer(typeof(State)).Serialize(output, s);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
                return true;
            }
            catch (Exception ex) { LastError = "Не удалось сохранить настройки: " + ex.Message; return false; }
        }
    }
}
