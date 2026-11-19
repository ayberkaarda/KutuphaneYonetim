namespace KutuphaneYonetim.UI
{
    partial class UyeForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UyeForm));
            this.txtAd = new System.Windows.Forms.TextBox();
            this.txtSoyad = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnEkle = new System.Windows.Forms.Button();
            this.btnSil = new System.Windows.Forms.Button();
            this.btnCezaOde = new System.Windows.Forms.Button();
            this.lblAra = new System.Windows.Forms.Label();
            this.txtAra = new System.Windows.Forms.TextBox();
            this.cmbFiltre = new System.Windows.Forms.ComboBox();
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
            this.lblAra.Location = new System.Drawing.Point(24, 15);
            this.lblAra.Name = "lblAra";
            this.lblAra.Size = new System.Drawing.Size(27, 16);
            this.lblAra.TabIndex = 8;
            this.lblAra.Text = "Ara";
            //
            // txtAra
            //
            this.txtAra.Location = new System.Drawing.Point(60, 12);
            this.txtAra.Name = "txtAra";
            this.txtAra.Size = new System.Drawing.Size(220, 22);
            this.txtAra.TabIndex = 9;
            this.txtAra.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtAra_KeyDown);
            //
            // cmbFiltre
            //
            this.cmbFiltre.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFiltre.FormattingEnabled = true;
            this.cmbFiltre.Location = new System.Drawing.Point(290, 11);
            this.cmbFiltre.Name = "cmbFiltre";
            this.cmbFiltre.Size = new System.Drawing.Size(190, 24);
            this.cmbFiltre.TabIndex = 10;
            this.cmbFiltre.SelectionChangeCommitted += new System.EventHandler(this.cmbFiltre_SelectionChangeCommitted);
            //
            // btnAra
            //
            this.btnAra.Location = new System.Drawing.Point(490, 9);
            this.btnAra.Name = "btnAra";
            this.btnAra.Size = new System.Drawing.Size(111, 28);
            this.btnAra.TabIndex = 11;
            this.btnAra.Text = "Ara";
            this.btnAra.UseVisualStyleBackColor = true;
            this.btnAra.Click += new System.EventHandler(this.btnAra_Click);
            //
            // dataGridView1
            //
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(24, 45);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowTemplate.Height = 24;
            this.dataGridView1.Size = new System.Drawing.Size(577, 170);
            this.dataGridView1.TabIndex = 2;
            this.dataGridView1.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellClick);
            //
            // btnOnceki
            //
            this.btnOnceki.Location = new System.Drawing.Point(24, 221);
            this.btnOnceki.Name = "btnOnceki";
            this.btnOnceki.Size = new System.Drawing.Size(90, 28);
            this.btnOnceki.TabIndex = 12;
            this.btnOnceki.Text = "< Önceki";
            this.btnOnceki.UseVisualStyleBackColor = true;
            this.btnOnceki.Click += new System.EventHandler(this.btnOnceki_Click);
            //
            // lblSayfa
            //
            this.lblSayfa.AutoSize = true;
            this.lblSayfa.Location = new System.Drawing.Point(122, 227);
            this.lblSayfa.Name = "lblSayfa";
            this.lblSayfa.Size = new System.Drawing.Size(0, 16);
            this.lblSayfa.TabIndex = 13;
            //
            // btnSonraki
            //
            this.btnSonraki.Location = new System.Drawing.Point(511, 221);
            this.btnSonraki.Name = "btnSonraki";
            this.btnSonraki.Size = new System.Drawing.Size(90, 28);
            this.btnSonraki.TabIndex = 14;
            this.btnSonraki.Text = "Sonraki >";
            this.btnSonraki.UseVisualStyleBackColor = true;
            this.btnSonraki.Click += new System.EventHandler(this.btnSonraki_Click);
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(118, 271);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(24, 16);
            this.label1.TabIndex = 3;
            this.label1.Text = "Ad";
            //
            // txtAd
            //
            this.txtAd.Location = new System.Drawing.Point(188, 268);
            this.txtAd.Name = "txtAd";
            this.txtAd.Size = new System.Drawing.Size(100, 22);
            this.txtAd.TabIndex = 0;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(118, 311);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 16);
            this.label2.TabIndex = 4;
            this.label2.Text = "Soyad";
            //
            // txtSoyad
            //
            this.txtSoyad.Location = new System.Drawing.Point(188, 308);
            this.txtSoyad.Name = "txtSoyad";
            this.txtSoyad.Size = new System.Drawing.Size(100, 22);
            this.txtSoyad.TabIndex = 1;
            //
            // btnEkle
            //
            this.btnEkle.Location = new System.Drawing.Point(188, 350);
            this.btnEkle.Name = "btnEkle";
            this.btnEkle.Size = new System.Drawing.Size(90, 35);
            this.btnEkle.TabIndex = 5;
            this.btnEkle.Text = "Üye Ekle";
            this.btnEkle.UseVisualStyleBackColor = true;
            this.btnEkle.Click += new System.EventHandler(this.btnEkle_Click);
            //
            // btnSil
            //
            this.btnSil.Location = new System.Drawing.Point(439, 268);
            this.btnSil.Name = "btnSil";
            this.btnSil.Size = new System.Drawing.Size(162, 35);
            this.btnSil.TabIndex = 6;
            this.btnSil.Text = "Seçili Üyeyi Sil";
            this.btnSil.UseVisualStyleBackColor = true;
            this.btnSil.Click += new System.EventHandler(this.btnSil_Click);
            //
            // btnCezaOde
            //
            this.btnCezaOde.Location = new System.Drawing.Point(439, 318);
            this.btnCezaOde.Name = "btnCezaOde";
            this.btnCezaOde.Size = new System.Drawing.Size(162, 35);
            this.btnCezaOde.TabIndex = 7;
            this.btnCezaOde.Text = "Cezayı Öde";
            this.btnCezaOde.UseVisualStyleBackColor = true;
            this.btnCezaOde.Click += new System.EventHandler(this.btnCezaOde_Click);
            //
            // UyeForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(639, 410);
            this.Controls.Add(this.lblSayfa);
            this.Controls.Add(this.btnSonraki);
            this.Controls.Add(this.btnOnceki);
            this.Controls.Add(this.btnAra);
            this.Controls.Add(this.cmbFiltre);
            this.Controls.Add(this.txtAra);
            this.Controls.Add(this.lblAra);
            this.Controls.Add(this.btnCezaOde);
            this.Controls.Add(this.btnSil);
            this.Controls.Add(this.btnEkle);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.txtSoyad);
            this.Controls.Add(this.txtAd);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "UyeForm";
            this.Text = "Üye Ekle | Sil";
            this.Load += new System.EventHandler(this.UyeForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtAd;
        private System.Windows.Forms.TextBox txtSoyad;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnEkle;
        private System.Windows.Forms.Button btnSil;
        private System.Windows.Forms.Button btnCezaOde;
        private System.Windows.Forms.Label lblAra;
        private System.Windows.Forms.TextBox txtAra;
        private System.Windows.Forms.ComboBox cmbFiltre;
        private System.Windows.Forms.Button btnAra;
        private System.Windows.Forms.Button btnOnceki;
        private System.Windows.Forms.Button btnSonraki;
        private System.Windows.Forms.Label lblSayfa;
    }
}