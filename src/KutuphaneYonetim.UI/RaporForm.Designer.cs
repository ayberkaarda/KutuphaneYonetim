namespace KutuphaneYonetim.UI
{
    partial class RaporForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RaporForm));
            this.label1 = new System.Windows.Forms.Label();
            this.cmbRapor = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.dtpBaslangic = new System.Windows.Forms.DateTimePicker();
            this.label3 = new System.Windows.Forms.Label();
            this.dtpBitis = new System.Windows.Forms.DateTimePicker();
            this.label4 = new System.Windows.Forms.Label();
            this.nudAdet = new System.Windows.Forms.NumericUpDown();
            this.btnGoster = new System.Windows.Forms.Button();
            this.btnCsv = new System.Windows.Forms.Button();
            this.lblOzet = new System.Windows.Forms.Label();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.nudAdet)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(42, 16);
            this.label1.TabIndex = 7;
            this.label1.Text = "Rapor";
            //
            // cmbRapor
            //
            this.cmbRapor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRapor.FormattingEnabled = true;
            this.cmbRapor.Location = new System.Drawing.Point(70, 12);
            this.cmbRapor.Name = "cmbRapor";
            this.cmbRapor.Size = new System.Drawing.Size(220, 24);
            this.cmbRapor.TabIndex = 0;
            this.cmbRapor.SelectionChangeCommitted += new System.EventHandler(this.cmbRapor_SelectionChangeCommitted);
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(305, 15);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(62, 16);
            this.label2.TabIndex = 8;
            this.label2.Text = "Başlangıç";
            //
            // dtpBaslangic
            //
            this.dtpBaslangic.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpBaslangic.Location = new System.Drawing.Point(385, 12);
            this.dtpBaslangic.Name = "dtpBaslangic";
            this.dtpBaslangic.Size = new System.Drawing.Size(120, 22);
            this.dtpBaslangic.TabIndex = 1;
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(518, 15);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(34, 16);
            this.label3.TabIndex = 9;
            this.label3.Text = "Bitiş";
            //
            // dtpBitis
            //
            this.dtpBitis.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpBitis.Location = new System.Drawing.Point(565, 12);
            this.dtpBitis.Name = "dtpBitis";
            this.dtpBitis.Size = new System.Drawing.Size(120, 22);
            this.dtpBitis.TabIndex = 2;
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(700, 15);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(22, 16);
            this.label4.TabIndex = 10;
            this.label4.Text = "İlk";
            //
            // nudAdet
            //
            this.nudAdet.Location = new System.Drawing.Point(735, 12);
            this.nudAdet.Name = "nudAdet";
            this.nudAdet.Size = new System.Drawing.Size(65, 22);
            this.nudAdet.TabIndex = 3;
            //
            // btnGoster
            //
            this.btnGoster.Location = new System.Drawing.Point(12, 47);
            this.btnGoster.Name = "btnGoster";
            this.btnGoster.Size = new System.Drawing.Size(120, 30);
            this.btnGoster.TabIndex = 4;
            this.btnGoster.Text = "Göster";
            this.btnGoster.UseVisualStyleBackColor = true;
            this.btnGoster.Click += new System.EventHandler(this.btnGoster_Click);
            //
            // btnCsv
            //
            this.btnCsv.Location = new System.Drawing.Point(140, 47);
            this.btnCsv.Name = "btnCsv";
            this.btnCsv.Size = new System.Drawing.Size(150, 30);
            this.btnCsv.TabIndex = 5;
            this.btnCsv.Text = "CSV Dışa Aktar";
            this.btnCsv.UseVisualStyleBackColor = true;
            this.btnCsv.Click += new System.EventHandler(this.btnCsv_Click);
            //
            // lblOzet
            //
            this.lblOzet.AutoSize = true;
            this.lblOzet.Location = new System.Drawing.Point(305, 54);
            this.lblOzet.Name = "lblOzet";
            this.lblOzet.Size = new System.Drawing.Size(0, 16);
            this.lblOzet.TabIndex = 11;
            //
            // dataGridView1
            //
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(12, 87);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowTemplate.Height = 24;
            this.dataGridView1.Size = new System.Drawing.Size(876, 410);
            this.dataGridView1.TabIndex = 6;
            //
            // RaporForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 510);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.lblOzet);
            this.Controls.Add(this.btnCsv);
            this.Controls.Add(this.btnGoster);
            this.Controls.Add(this.nudAdet);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.dtpBitis);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.dtpBaslangic);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cmbRapor);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "RaporForm";
            this.Text = "Raporlar";
            this.Load += new System.EventHandler(this.RaporForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.nudAdet)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbRapor;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker dtpBaslangic;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.DateTimePicker dtpBitis;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.NumericUpDown nudAdet;
        private System.Windows.Forms.Button btnGoster;
        private System.Windows.Forms.Button btnCsv;
        private System.Windows.Forms.Label lblOzet;
        private System.Windows.Forms.DataGridView dataGridView1;
    }
}
