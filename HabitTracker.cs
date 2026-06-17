// HabitTracker.cs - Трекер привычек на C# (CLI + WinForms)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace HabitTracker
{
    public class Habit
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Frequency { get; set; }
        public string Created { get; set; }
        public List<string> History { get; set; } = new List<string>();
    }

    public class Tracker
    {
        public List<Habit> Habits { get; set; } = new List<Habit>();
        public int NextId { get; set; } = 1;
        private const string DataFile = "habits.json";

        public void Load()
        {
            if (File.Exists(DataFile))
            {
                try
                {
                    string json = File.ReadAllText(DataFile);
                    var data = JsonSerializer.Deserialize<Tracker>(json);
                    if (data != null)
                    {
                        Habits = data.Habits;
                        NextId = data.NextId;
                        return;
                    }
                }
                catch { }
            }
            Habits = new List<Habit>();
            NextId = 1;
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataFile, json);
        }

        public Habit AddHabit(string name, string description, string frequency)
        {
            if (string.IsNullOrEmpty(frequency)) frequency = "daily";
            var h = new Habit
            {
                Id = NextId++,
                Name = name,
                Description = description,
                Frequency = frequency,
                Created = DateTime.Now.ToString("yyyy-MM-dd"),
                History = new List<string>()
            };
            Habits.Add(h);
            Save();
            return h;
        }

        public bool EditHabit(int id, string name, string description, string frequency)
        {
            var h = Habits.FirstOrDefault(x => x.Id == id);
            if (h == null) return false;
            if (name != null) h.Name = name;
            if (description != null) h.Description = description;
            if (frequency != null) h.Frequency = frequency;
            Save();
            return true;
        }

        public bool DeleteHabit(int id)
        {
            int removed = Habits.RemoveAll(x => x.Id == id);
            if (removed > 0) { Save(); return true; }
            return false;
        }

        public bool CheckHabit(int id, string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            var h = Habits.FirstOrDefault(x => x.Id == id);
            if (h == null) return false;
            if (h.History.Contains(dateStr)) return false;
            h.History.Add(dateStr);
            Save();
            return true;
        }

        public bool UncheckHabit(int id, string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            var h = Habits.FirstOrDefault(x => x.Id == id);
            if (h == null) return false;
            if (!h.History.Contains(dateStr)) return false;
            h.History.Remove(dateStr);
            Save();
            return true;
        }

        public (int total, int weekCount, double weekPercent, int streak, string lastCheck) GetHabitStats(Habit h)
        {
            int total = h.History.Count;
            DateTime today = DateTime.Today;
            DateTime weekAgo = today.AddDays(-7);
            string weekAgoStr = weekAgo.ToString("yyyy-MM-dd");
            int weekCount = h.History.Count(d => string.Compare(d, weekAgoStr) >= 0);
            int expected = h.Frequency == "daily" ? 7 : h.Frequency == "weekly" ? 1 : 1;
            double weekPercent = expected > 0 ? (double)weekCount / expected * 100 : 0;
            int streak = 0;
            DateTime check = today;
            while (true)
            {
                string ds = check.ToString("yyyy-MM-dd");
                if (h.History.Contains(ds))
                {
                    streak++;
                    check = check.AddDays(-1);
                }
                else break;
            }
            string lastCheck = h.History.Count > 0 ? h.History.Last() : null;
            return (total, weekCount, weekPercent, streak, lastCheck);
        }
    }

    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--gui")
            {
                Application.EnableVisualStyles();
                Application.Run(new HabitTrackerGUI());
                return;
            }
            // CLI
            var tracker = new Tracker();
            tracker.Load();
            if (args.Length == 0)
            {
                InteractiveMode(tracker);
                return;
            }
            try
            {
                string cmd = args[0];
                switch (cmd)
                {
                    case "add":
                        string name = null, desc = "", freq = "daily";
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--name") name = args[++i];
                            else if (args[i] == "--description") desc = args[++i];
                            else if (args[i] == "--frequency") freq = args[++i];
                        }
                        if (name == null) { Console.WriteLine("Укажите --name"); return; }
                        var h = tracker.AddHabit(name, desc, freq);
                        Console.WriteLine($"✅ Привычка #{h.Id} '{h.Name}' добавлена");
                        break;
                    case "list":
                        var habits = tracker.Habits;
                        if (!habits.Any()) { Console.WriteLine("Нет привычек."); return; }
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        Console.WriteLine($"{"ID",-4} {"Название",-20} {"Частота",-10} {"Сегодня"}");
                        foreach (var hab in habits)
                        {
                            string done = hab.History.Contains(today) ? "✅" : "❌";
                            Console.WriteLine($"{hab.Id,-4} {hab.Name,-20} {hab.Frequency,-10} {done}");
                        }
                        break;
                    case "check":
                        int id = 0; string date = null;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--id") id = int.Parse(args[++i]);
                            else if (args[i] == "--date") date = args[++i];
                        }
                        if (id == 0) { Console.WriteLine("Укажите --id"); return; }
                        if (tracker.CheckHabit(id, date))
                            Console.WriteLine($"✅ Привычка #{id} отмечена выполнена");
                        else
                            Console.WriteLine($"❌ Не удалось отметить");
                        break;
                    case "uncheck":
                        id = 0; date = null;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--id") id = int.Parse(args[++i]);
                            else if (args[i] == "--date") date = args[++i];
                        }
                        if (id == 0) { Console.WriteLine("Укажите --id"); return; }
                        if (tracker.UncheckHabit(id, date))
                            Console.WriteLine($"✅ Отметка снята для #{id}");
                        else
                            Console.WriteLine($"❌ Не удалось снять отметку");
                        break;
                    case "stats":
                        id = 0;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--id") id = int.Parse(args[++i]);
                        }
                        if (id == 0) { Console.WriteLine("Укажите --id"); return; }
                        var habit = tracker.Habits.FirstOrDefault(x => x.Id == id);
                        if (habit == null) { Console.WriteLine("Привычка не найдена"); return; }
                        var stats = tracker.GetHabitStats(habit);
                        Console.WriteLine($"📊 Статистика для '{habit.Name}':");
                        Console.WriteLine($"  Всего выполнений: {stats.total}");
                        Console.WriteLine($"  За последние 7 дней: {stats.weekCount} ({stats.weekPercent:F1}%)");
                        Console.WriteLine($"  Текущая серия (streak): {stats.streak} дней");
                        Console.WriteLine($"  Последнее выполнение: {stats.lastCheck ?? "никогда"}");
                        break;
                    case "edit":
                        id = 0; string newName = null, newDesc = null, newFreq = null;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--id") id = int.Parse(args[++i]);
                            else if (args[i] == "--name") newName = args[++i];
                            else if (args[i] == "--description") newDesc = args[++i];
                            else if (args[i] == "--frequency") newFreq = args[++i];
                        }
                        if (id == 0) { Console.WriteLine("Укажите --id"); return; }
                        if (tracker.EditHabit(id, newName, newDesc, newFreq))
                            Console.WriteLine($"✅ Привычка #{id} обновлена");
                        else
                            Console.WriteLine($"❌ Привычка не найдена");
                        break;
                    case "delete":
                        id = 0;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (args[i] == "--id") id = int.Parse(args[++i]);
                        }
                        if (id == 0) { Console.WriteLine("Укажите --id"); return; }
                        if (tracker.DeleteHabit(id))
                            Console.WriteLine($"✅ Привычка #{id} удалена");
                        else
                            Console.WriteLine($"❌ Привычка не найдена");
                        break;
                    default:
                        InteractiveMode(tracker);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        static void InteractiveMode(Tracker tracker)
        {
            while (true)
            {
                Console.WriteLine("\n🌱 Трекер привычек (интерактивный)");
                Console.WriteLine("1. Добавить привычку");
                Console.WriteLine("2. Список привычек");
                Console.WriteLine("3. Отметить выполнение");
                Console.WriteLine("4. Отменить выполнение");
                Console.WriteLine("5. Статистика");
                Console.WriteLine("6. Редактировать");
                Console.WriteLine("7. Удалить");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");
                string choice = Console.ReadLine();
                switch (choice)
                {
                    case "0": return;
                    case "1":
                        Console.Write("Название: ");
                        string name = Console.ReadLine();
                        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Название обязательно"); break; }
                        Console.Write("Описание (необязательно): ");
                        string desc = Console.ReadLine();
                        Console.Write("Частота (daily/weekly/monthly, по умолчанию daily): ");
                        string freq = Console.ReadLine();
                        if (string.IsNullOrEmpty(freq)) freq = "daily";
                        var h = tracker.AddHabit(name, desc, freq);
                        Console.WriteLine($"✅ Привычка #{h.Id} '{h.Name}' добавлена");
                        break;
                    case "2":
                        var habits = tracker.Habits;
                        if (!habits.Any()) { Console.WriteLine("Нет привычек."); break; }
                        string today = DateTime.Now.ToString("yyyy-MM-dd");
                        Console.WriteLine($"{"ID",-4} {"Название",-20} {"Частота",-10} {"Сегодня"}");
                        foreach (var hab in habits)
                        {
                            string done = hab.History.Contains(today) ? "✅" : "❌";
                            Console.WriteLine($"{hab.Id,-4} {hab.Name,-20} {hab.Frequency,-10} {done}");
                        }
                        break;
                    case "3":
                        Console.Write("ID привычки: ");
                        if (!int.TryParse(Console.ReadLine(), out int id)) { Console.WriteLine("Неверный ID"); break; }
                        if (tracker.CheckHabit(id, null))
                            Console.WriteLine($"✅ Привычка #{id} отмечена выполнена");
                        else
                            Console.WriteLine($"❌ Не удалось отметить");
                        break;
                    case "4":
                        Console.Write("ID привычки: ");
                        if (!int.TryParse(Console.ReadLine(), out id)) { Console.WriteLine("Неверный ID"); break; }
                        if (tracker.UncheckHabit(id, null))
                            Console.WriteLine($"✅ Отметка снята для #{id}");
                        else
                            Console.WriteLine($"❌ Не удалось снять отметку");
                        break;
                    case "5":
                        Console.Write("ID привычки: ");
                        if (!int.TryParse(Console.ReadLine(), out id)) { Console.WriteLine("Неверный ID"); break; }
                        var habit = tracker.Habits.FirstOrDefault(x => x.Id == id);
                        if (habit == null) { Console.WriteLine("Привычка не найдена"); break; }
                        var stats = tracker.GetHabitStats(habit);
                        Console.WriteLine($"📊 Статистика для '{habit.Name}':");
                        Console.WriteLine($"  Всего выполнений: {stats.total}");
                        Console.WriteLine($"  За последние 7 дней: {stats.weekCount} ({stats.weekPercent:F1}%)");
                        Console.WriteLine($"  Текущая серия (streak): {stats.streak} дней");
                        Console.WriteLine($"  Последнее выполнение: {stats.lastCheck ?? "никогда"}");
                        break;
                    case "6":
                        Console.Write("ID привычки: ");
                        if (!int.TryParse(Console.ReadLine(), out id)) { Console.WriteLine("Неверный ID"); break; }
                        habit = tracker.Habits.FirstOrDefault(x => x.Id == id);
                        if (habit == null) { Console.WriteLine("Привычка не найдена"); break; }
                        Console.WriteLine("Оставьте пустым, чтобы не менять.");
                        Console.Write($"Название ({habit.Name}): ");
                        string newName = Console.ReadLine();
                        if (string.IsNullOrEmpty(newName)) newName = null;
                        Console.Write($"Описание ({habit.Description}): ");
                        string newDesc = Console.ReadLine();
                        if (string.IsNullOrEmpty(newDesc)) newDesc = null;
                        Console.Write($"Частота ({habit.Frequency}): ");
                        string newFreq = Console.ReadLine();
                        if (string.IsNullOrEmpty(newFreq)) newFreq = null;
                        if (tracker.EditHabit(id, newName, newDesc, newFreq))
                            Console.WriteLine("✅ Обновлено");
                        else
                            Console.WriteLine("❌ Ошибка");
                        break;
                    case "7":
                        Console.Write("ID для удаления: ");
                        if (!int.TryParse(Console.ReadLine(), out id)) { Console.WriteLine("Неверный ID"); break; }
                        if (tracker.DeleteHabit(id))
                            Console.WriteLine("✅ Удалено");
                        else
                            Console.WriteLine("❌ Не найдено");
                        break;
                    default:
                        Console.WriteLine("Неверный выбор");
                        break;
                }
            }
        }
    }

    // ========== GUI ==========
    public class HabitTrackerGUI : Form
    {
        private Tracker tracker = new Tracker();
        private DataGridView grid;
        private TextBox nameBox, descBox;
        private ComboBox freqBox;

        public HabitTrackerGUI()
        {
            tracker.Load();
            Text = "🌱 Трекер привычек";
            Size = new System.Drawing.Size(700, 500);
            StartPosition = FormStartPosition.CenterScreen;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Padding = new Padding(5) };
            top.Controls.Add(new Label { Text = "Название:", AutoSize = true });
            nameBox = new TextBox { Width = 120 };
            top.Controls.Add(nameBox);
            top.Controls.Add(new Label { Text = "Описание:", AutoSize = true });
            descBox = new TextBox { Width = 150 };
            top.Controls.Add(descBox);
            top.Controls.Add(new Label { Text = "Частота:", AutoSize = true });
            freqBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Items = { "daily", "weekly", "monthly" }, SelectedIndex = 0 };
            top.Controls.Add(freqBox);
            var addBtn = new Button { Text = "Добавить" };
            addBtn.Click += (s, e) => AddHabit();
            top.Controls.Add(addBtn);
            Controls.Add(top);

            grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            grid.Columns.Add("Id", "ID");
            grid.Columns.Add("Name", "Название");
            grid.Columns.Add("Freq", "Частота");
            grid.Columns.Add("Done", "Сегодня");
            grid.Columns.Add("Streak", "Streak");
            Controls.Add(grid);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Padding = new Padding(5) };
            var checkBtn = new Button { Text = "✅ Отметить выполнение" };
            checkBtn.Click += (s, e) => CheckHabit();
            bottom.Controls.Add(checkBtn);
            var uncheckBtn = new Button { Text = "❌ Отменить" };
            uncheckBtn.Click += (s, e) => UncheckHabit();
            bottom.Controls.Add(uncheckBtn);
            var statsBtn = new Button { Text = "📊 Статистика" };
            statsBtn.Click += (s, e) => ShowStats();
            bottom.Controls.Add(statsBtn);
            var editBtn = new Button { Text = "✏️ Редактировать" };
            editBtn.Click += (s, e) => EditHabit();
            bottom.Controls.Add(editBtn);
            var deleteBtn = new Button { Text = "🗑 Удалить" };
            deleteBtn.Click += (s, e) => DeleteHabit();
            bottom.Controls.Add(deleteBtn);
            Controls.Add(bottom);

            RefreshGrid();
        }

        private void RefreshGrid()
        {
            grid.Rows.Clear();
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            foreach (var h in tracker.Habits)
            {
                string done = h.History.Contains(today) ? "✅" : "❌";
                var stats = tracker.GetHabitStats(h);
                grid.Rows.Add(h.Id, h.Name, h.Frequency, done, stats.streak);
            }
        }

        private void AddHabit()
        {
            string name = nameBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) { MessageBox.Show("Введите название"); return; }
            string desc = descBox.Text.Trim();
            string freq = freqBox.SelectedItem.ToString();
            tracker.AddHabit(name, desc, freq);
            nameBox.Text = "";
            descBox.Text = "";
            RefreshGrid();
        }

        private int? GetSelectedId()
        {
            if (grid.SelectedRows.Count == 0) { MessageBox.Show("Выберите привычку"); return null; }
            return (int)grid.SelectedRows[0].Cells[0].Value;
        }

        private void CheckHabit()
        {
            var id = GetSelectedId();
            if (id.HasValue && tracker.CheckHabit(id.Value, null))
                RefreshGrid();
            else
                MessageBox.Show("Не удалось отметить");
        }

        private void UncheckHabit()
        {
            var id = GetSelectedId();
            if (id.HasValue && tracker.UncheckHabit(id.Value, null))
                RefreshGrid();
            else
                MessageBox.Show("Не удалось снять отметку");
        }

        private void ShowStats()
        {
            var id = GetSelectedId();
            if (!id.HasValue) return;
            var h = tracker.Habits.FirstOrDefault(x => x.Id == id);
            if (h == null) return;
            var stats = tracker.GetHabitStats(h);
            string msg = $"📊 Статистика для '{h.Name}':\n\n";
            msg += $"Всего выполнений: {stats.total}\n";
            msg += $"За последние 7 дней: {stats.weekCount} ({stats.weekPercent:F1}%)\n";
            msg += $"Текущая серия (streak): {stats.streak} дней\n";
            msg += $"Последнее выполнение: {stats.lastCheck ?? "никогда"}";
            MessageBox.Show(msg);
        }

        private void EditHabit()
        {
            var id = GetSelectedId();
            if (!id.HasValue) return;
            var h = tracker.Habits.FirstOrDefault(x => x.Id == id);
            if (h == null) return;
            var dialog = new Form { Text = "Редактировать", Size = new System.Drawing.Size(400, 200), StartPosition = FormStartPosition.CenterParent };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "Название:", AutoSize = true }, 0, 0);
            var nameEdit = new TextBox { Text = h.Name };
            layout.Controls.Add(nameEdit, 1, 0);
            layout.Controls.Add(new Label { Text = "Описание:", AutoSize = true }, 0, 1);
            var descEdit = new TextBox { Text = h.Description };
            layout.Controls.Add(descEdit, 1, 1);
            layout.Controls.Add(new Label { Text = "Частота:", AutoSize = true }, 0, 2);
            var freqEdit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Items = { "daily", "weekly", "monthly" }, SelectedItem = h.Frequency };
            layout.Controls.Add(freqEdit, 1, 2);
            var saveBtn = new Button { Text = "Сохранить", Dock = DockStyle.Right };
            saveBtn.Click += (s, e) =>
            {
                string name = nameEdit.Text.Trim();
                if (string.IsNullOrEmpty(name)) { MessageBox.Show("Название обязательно"); return; }
                string desc = descEdit.Text.Trim();
                string freq = freqEdit.SelectedItem.ToString();
                if (tracker.EditHabit(id.Value, name, desc, freq))
                {
                    RefreshGrid();
                    dialog.Close();
                }
                else MessageBox.Show("Ошибка");
            };
            layout.Controls.Add(saveBtn, 1, 3);
            dialog.Controls.Add(layout);
            dialog.ShowDialog();
        }

        private void DeleteHabit()
        {
            var id = GetSelectedId();
            if (!id.HasValue) return;
            if (MessageBox.Show("Удалить привычку?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (tracker.DeleteHabit(id.Value)) RefreshGrid();
            }
        }
    }
}
