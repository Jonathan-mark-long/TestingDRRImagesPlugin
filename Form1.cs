using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Testing_DRR_Images
{
    public partial class Form1 : Form
    {
        private TextBox textBox1;

        private DataGridView datagridview1;
        public Form1(double IFP, double HIF, double MHD, double TotalDose, Color ILFColour, Color HIFColour, Color HIF2Colour, Color MHDColour, Color MHD2Colour)
        {
            InitializeComponent(IFP, HIF, MHD, TotalDose, ILFColour, HIFColour, HIF2Colour, MHDColour, MHD2Colour);
        }

        private void InitializeComponent(double IFP, double HIF, double MHD, double TotalDose, Color ILFColour, Color HIFColour, Color HIF2Colour, Color MHDColour, Color MHD2Colour)
        {
            this.datagridview1 = new DataGridView();
            this.SuspendLayout();
            this.datagridview1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.datagridview1.ReadOnly = true;

            this.datagridview1.ColumnCount = 5;
            this.datagridview1.Columns[0].Name = "Prescription";
            this.datagridview1.Columns[1].Name = "Dose Constraint (Gy)";
            this.datagridview1.Columns[2].Name = "% In Field Constraint";
            this.datagridview1.Columns[3].Name = "% In Field Calculated";
            this.datagridview1.Columns[4].Name = "MHD Calculated";

            this.datagridview1.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            this.datagridview1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            this.datagridview1.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            this.datagridview1.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            this.datagridview1.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

            if (TotalDose > 40)
            {
                string[] row = new string[] { "40.05Gy", "V18Gy ≤ 15%", "%ILF ≤ 13.5%", IFP.ToString() + "%", "---" };
                datagridview1.Rows.Add(row);
                string[] row2 = new string[] { "40.05Gy", "MHD ≤ 4Gy", "%HIF ≤ 7%", HIF.ToString() + "%", MHD.ToString() + "Gy" };
                datagridview1.Rows.Add(row2);
                string[] row3 = new string[] { "40.05Gy", "MHD ≤ 2Gy", "%HIF ≤ 1.7%", HIF.ToString() + "%", MHD.ToString() + "Gy" };
                datagridview1.Rows.Add(row3);

                datagridview1.Rows[0].Cells[3].Style.BackColor = ILFColour;
                datagridview1.Rows[1].Cells[3].Style.BackColor = HIFColour;
                datagridview1.Rows[1].Cells[4].Style.BackColor = MHDColour;
                datagridview1.Rows[2].Cells[3].Style.BackColor = HIF2Colour;
                datagridview1.Rows[2].Cells[4].Style.BackColor = MHD2Colour;

            }
            else
            {
                string[] row = new string[] { "26Gy", "V11.7Gy ≤ 15%", "%ILF ≤ 13%", IFP.ToString() + "%", "---" };
                datagridview1.Rows.Add(row);
                string[] row2 = new string[] { "26Gy", "MHD ≤ 2Gy", "%HIF ≤ 1.7%", HIF.ToString() + "%", MHD.ToString() + "Gy" };
                datagridview1.Rows.Add(row2);

                datagridview1.Rows[0].Cells[3].Style.BackColor = ILFColour;
                datagridview1.Rows[1].Cells[3].Style.BackColor = HIFColour;
                datagridview1.Rows[1].Cells[4].Style.BackColor = MHDColour;
            }

            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(650, 250);
            this.Controls.Add(this.datagridview1);
            this.Text = "";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }


}
