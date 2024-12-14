using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Globalization;

namespace CarIndexReader
{
    // Обновлённые структуры
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct HeadRecord
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 200)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct EndRecord
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 200)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct CarRecord
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string carNameId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dsCrashModel;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
        public string dsShadowModel;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] FullCarName;

        public UInt32 enumA1;
        public UInt32 enumA2;
        public UInt16 enumB;
        public byte zeroOrFour;
        public byte zeroTWO;
        public UInt32 enumC;
        public UInt32 stealTimeMs;
        public UInt32 zero;
        public UInt32 unkB; // ранее enumD
        public UInt32 seatCount;
        public UInt32 enumD; // ранее enumE
        public UInt32 unkZ;
    }

    struct FileStructure
    {
        public HeadRecord start;
        public List<CarRecord> cars;
        public EndRecord end;
    }

    static class BinaryFileReader
    {
        // Размер структур
        private const int HeadRecordSize = 200;
        private const int EndRecordSize = 200;
        private const int CarRecordSize = 200; // вычислено из полей

        public static FileStructure ReadFileStructure(string filePath)
        {
            FileStructure fsData = new FileStructure();
            fsData.cars = new List<CarRecord>();

            using (var br = new BinaryReader(File.OpenRead(filePath)))
            {
                // Читаем HeadRecord
                fsData.start = ReadHeadRecord(br);

                // Читаем CarRecord до встречи EndRecord
                while (true)
                {
                    // Пытаемся увидеть, нет ли там записи EndRecord
                    // Для этого глянем следующий байт. Если EOF или 0x00, значит дальше EndRecord
                    if (br.BaseStream.Position + CarRecordSize > br.BaseStream.Length)
                    {
                        // Достигли конца файла раньше EndRecord - возможно ошибка формата.
                        // Но примем что EndRecord отсутствует.
                        break;
                    }

                    // Прочитаем первый байт следующего блока
                    var firstByte = br.ReadByte();
                    br.BaseStream.Seek(-1, SeekOrigin.Current); // вернём каретку назад

                    if (firstByte == 0x00)
                    {
                        // Похоже мы дошли до EndRecord
                        break;
                    }

                    // Читаем CarRecord
                    CarRecord car = ReadCarRecord(br);
                    fsData.cars.Add(car);
                }

                // Читаем EndRecord
                if (br.BaseStream.Position + EndRecordSize <= br.BaseStream.Length)
                {
                    fsData.end = ReadEndRecord(br);
                }
                else
                {
                    // Если нет - создадим пустой
                    fsData.end = new EndRecord { data = new byte[200] };
                }
            }

            return fsData;
        }

        public static void WriteFileStructure(string filePath, FileStructure fsData)
        {
            using (var bw = new BinaryWriter(File.Open(filePath, FileMode.Create, FileAccess.Write)))
            {
                // Запись HeadRecord
                WriteHeadRecord(bw, fsData.start);

                // Запись CarRecords
                foreach (var car in fsData.cars)
                {
                    WriteCarRecord(bw, car);
                }

                // Запись EndRecord
                WriteEndRecord(bw, fsData.end);
            }
        }

        private static HeadRecord ReadHeadRecord(BinaryReader br)
        {
            HeadRecord hr = new HeadRecord();
            hr.data = br.ReadBytes(HeadRecordSize);
            return hr;
        }

        private static EndRecord ReadEndRecord(BinaryReader br)
        {
            EndRecord er = new EndRecord();
            er.data = br.ReadBytes(EndRecordSize);
            return er;
        }

        private static CarRecord ReadCarRecord(BinaryReader br)
        {
            // Читаем массив байт для CarRecord
            byte[] buffer = br.ReadBytes(CarRecordSize);
            // Маршалим в структуру
            return ByteArrayToStructure<CarRecord>(buffer);
        }

        private static void WriteHeadRecord(BinaryWriter bw, HeadRecord hr)
        {
            bw.Write(hr.data, 0, hr.data.Length);
        }

        private static void WriteEndRecord(BinaryWriter bw, EndRecord er)
        {
            bw.Write(er.data, 0, er.data.Length);
        }

        private static void WriteCarRecord(BinaryWriter bw, CarRecord car)
        {
            byte[] buffer = StructureToByteArray(car);
            bw.Write(buffer);
        }

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, ptr, size);
                return (T)Marshal.PtrToStructure(ptr, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static byte[] StructureToByteArray<T>(T obj) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] bytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(obj, ptr, false);
                Marshal.Copy(ptr, bytes, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return bytes;
        }
    }

    static class HeadersLoader
    {
        public static Dictionary<string, string> LoadHeaders(string filePath)
        {
            var dict = new Dictionary<string, string>();
            if (!File.Exists(filePath)) return dict;

            foreach (var line in System.IO.File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
                var parts = line.Split(new char[] { '=' }, 2);
                if (parts.Length == 2)
                    dict[parts[0].Trim()] = parts[1].Trim();
            }
            return dict;
        }
    }

    static class EnumsLoader
    {
        public static Dictionary<string, Dictionary<string, uint>> LoadEnums(string filePath)
        {
            var result = new Dictionary<string, Dictionary<string, uint>>();
            if (!File.Exists(filePath)) return result;

            Dictionary<string, uint> currentEnum = null;
            string currentEnumName = null;

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentEnumName = trimmed.Trim('[', ']');
                    currentEnum = new Dictionary<string, uint>();
                    result[currentEnumName] = currentEnum;
                }
                else if (currentEnum != null && trimmed.Contains("="))
                {
                    var parts = trimmed.Split('=');
                    if (parts.Length == 2)
                    {
                        string name = parts[0].Trim();
                        string valStr = parts[1].Trim();
                        uint val = 0;
                        if (valStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            if (uint.TryParse(valStr.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val))
                            {
                                currentEnum[name] = val;
                            }
                        }
                        else
                        {
                            if (uint.TryParse(valStr, out val))
                            {
                                currentEnum[name] = val;
                            }
                        }
                    }
                }
            }

            return result;
        }
    }

    class ComboBoxItem
    {
        public string Name { get; set; }
        public uint Value { get; set; }
        public ComboBoxItem(string n, uint v)
        {
            Name = n;
            Value = v;
        }
        public override string ToString() => Name;
    }

    public class MainForm : Form
    {
        private FileStructure fileData;
        private Dictionary<string, string> headers;
        private Dictionary<string, Dictionary<string, uint>> enumsData;
        private int currentCarIndex = 0;

        // Контролы для редактирования CarRecord
        private TextBox txtCarNameId, txtDsCrashModel, txtDsShadowModel, txtFullCarName;
        private ComboBox comboEnumA1, comboEnumA2, comboEnumB, comboZeroOrFour, comboEnumC, comboEnumD;
        private TextBox txtZeroTWO, txtStealTimeMs, txtZero, txtUnkB, txtSeatCount, txtUnkZ;

        // Метки
        private Label lblCarNameId, lblDsCrashModel, lblDsShadowModel, lblFullCarName;
        private Label lblEnumA1, lblEnumA2, lblEnumB, lblZeroOrFour, lblZeroTWO, lblEnumC, lblStealTimeMs, lblZero, lblUnkB, lblSeatCount, lblEnumD, lblUnkZ;

        private Button btnSave, btnPrevCar, btnNextCar, btnAddCar, btnRemoveCar;

        // Dropdown для выбора CarRecord по FullCarName
        private ComboBox comboCarList;

        public MainForm()
        {
            this.Text = "Car Editor";
            this.Width = 1000;
            this.Height = 600;

            var mainPanel = new TableLayoutPanel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.ColumnCount = 4;
            mainPanel.RowCount = 20;
            for (int i = 0; i < 20; i++)
                mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < 4; i++)
                mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            // Инициализация контролов
            lblCarNameId = new Label() { AutoSize = true }; txtCarNameId = new TextBox();
            lblDsCrashModel = new Label() { AutoSize = true }; txtDsCrashModel = new TextBox();
            lblDsShadowModel = new Label() { AutoSize = true }; txtDsShadowModel = new TextBox();
            lblFullCarName = new Label() { AutoSize = true }; txtFullCarName = new TextBox();

            lblEnumA1 = new Label() { AutoSize = true }; comboEnumA1 = new ComboBox();
            lblEnumA2 = new Label() { AutoSize = true }; comboEnumA2 = new ComboBox();
            lblEnumB = new Label() { AutoSize = true }; comboEnumB = new ComboBox();
            lblZeroOrFour = new Label() { AutoSize = true }; comboZeroOrFour = new ComboBox();
            lblZeroTWO = new Label() { AutoSize = true }; txtZeroTWO = new TextBox() { ReadOnly = true };
            lblEnumC = new Label() { AutoSize = true }; comboEnumC = new ComboBox();
            lblStealTimeMs = new Label() { AutoSize = true }; txtStealTimeMs = new TextBox();
            lblZero = new Label() { AutoSize = true }; txtZero = new TextBox() { ReadOnly = true };
            lblUnkB = new Label() { AutoSize = true }; txtUnkB = new TextBox();
            lblSeatCount = new Label() { AutoSize = true }; txtSeatCount = new TextBox();
            lblEnumD = new Label() { AutoSize = true }; comboEnumD = new ComboBox();
            lblUnkZ = new Label() { AutoSize = true }; txtUnkZ = new TextBox();

            btnSave = new Button() { Text = "Save" };
            btnPrevCar = new Button() { Text = "Previous Car" };
            btnNextCar = new Button() { Text = "Next Car" };
            btnAddCar = new Button() { Text = "Add Car" };
            btnRemoveCar = new Button() { Text = "Remove Car" };

            comboCarList = new ComboBox() { Dock = DockStyle.Top };

            int row = 0;
            mainPanel.Controls.Add(comboCarList, 1, row);
            mainPanel.SetColumnSpan(comboCarList, 3);

            row++;

            mainPanel.Controls.Add(lblCarNameId, 0, row); mainPanel.Controls.Add(txtCarNameId, 1, row); row++;
            mainPanel.Controls.Add(lblDsCrashModel, 0, row); mainPanel.Controls.Add(txtDsCrashModel, 1, row); row++;
            mainPanel.Controls.Add(lblDsShadowModel, 0, row); mainPanel.Controls.Add(txtDsShadowModel, 1, row); row++;
            mainPanel.Controls.Add(lblFullCarName, 0, row); mainPanel.Controls.Add(txtFullCarName, 1, row); row++;

            mainPanel.Controls.Add(lblEnumA1, 0, row); mainPanel.Controls.Add(comboEnumA1, 1, row); row++;
            mainPanel.Controls.Add(lblEnumA2, 0, row); mainPanel.Controls.Add(comboEnumA2, 1, row); row++;
            mainPanel.Controls.Add(lblEnumB, 0, row); mainPanel.Controls.Add(comboEnumB, 1, row); row++;
            mainPanel.Controls.Add(lblZeroOrFour, 0, row); mainPanel.Controls.Add(comboZeroOrFour, 1, row); row++;
            mainPanel.Controls.Add(lblZeroTWO, 0, row); mainPanel.Controls.Add(txtZeroTWO, 1, row); row++;
            mainPanel.Controls.Add(lblEnumC, 0, row); mainPanel.Controls.Add(comboEnumC, 1, row); row++;
            mainPanel.Controls.Add(lblStealTimeMs, 0, row); mainPanel.Controls.Add(txtStealTimeMs, 1, row); row++;
            mainPanel.Controls.Add(lblZero, 0, row); mainPanel.Controls.Add(txtZero, 1, row); row++;
            mainPanel.Controls.Add(lblUnkB, 0, row); mainPanel.Controls.Add(txtUnkB, 1, row); row++;
            mainPanel.Controls.Add(lblSeatCount, 0, row); mainPanel.Controls.Add(txtSeatCount, 1, row); row++;
            mainPanel.Controls.Add(lblEnumD, 0, row); mainPanel.Controls.Add(comboEnumD, 1, row); row++;
            mainPanel.Controls.Add(lblUnkZ, 0, row); mainPanel.Controls.Add(txtUnkZ, 1, row); row++;

            mainPanel.Controls.Add(btnPrevCar, 0, row);
            mainPanel.Controls.Add(btnNextCar, 1, row);
            mainPanel.Controls.Add(btnAddCar, 2, row);
            mainPanel.Controls.Add(btnRemoveCar, 3, row);
            row++;
            mainPanel.Controls.Add(btnSave, 3, row);

            this.Controls.Add(mainPanel);

            this.Load += MainForm_Load;
            btnSave.Click += btnSave_Click;
            btnPrevCar.Click += btnPrevCar_Click;
            btnNextCar.Click += btnNextCar_Click;
            btnAddCar.Click += btnAddCar_Click;
            btnRemoveCar.Click += btnRemoveCar_Click;

            comboCarList.SelectedIndexChanged += (s, e) => {
                if (comboCarList.SelectedIndex >= 0 && comboCarList.SelectedIndex < fileData.cars.Count)
                {
                    currentCarIndex = comboCarList.SelectedIndex;
                    ShowCarRecord(currentCarIndex);
                }
            };
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            string opennedFile = "";

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // Фильтр по имени файла
                openFileDialog.Filter = "carindex.def|carindex.def";
                openFileDialog.Title = "Выберите файл carindex.def";

                // Открыть диалоговое окно
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFilePath = openFileDialog.FileName;

                    // Проверка на существование выбранного файла
                    if (Path.GetFileName(selectedFilePath) == "carindex.def")
                    {
                        opennedFile = selectedFilePath;
                     }
                    else
                    {
                        MessageBox.Show("Selected Incorrect File", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Environment.Exit(1);
                    }
                }
                else
                {
                    MessageBox.Show("File not selected. Shutdown", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Environment.Exit(1);
                }
            }

            fileData = BinaryFileReader.ReadFileStructure(opennedFile);
            headers = HeadersLoader.LoadHeaders("headers.txt");
            enumsData = EnumsLoader.LoadEnums("enums.txt");

            PopulateCarList();
            if (fileData.cars.Count > 0)
            {
                currentCarIndex = 0;
                ShowCarRecord(currentCarIndex);
            }
        }

        private void PopulateCarList()
        {
            comboCarList.Items.Clear();
            foreach (var c in fileData.cars)
            {
                comboCarList.Items.Add(c.carNameId);
            }
            if (fileData.cars.Count > 0)
                comboCarList.SelectedIndex = 0;
        }

        private void ShowCarRecord(int index)
        {
            if (index < 0 || index >= fileData.cars.Count) return;
            var car = fileData.cars[index];

            lblCarNameId.Text = GetHeader("carNameId");
            lblDsCrashModel.Text = GetHeader("dsCrashModel");
            lblDsShadowModel.Text = GetHeader("dsShadowModel");
            lblFullCarName.Text = GetHeader("FullCarName");

            lblEnumA1.Text = GetHeader("enumA1");
            lblEnumA2.Text = GetHeader("enumA2");
            lblEnumB.Text = GetHeader("enumB");
            lblZeroOrFour.Text = GetHeader("zeroOrFour");
            lblZeroTWO.Text = GetHeader("zeroTWO");
            lblEnumC.Text = GetHeader("enumC");
            lblStealTimeMs.Text = GetHeader("stealTimeMs");
            lblZero.Text = GetHeader("zero");
            lblUnkB.Text = GetHeader("unkB");
            lblSeatCount.Text = GetHeader("seatCount");
            lblEnumD.Text = GetHeader("enumD");
            lblUnkZ.Text = GetHeader("unkZ");

            txtCarNameId.Text = car.carNameId;
            txtDsCrashModel.Text = car.dsCrashModel;
            txtDsShadowModel.Text = car.dsShadowModel;

            byte[] cleanCarName = new byte[64];

            int k = 0;
            for (int i = 0; i < car.FullCarName.Length; i+=2)
            {
                cleanCarName[k] = car.FullCarName[i]; 
                k++;
            }

            txtFullCarName.Text = System.Text.Encoding.ASCII.GetString(cleanCarName.ToArray());
            txtZeroTWO.Text = car.zeroTWO.ToString("X2");
            txtStealTimeMs.Text = car.stealTimeMs.ToString();
            txtZero.Text = car.zero.ToString("X");
            txtUnkB.Text = car.unkB.ToString();
            txtSeatCount.Text = car.seatCount.ToString();
            txtUnkZ.Text = car.unkZ.ToString();

            FillComboBox(comboEnumA1, "enumA1", car.enumA1);
            FillComboBox(comboEnumA2, "enumA2", car.enumA2);
            FillComboBox(comboEnumB, "enumB", car.enumB);
            FillComboBox(comboZeroOrFour, "zeroOrFour", car.zeroOrFour);
            FillComboBox(comboEnumC, "enumC", car.enumC);
            FillComboBox(comboEnumD, "enumD", car.enumD);
        }

        private string GetHeader(string fieldName)
        {
            return headers.ContainsKey(fieldName) ? headers[fieldName] : fieldName;
        }

        private void FillComboBox(ComboBox combo, string enumName, uint currentValue)
        {
            combo.Items.Clear();
            if (enumsData.ContainsKey(enumName))
            {
                foreach (var kvp in enumsData[enumName])
                {
                    combo.Items.Add(new ComboBoxItem(kvp.Key, kvp.Value));
                }
                // Найдём индекс текущего значения
                int idx = combo.Items.Cast<ComboBoxItem>().ToList().FindIndex(i => i.Value == currentValue);
                if (idx >= 0)
                    combo.SelectedIndex = idx;
                else if (combo.Items.Count > 0)
                    combo.SelectedIndex = 0;
            }
            else
            {
                // Если нет в enumsData, просто показываем hex
                combo.Items.Add(new ComboBoxItem("0x" + currentValue.ToString("X"), currentValue));
                combo.SelectedIndex = 0;
            }
        }

        private void FillComboBox(ComboBox combo, string enumName, ushort currentValue)
        {
            FillComboBox(combo, enumName, (uint)currentValue);
        }

        private void FillComboBox(ComboBox combo, string enumName, byte currentValue)
        {
            FillComboBox(combo, enumName, (uint)currentValue);
        }

        private uint GetSelectedComboValue(ComboBox combo, uint defaultVal)
        {
            if (combo.SelectedItem is ComboBoxItem item)
            {
                return item.Value;
            }
            return defaultVal;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (currentCarIndex < 0 || currentCarIndex >= fileData.cars.Count) return;
            var car = fileData.cars[currentCarIndex];

            car.carNameId = txtCarNameId.Text;
            car.dsCrashModel = txtDsCrashModel.Text;
            car.dsShadowModel = txtDsShadowModel.Text;

            car.FullCarName = new byte[64]; 
            byte[] rawCarName = System.Text.Encoding.ASCII.GetBytes(txtFullCarName.Text);

            int f = 0;
            for (int i = 0; i < rawCarName.Length; i++)
            {
                car.FullCarName[f] = rawCarName[i];

                f += 2;
            }


            UInt32.TryParse(txtStealTimeMs.Text, out car.stealTimeMs);
            UInt32.TryParse(txtZero.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out car.zero);
            UInt32.TryParse(txtUnkB.Text, out car.unkB);
            UInt32.TryParse(txtSeatCount.Text, out car.seatCount);
            UInt32.TryParse(txtUnkZ.Text, out car.unkZ);

            car.enumA1 = GetSelectedComboValue(comboEnumA1, car.enumA1);
            car.enumA2 = GetSelectedComboValue(comboEnumA2, car.enumA2);
            car.enumB = (UInt16)GetSelectedComboValue(comboEnumB, car.enumB);
            car.zeroOrFour = (byte)GetSelectedComboValue(comboZeroOrFour, car.zeroOrFour);
            car.enumC = GetSelectedComboValue(comboEnumC, car.enumC);
            car.enumD = GetSelectedComboValue(comboEnumD, car.enumD);

            fileData.cars[currentCarIndex] = car;

            BinaryFileReader.WriteFileStructure("carindex.def", fileData);
            PopulateCarList(); // обновим список
            MessageBox.Show("Saved!");
        }

        private void btnPrevCar_Click(object sender, EventArgs e)
        {
            if (currentCarIndex > 0)
            {
                currentCarIndex--;
                comboCarList.SelectedIndex = currentCarIndex;
                ShowCarRecord(currentCarIndex);
            }
        }

        private void btnNextCar_Click(object sender, EventArgs e)
        {
            if (currentCarIndex < fileData.cars.Count - 1)
            {
                currentCarIndex++;
                comboCarList.SelectedIndex = currentCarIndex;
                ShowCarRecord(currentCarIndex);
            }
        }

        private void btnAddCar_Click(object sender, EventArgs e)
        {
            // Добавляем пустую запись
            CarRecord newCar = new CarRecord
            {
                carNameId = "NewCar",
                dsCrashModel = "",
                dsShadowModel = "",
                FullCarName = new byte[64],
                // Остальные поля по умолчанию = 0
            };

            fileData.cars.Add(newCar);
            BinaryFileReader.WriteFileStructure("carindex.def", fileData);
            PopulateCarList();
            currentCarIndex = fileData.cars.Count - 1;
            comboCarList.SelectedIndex = currentCarIndex;
            ShowCarRecord(currentCarIndex);
        }

        private void btnRemoveCar_Click(object sender, EventArgs e)
        {
            if (fileData.cars.Count == 0) return;
            if (currentCarIndex < 0 || currentCarIndex >= fileData.cars.Count) return;

            fileData.cars.RemoveAt(currentCarIndex);
            if (currentCarIndex >= fileData.cars.Count)
                currentCarIndex = fileData.cars.Count - 1;

            BinaryFileReader.WriteFileStructure("carindex.def", fileData);
            PopulateCarList();
            if (currentCarIndex >= 0 && fileData.cars.Count > 0)
            {
                comboCarList.SelectedIndex = currentCarIndex;
                ShowCarRecord(currentCarIndex);
            }
            else
            {
                // Нет больше записей
                ClearFields();
            }
        }

        private void ClearFields()
        {
            txtCarNameId.Text = "";
            txtDsCrashModel.Text = "";
            txtDsShadowModel.Text = "";
            txtFullCarName.Text = "";
            txtZeroTWO.Text = "";
            txtStealTimeMs.Text = "";
            txtZero.Text = "";
            txtUnkB.Text = "";
            txtSeatCount.Text = "";
            txtUnkZ.Text = "";
            comboEnumA1.Items.Clear();
            comboEnumA2.Items.Clear();
            comboEnumB.Items.Clear();
            comboZeroOrFour.Items.Clear();
            comboEnumC.Items.Clear();
            comboEnumD.Items.Clear();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
