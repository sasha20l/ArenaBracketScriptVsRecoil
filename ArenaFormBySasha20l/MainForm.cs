
// Полный код C# с добавленным функционалом рандома для смещения, задержки и времени удержания.
// Готов к использованию в Visual Studio, за исключением наличия SQLite DLL и файла базы данных.

using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

namespace AntiRecoilConfigurator
{
    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        const int MOUSEEVENTF_MOVE = 0x0001;
        const int VK_LBUTTON = 0x01;
        const int VK_RBUTTON = 0x02;

        private TextBox txtName;
        private ComboBox comboHotkey;
        private NumericUpDown numMoveY1, numDelay1, numTime1;
        private NumericUpDown numMoveY2, numDelay2, numTime2;

        private CheckBox chkRndMoveY1, chkRndDelay1, chkRndTime1;
        private CheckBox chkRndMoveY2, chkRndDelay2, chkRndTime2;

        private NumericUpDown numMoveY1Min, numMoveY1Max, numDelay1Min, numDelay1Max, numTime1Min, numTime1Max;
        private NumericUpDown numMoveY2Min, numMoveY2Max, numDelay2Min, numDelay2Max, numTime2Min, numTime2Max;

        private Button btnSave, btnDelete, btnUpdate;
        private ListBox listSettings;
        private SQLiteConnection db;
        private Random random = new Random();

        private bool isActive = false;
        private bool hotkeyWasDown = false;
        private int hotkeyVk = (int)Keys.F8;

        private Stopwatch timer1 = new Stopwatch();
        private Stopwatch timer2 = new Stopwatch();

        private double accY1 = 0, accY2 = 0;
        private int totalY1 = 0, totalY2 = 0;
        private bool lastLmbPressed = false, lastRmbPressed = false;
        private int lastUsedMode = 0;

        public MainForm()
        {
            Text = "Настройки антиотдачи by sasha20l";
            Width = 750;
            Height = 830;
            Font = new Font("Segoe UI", 10F);
            ShowIcon = false;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(240, 248, 255);
            BackgroundImageLayout = ImageLayout.Stretch;

            InitializeComponents();
            comboHotkey.SelectedIndexChanged += (s, e) =>
            {
                if (comboHotkey.SelectedItem is string key && key.StartsWith("F"))
                {
                    hotkeyVk = (int)Enum.Parse(typeof(Keys), key);
                }
            };
            InitializeDatabase();
            LoadSettings();
            Shown += (_, __) => StartHotkeyThread();
        }

        private void InitializeComponents()
        {
            Label lblName = new Label { Text = "Профиль:", Top = 10, Left = 10, Width = 150 };
            txtName = new TextBox { Top = 10, Left = 160, Width = 200 };

            Label lblHotkey = new Label { Text = "Горячая клавиша:", Top = 40, Left = 10, Width = 150 };
            comboHotkey = new ComboBox { Top = 40, Left = 160, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int i = 1; i <= 12; i++) comboHotkey.Items.Add("F" + i);
            comboHotkey.SelectedIndex = 7;

            GroupBox group = new GroupBox { Text = "Режимы", Top = 80, Left = 10, Width = 420, Height = 700 };

            int top = 30;
            AddModeControls(group, "Режим 1 (ЛКМ)", ref top, out numMoveY1, out numMoveY1Min, out numMoveY1Max, out chkRndMoveY1,
                            out numDelay1, out numDelay1Min, out numDelay1Max, out chkRndDelay1,
                            out numTime1, out numTime1Min, out numTime1Max, out chkRndTime1);

            top += 00;
            AddModeControls(group, "Режим 2 (ПКМ+ЛКМ)", ref top, out numMoveY2, out numMoveY2Min, out numMoveY2Max, out chkRndMoveY2,
                            out numDelay2, out numDelay2Min, out numDelay2Max, out chkRndDelay2,
                            out numTime2, out numTime2Min, out numTime2Max, out chkRndTime2);

            btnSave = new Button { Text = "Сохранить", Top = 520, Left = 450 };
            btnSave.Click += SaveSetting;

            btnUpdate = new Button { Text = "Обновить", Top = 520, Left = 540 };
            btnUpdate.Click += UpdateSetting;

            btnDelete = new Button { Text = "Удалить", Top = 520, Left = 630 };
            btnDelete.Click += DeleteSetting;

            listSettings = new ListBox { Top = 10, Left = 450, Width = 260, Height = 480 };
            listSettings.SelectedIndexChanged += LoadSelectedSetting;

            Controls.AddRange(new Control[] {
                lblName, txtName,
                lblHotkey, comboHotkey,
                group,
                btnSave, btnUpdate, btnDelete, listSettings
            });
        }

