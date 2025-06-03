using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SpreadsheetApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SpreadsheetForm());
        }
    }

    public class SpreadsheetForm : Form
    {
        private readonly DataGridView grid = new DataGridView();
        private readonly Button btnLoad = new Button();
        private readonly Button btnSave = new Button();

        private SharableSpreadSheet sheet;
        private readonly object syncObj = new object();

        private readonly Color ACCENT = Color.FromArgb(0, 120, 215);   
        private readonly Color ACCENT_LIGHT = Color.FromArgb(224, 243, 255);
        private readonly Color ACCENT_DARK = Color.FromArgb(0, 99, 177);

        public SpreadsheetForm()
        {
            Text = "Sharable Spreadsheet";
            Width = 1000;
            Height = 700;
            Font = new Font("Segoe UI", 10f);
            BackColor = Color.WhiteSmoke;

            grid.Dock = DockStyle.Fill; //the grid behaviour
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.EnableHeadersVisualStyles = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;

            grid.GridColor = ACCENT_LIGHT; //setting colors
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = ACCENT_LIGHT;
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            grid.ColumnHeadersDefaultCellStyle.BackColor = ACCENT;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);

            grid.CellEndEdit += Grid_CellEndEdit;

            StyleButton(btnLoad, "Load"); //style and bind actions for the load and save buttons
            StyleButton(btnSave, "Save");
            btnLoad.Click += BtnLoad_Click;
            btnSave.Click += BtnSave_Click;

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(10),
                BackColor = ACCENT
            };
            panel.Controls.AddRange(new Control[] { btnLoad, btnSave });

            Controls.Add(grid);
            Controls.Add(panel);

            InitializeEmptySheet(10, 10); //initialising by default empty 10x10 spreadsheet
        }

        private void StyleButton(Button btn, string text) //design stuff
        {
            btn.Text = text;
            btn.AutoSize = true;
            btn.Margin = new Padding(5);
            btn.Padding = new Padding(10, 6, 10, 6);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.White;
            btn.ForeColor = ACCENT_DARK;
            btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        }

        private void InitializeEmptySheet(int rows, int cols)
        {
            sheet = new SharableSpreadSheet(rows, cols);
            SyncToGrid(); //creating new empty spreadsheet
        }
        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e) //updating the spreadsheet info after editing a cell
        {
            if (sheet == null) return;
            int r = e.RowIndex;
            int c = e.ColumnIndex;
            string value = grid.Rows[r].Cells[c].Value?.ToString() ?? string.Empty;
            Task.Run(() => sheet.setCell(r, c, value));
        }
        private void BtnLoad_Click(object sender, EventArgs e) //to load info from a csv file
        {
            using var dlg = new OpenFileDialog { Filter = "CSV|*.csv|All|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Cursor = Cursors.WaitCursor;
            Task.Run(() =>
            {
                lock (syncObj) sheet.load(dlg.FileName); //locking to not have multiple threads try to load at the same time creating race condition
                BeginInvoke((Action)(SyncToGrid));
            });
        }

        private void BtnSave_Click(object sender, EventArgs e) //to save the file to a csv file
        {
            using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", DefaultExt = "csv" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            Cursor = Cursors.WaitCursor;
            Task.Run(() =>
            {
                lock (syncObj) sheet.save(dlg.FileName);
                BeginInvoke((Action)(() => Cursor = Cursors.Default));
            });
        }
        private void SyncToGrid() //copying spreadsheet content into the UI
        {
            lock (syncObj)
            {
                grid.SuspendLayout();
                grid.Columns.Clear();
                int rows = GetSheetRows();
                int cols = GetSheetCols();

                for (int c = 0; c < cols; c++)    //creating columns with numbered headers
                    grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = (c + 1).ToString() });

                grid.Rows.Clear();
                grid.Rows.Add(rows);

                for (int r = 0; r < rows; r++) //filling the value of each cell
                    for (int c = 0; c < cols; c++)
                        grid.Rows[r].Cells[c].Value = sheet.getCell(r, c);

                grid.ResumeLayout();
                Cursor = Cursors.Default;
            }
        }
        private int GetSheetRows() => (int)typeof(SharableSpreadSheet) //using reflection to access private 'rows' field
                                        .GetField("rows", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                        .GetValue(sheet);

        private int GetSheetCols() => (int)typeof(SharableSpreadSheet) //using reflection to access private 'cols' field
                                        .GetField("cols", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                                        .GetValue(sheet);
    }
}