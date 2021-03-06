﻿using System;
using System.Windows.Forms;
using TaskLeader.BO;

namespace TaskLeader.GUI
{
    public partial class SaveLink : Form
    {
        public Link lien;

        public SaveLink()
        {
            InitializeComponent();
        }

        private void fileBut_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.pathBox.Text = openFileDialog.FileName;
            }
        }

        private void saveBut_Click(object sender, EventArgs e)
        {
            this.lien = new Link(titreBox.Text, pathBox.Text);
            this.DialogResult = DialogResult.OK;
        }

        private void pathBox_TextChanged(object sender, EventArgs e)
        {
            this.titreBox.Text = this.pathBox.Text.Substring(this.pathBox.Text.LastIndexOf(@"\") + 1);
        }
    }
}