        private void AddModeControls(Control parent, string label, ref int top,
    out NumericUpDown main, out NumericUpDown min, out NumericUpDown max, out CheckBox chk,
    out NumericUpDown delay, out NumericUpDown delayMin, out NumericUpDown delayMax, out CheckBox chkDelay,
    out NumericUpDown time, out NumericUpDown timeMin, out NumericUpDown timeMax, out CheckBox chkTime)
        {
            // Основной заголовок режима
            Label lbl = new Label { Text = label, Top = top, Left = 10, Width = 400, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            parent.Controls.Add(lbl);

            top += 30;

            // Основные значения
            Label lblY = new Label { Text = "Смещение по Y (фиксированное):", Top = top, Left = 10, Width = 250 };
            main = new NumericUpDown { Top = top, Left = 270, Width = 80, DecimalPlaces = 2, Minimum = -1000, Maximum = 1000 };
            top += 30;

            Label lblDelay = new Label { Text = "Задержка между движениями (мс):", Top = top, Left = 10, Width = 250 };
            delay = new NumericUpDown { Top = top, Left = 270, Width = 80, Minimum = 1, Maximum = 1000 };
            top += 30;

            Label lblTime = new Label { Text = "Время удержания в мс (1000 мс = 1 сек):", Top = top, Left = 10, Width = 300 };
            time = new NumericUpDown { Top = top, Left = 310, Width = 80, Minimum = 1, Maximum = 5000 };
            top += 40;

            // Блок случайности
            GroupBox groupRandom = new GroupBox
            {
                Text = "Случайные значения (опционально)",
                Top = top,
                Left = 10,
                Width = 400,
                Height = 190
            };

            // Смещение Y
            chk = new CheckBox { Text = "Использовать случайное смещение Y", Top = 20, Left = 10, Width = 250 };
            Label lblYRange = new Label { Text = "Диапазон (от - до):", Top = 45, Left = 30 };
            min = new NumericUpDown { Top = 45, Left = 160, Width = 80, DecimalPlaces = 2, Minimum = -1000, Maximum = 1000 };
            max = new NumericUpDown { Top = 45, Left = 250, Width = 80, DecimalPlaces = 2, Minimum = -1000, Maximum = 1000 };

            // Задержка
            chkDelay = new CheckBox { Text = "Случайная задержка между смещениями", Top = 70, Left = 10, Width = 250 };
            Label lblDelayRange = new Label { Text = "Диапазон (мс):", Top = 95, Left = 30 };
            delayMin = new NumericUpDown { Top = 95, Left = 160, Width = 80, Minimum = 1, Maximum = 1000 };
            delayMax = new NumericUpDown { Top = 95, Left = 250, Width = 80, Minimum = 1, Maximum = 1000 };

            // Время удержания
            chkTime = new CheckBox { Text = "Случайное время удержания (мс)", Top = 120, Left = 10, Width = 250 };
            Label lblTimeRange = new Label { Text = "Диапазон (мс):", Top = 145, Left = 30 };
            timeMin = new NumericUpDown { Top = 145, Left = 160, Width = 80, Minimum = 1, Maximum = 5000 };
            timeMax = new NumericUpDown { Top = 145, Left = 250, Width = 80, Minimum = 1, Maximum = 5000 };

            // Добавляем всё в groupBox
            groupRandom.Controls.AddRange(new Control[]
            {
        chk, lblYRange, min, max,
        chkDelay, lblDelayRange, delayMin, delayMax,
        chkTime, lblTimeRange, timeMin, timeMax
            });

            // Добавляем основной блок
            parent.Controls.AddRange(new Control[]
            {
        lblY, main,
        lblDelay, delay,
        lblTime, time,
        groupRandom
            });

            top += groupRandom.Height + 20;
        }


        private int GetRandomOrFixed(NumericUpDown fixedValue, NumericUpDown min, NumericUpDown max, CheckBox chk)
        {
            return chk.Checked ? random.Next((int)min.Value, (int)max.Value + 1) : (int)fixedValue.Value;
        }

        private void InitializeDatabase()
        {
            db = new SQLiteConnection("Data Source=anti_recoil.db;Version=3;");
            db.Open();
            using var cmd = new SQLiteCommand(
                "CREATE TABLE IF NOT EXISTS settings (" +
                "name TEXT PRIMARY KEY, " +
                "moveY1 REAL, delay1 INT, time1 INT, " +
                "moveY2 REAL, delay2 INT, time2 INT, " +
                "hotkey TEXT)", db);
            cmd.ExecuteNonQuery();
        }

        private void SaveSetting(object sender, EventArgs e)
        {
            using var cmd = new SQLiteCommand("INSERT OR REPLACE INTO settings " +
                "(name, moveY1, delay1, time1, moveY2, delay2, time2, hotkey) " +
                "VALUES (@n, @y1, @d1, @t1, @y2, @d2, @t2, @hk)", db);
            cmd.Parameters.AddWithValue("@n", txtName.Text);
            cmd.Parameters.AddWithValue("@y1", numMoveY1.Value);
            cmd.Parameters.AddWithValue("@d1", numDelay1.Value);
            cmd.Parameters.AddWithValue("@t1", numTime1.Value);
            cmd.Parameters.AddWithValue("@y2", numMoveY2.Value);
            cmd.Parameters.AddWithValue("@d2", numDelay2.Value);
            cmd.Parameters.AddWithValue("@t2", numTime2.Value);
            cmd.Parameters.AddWithValue("@hk", comboHotkey.SelectedItem.ToString());
            cmd.ExecuteNonQuery();
            LoadSettings();
        }

        private void LoadSettings()
        {
            listSettings.Items.Clear();
            using var cmd = new SQLiteCommand("SELECT name FROM settings", db);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                listSettings.Items.Add(reader.GetString(0));
        }

        private void LoadSelectedSetting(object sender, EventArgs e)
        {
            if (listSettings.SelectedItem == null) return;
            string name = listSettings.SelectedItem.ToString();
            using var cmd = new SQLiteCommand("SELECT * FROM settings WHERE name = @n", db);
            cmd.Parameters.AddWithValue("@n", name);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                txtName.Text = reader.GetString(0);
                numMoveY1.Value = Convert.ToDecimal(reader.GetDouble(1));
                numDelay1.Value = reader.GetInt32(2);
                numTime1.Value = reader.GetInt32(3);
                numMoveY2.Value = Convert.ToDecimal(reader.GetDouble(4));
                numDelay2.Value = reader.GetInt32(5);
                numTime2.Value = reader.GetInt32(6);
                comboHotkey.SelectedItem = reader.GetString(7);
            }
        }

