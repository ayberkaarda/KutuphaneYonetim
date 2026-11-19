namespace KutuphaneYonetim.UI
{
    partial class KitapForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(KitapForm));
            this.txtAd = new System.Windows.Forms.TextBox();
            this.txtYazar = new System.Windows.Forms.TextBox();
            this.btnEkle = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnSil = new System.Windows.Forms.Button();
            this.txtIsbn = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.lblAra = new System.Windows.Forms.Label();
            this.txtAra = new System.Windows.Forms.TextBox();
            this.cmbDurum = new System.Windows.Forms.ComboBox();
            this.btnAra = new System.Windows.Forms.Button();
            this.btnOnceki = new System.Windows.Forms.Button();
            this.btnSonraki = new System.Windows.Forms.Button();
            this.lblSayfa = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            //
            // lblAra
            //
            this.lblAra.AutoSize = true;
            this.lblAra.Location = new System.Drawing.Point(12, 15);
            this.lblAra.Name = "lblAra";
            this.lblAra.Size = new System.Drawing.Size(27, 16);
            this.lblAra.TabIndex = 7;
            this.lblAra.Text = "Ara";
            //
            // txtAra
            //
            this.txtAra.Location = new System.Drawing.Point(50, 12);
            this.txtAra.Name = "txtAra";
            this.txtAra.Size = new System.Drawing.Size(300, 22);
            this.txtAra.TabIndex = 8;
            this.txtAra.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtAra_KeyDown);
            //
            // cmbDurum
            //
            this.cmbDurum.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDurum.FormattingEnabled = true;
            this.cmbDurum.Location = new System.Drawing.Point(360, 11);
            this.cmbDurum.Name = "cmbDurum";
            this.cmbDurum.Size = new System.Drawing.Size(150, 24);
            this.cmbDurum.TabIndex = 9;
            this.cmbDurum.SelectionChangeCommitted += new System.EventHandler(this.cmbDurum_SelectionChangeCommitted);
            //
            // btnAra
            //
            this.btnAra.Location = new System.Drawing.Point(520, 9);
            this.btnAra.Name = "btnAra";
            this.btnAra.Size = new System.Drawing.Size(90, 28);
            this.btnAra.TabIndex = 10;
            this.btnAra.Text = "Ara";
            this.btnAra.UseVisualStyleBackColor = true;
            this.btnAra.Click += new System.EventHandler(this.btnAra_Click);
            //
            // dataGridView1
            //
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(12, 45);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowTemplate.Height = 24;
            this.dataGridView1.Size = new System.Drawing.Size(776, 190);
            this.dataGridView1.TabIndex = 3;
            this.dataGridView1.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellClick);
            //
            // btnOnceki
            //
            this.btnOnceki.Location = new System.Drawing.Point(12, 241);
            this.btnOnceki.Name = "btnOnceki";
            this.btnOnceki.Size = new System.Drawing.Size(90, 28);
            this.btnOnceki.TabIndex = 11;
            this.btnOnceki.Text = "< Önceki";
            this.btnOnceki.UseVisualStyleBackColor = true;
            this.btnOnceki.Click += new System.EventHandler(this.btnOnceki_Click);
            //
            // lblSayfa
            //
            this.lblSayfa.AutoSize = true;
            this.lblSayfa.Location = new System.Drawing.Point(110, 247);
            this.lblSayfa.Name = "lblSayfa";
            this.lblSayfa.Size = new System.Drawing.Size(0, 16);
            this.lblSayfa.TabIndex = 12;
            //
            // btnSonraki
            //
            this.btnSonraki.Location = new System.Drawing.Point(698, 241);
            this.btnSonraki.Name = "btnSonraki";
            this.btnSonraki.Size = new System.Drawing.Size(90, 28);
            this.btnSonraki.TabIndex = 13;
            this.btnSonraki.Text = "Sonraki >";
            this.btnSonraki.UseVisualStyleBackColor = true;
            this.btnSonraki.Click += new System.EventHandler(this.btnSonraki_Click);
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(72, 293);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 16);
            this.label1.TabIndex = 4;
            this.label1.Text = "Kitap Adı";
            //
            // txtAd
            //
            this.txtAd.Location = new System.Drawing.Point(143, 290);
            this.txtAd.Name = "txtAd";
            this.txtAd.Size = new System.Drawing.Size(175, 22);
            this.txtAd.TabIndex = 0;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(90, 333);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(42, 16);
            this.label2.TabIndex = 5;
            this.label2.Text = "Yazar";
            //
            // txtYazar
            //
            this.txtYazar.Location = new System.Drawing.Point(143, 330);
            this.txtYazar.Name = "txtYazar";
            this.txtYazar.Size = new System.Drawing.Size(175, 22);
            this.txtYazar.TabIndex = 1;
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(95, 373);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(37, 16);
            this.label3.TabIndex = 14;
            this.label3.Text = "ISBN";
            //
            // txtIsbn
            //
            this.txtIsbn.Location = new System.Drawing.Point(143, 370);
            this.txtIsbn.MaxLength = 17;
            this.txtIsbn.Name = "txtIsbn";
            this.txtIsbn.Size = new System.Drawing.Size(175, 22);
            this.txtIsbn.TabIndex = 2;
            //
            // btnEkle
            //
            this.btnEkle.Location = new System.Drawing.Point(171, 410);
            this.btnEkle.Name = "btnEkle";
            this.btnEkle.Size = new System.Drawing.Size(115, 41);
            this.btnEkle.TabIndex = 15;
            this.btnEkle.Text = "Kitap Ekle";
            this.btnEkle.UseVisualStyleBackColor = true;
            this.btnEkle.Click += new System.EventHandler(this.btnEkle_Click);
            //
            // btnSil
            //
            this.btnSil.Location = new System.Drawing.Point(571, 330);
            this.btnSil.Name = "btnSil";
            this.btnSil.Size = new System.Drawing.Size(115, 41);
            this.btnSil.TabIndex = 6;
            this.btnSil.Text = "Kitap Sil";
            this.btnSil.UseVisualStyleBackColor = true;
            this.btnSil.Click += new System.EventHandler(this.btnSil_Click);
            //
            // KitapForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 465);
            this.Controls.Add(this.lblSayfa);
            this.Controls.Add(this.btnSonraki);
            this.Controls.Add(this.btnOnceki);
            this.Controls.Add(this.btnAra);
            this.Controls.Add(this.cmbDurum);
            this.Controls.Add(this.txtAra);
            this.Controls.Add(this.lblAra);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtIsbn);
            this.Controls.Add(this.btnSil);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.btnEkle);
            this.Controls.Add(this.txtYazar);
            this.Controls.Add(this.txtAd);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "KitapForm";
            this.Text = "Kitap Ekle | Sil";
            this.Load += new System.EventHandler(this.KitapForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtAd;
        private System.Windows.Forms.TextBox txtYazar;
        private System.Windows.Forms.Button btnEkle;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnSil;
        private System.Windows.Forms.TextBox txtIsbn;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblAra;
        private System.Windows.Forms.TextBox txtAra;
        private System.Windows.Forms.ComboBox cmbDurum;
        private System.Windows.Forms.Button btnAra;
        private System.Windows.Forms.Button btnOnceki;
        private System.Windows.Forms.Button btnSonraki;
        private System.Windows.Forms.Label lblSayfa;
    }
}