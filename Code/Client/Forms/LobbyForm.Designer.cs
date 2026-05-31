namespace Client.Forms
{
    partial class LobbyForm
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lstPlayers = new System.Windows.Forms.ListBox();
            this.btnCreateRoom = new System.Windows.Forms.Button();
            this.btnJoinRoom = new System.Windows.Forms.Button();
            this.btnHistory = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.ForeColor = System.Drawing.Color.DeepSkyBlue;
            this.lblTitle.Location = new System.Drawing.Point(286, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(108, 40);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "LOBBY";
            // 
            // lstPlayers
            // 
            this.lstPlayers.FormattingEnabled = true;
            this.lstPlayers.Location = new System.Drawing.Point(187, 52);
            this.lstPlayers.Name = "lstPlayers";
            this.lstPlayers.Size = new System.Drawing.Size(300, 212);
            this.lstPlayers.TabIndex = 1;
            // 
            // btnCreateRoom
            // 
            this.btnCreateRoom.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnCreateRoom.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCreateRoom.ForeColor = System.Drawing.Color.White;
            this.btnCreateRoom.Location = new System.Drawing.Point(308, 287);
            this.btnCreateRoom.Name = "btnCreateRoom";
            this.btnCreateRoom.Size = new System.Drawing.Size(75, 23);
            this.btnCreateRoom.TabIndex = 2;
            this.btnCreateRoom.Text = "Tạo phòng";
            this.btnCreateRoom.UseVisualStyleBackColor = false;
            this.btnCreateRoom.Click += new System.EventHandler(this.btnCreateRoom_Click);
            // 
            // btnJoinRoom
            // 
            this.btnJoinRoom.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnJoinRoom.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnJoinRoom.ForeColor = System.Drawing.Color.White;
            this.btnJoinRoom.Location = new System.Drawing.Point(308, 330);
            this.btnJoinRoom.Name = "btnJoinRoom";
            this.btnJoinRoom.Size = new System.Drawing.Size(75, 23);
            this.btnJoinRoom.TabIndex = 3;
            this.btnJoinRoom.Text = "Vào phòng";
            this.btnJoinRoom.UseVisualStyleBackColor = false;
            this.btnJoinRoom.Click += new System.EventHandler(this.btnJoinRoom_Click);
            // 
            // btnHistory
            // 
            this.btnHistory.BackColor = System.Drawing.Color.Gray;
            this.btnHistory.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnHistory.ForeColor = System.Drawing.Color.White;
            this.btnHistory.Location = new System.Drawing.Point(308, 370);
            this.btnHistory.Name = "btnHistory";
            this.btnHistory.Size = new System.Drawing.Size(75, 23);
            this.btnHistory.TabIndex = 4;
            this.btnHistory.Text = "Lịch sử";
            this.btnHistory.UseVisualStyleBackColor = false;
            this.btnHistory.Click += new System.EventHandler(this.btnHistory_Click);
            // 
            // LobbyForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.ClientSize = new System.Drawing.Size(684, 461);
            this.Controls.Add(this.btnHistory);
            this.Controls.Add(this.btnJoinRoom);
            this.Controls.Add(this.btnCreateRoom);
            this.Controls.Add(this.lstPlayers);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "LobbyForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Lobby";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.ListBox lstPlayers;
        private System.Windows.Forms.Button btnCreateRoom;
        private System.Windows.Forms.Button btnJoinRoom;
        private System.Windows.Forms.Button btnHistory;
    }
}