        private void DeleteSetting(object sender, EventArgs e)
        {
            if (listSettings.SelectedItem == null) return;
            string name = listSettings.SelectedItem.ToString();
            using var cmd = new SQLiteCommand("DELETE FROM settings WHERE name = @n", db);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.ExecuteNonQuery();
            LoadSettings();
        }

        private void UpdateSetting(object sender, EventArgs e)
        {
            SaveSetting(sender, e);
        }

        private void StartHotkeyThread()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1);
                    bool hotkeyDown = (GetAsyncKeyState(hotkeyVk) & 0x8000) != 0;
                    if (hotkeyDown && !hotkeyWasDown)
                        isActive = !isActive;
                    hotkeyWasDown = hotkeyDown;

                    bool lmb = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                    bool rmb = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

                    if (!isActive)
                    {
                        accY1 = accY2 = 0; totalY1 = totalY2 = 0;
                        timer1.Reset(); timer2.Reset();
                        continue;
                    }

                    if (rmb && lmb)
                    {
                        lastUsedMode = 2;
                        if (!timer2.IsRunning) timer2.Start();
                        if (timer2.ElapsedMilliseconds < GetRandomOrFixed(numTime2, numTime2Min, numTime2Max, chkRndTime2))
                        {
                            accY2 += GetRandomOrFixed(numMoveY2, numMoveY2Min, numMoveY2Max, chkRndMoveY2);
                            int dy = (int)accY2;
                            if (dy != 0)
                            {
                                mouse_event(MOUSEEVENTF_MOVE, 0, dy, 0, UIntPtr.Zero);
                                accY2 -= dy;
                                totalY2 += dy;
                            }
                            Thread.Sleep(GetRandomOrFixed(numDelay2, numDelay2Min, numDelay2Max, chkRndDelay2));
                        }
                    }
                    else if (lmb)
                    {
                        lastUsedMode = 1;
                        if (!timer1.IsRunning) timer1.Start();
                        if (timer1.ElapsedMilliseconds < GetRandomOrFixed(numTime1, numTime1Min, numTime1Max, chkRndTime1))
                        {
                            accY1 += GetRandomOrFixed(numMoveY1, numMoveY1Min, numMoveY1Max, chkRndMoveY1);
                            int dy = (int)accY1;
                            if (dy != 0)
                            {
                                mouse_event(MOUSEEVENTF_MOVE, 0, dy, 0, UIntPtr.Zero);
                                accY1 -= dy;
                                totalY1 += dy;
                            }
                            Thread.Sleep(GetRandomOrFixed(numDelay1, numDelay1Min, numDelay1Max, chkRndDelay1));
                        }
                    }

                    if (!lmb && lastLmbPressed)
                    {
                        if (lastUsedMode == 1 && totalY1 != 0)
                            mouse_event(MOUSEEVENTF_MOVE, 0, -totalY1, 0, UIntPtr.Zero);
                        else if (lastUsedMode == 2 && totalY2 != 0)
                            mouse_event(MOUSEEVENTF_MOVE, 0, -totalY2, 0, UIntPtr.Zero);
                        totalY1 = totalY2 = 0;
                        accY1 = accY2 = 0;
                        timer1.Reset(); timer2.Reset();
                    }

                    if (!rmb && lastRmbPressed && !lmb)
                        timer2.Reset();

                    lastLmbPressed = lmb;
                    lastRmbPressed = rmb;
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ShowWelcomeForm(); 

            Application.Run(new MainForm());
        }

        static void ShowWelcomeForm()
        {
            Form welcomeForm = new Form
            {
                Text = "Добро пожаловать",
                Size = new Size(420, 250),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label lblText = new Label
            {
                Text = "Это бесплатная программа для настройки антиотдачи.\n" +
                       "Вы можете использовать её без ограничений.\n\n" +
                       "Следите за новыми проектами или поддержите автора:",
                AutoSize = false,
                Size = new Size(380, 100),
                Location = new Point(10, 10)
            };

            LinkLabel link = new LinkLabel
            {
                Text = "https://github.com/sasha20l/ArenaBracketScriptVsRecoil",
                Location = new Point(10, 120),
                AutoSize = true
            };
            link.LinkClicked += (s, e) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = link.Text,
                    UseShellExecute = true
                });
            };

            Button btnOk = new Button
            {
                Text = "ОК",
                DialogResult = DialogResult.OK,
                Location = new Point(160, 170)
            };

            welcomeForm.Controls.AddRange(new Control[] { lblText, link, btnOk });
            welcomeForm.AcceptButton = btnOk;
            welcomeForm.ShowDialog();
        }
    }
}
