using System.Drawing;
using System.Windows.Forms;

namespace UOX3SpawnEditor
{
    partial class HelpForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label labelTitle;
        private RichTextBox richTextBoxHelp;
        private Button buttonClose;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelTitle = new Label();
            this.richTextBoxHelp = new RichTextBox();
            this.buttonClose = new Button();
            this.SuspendLayout();

            // labelTitle
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            this.labelTitle.Location = new Point(12, 9);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new Size(180, 21);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "UOX3 Spawn Editor Help";

            // richTextBoxHelp
            this.richTextBoxHelp.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.richTextBoxHelp.BorderStyle = BorderStyle.FixedSingle;
            this.richTextBoxHelp.Location = new Point(12, 40);
            this.richTextBoxHelp.Name = "richTextBoxHelp";
            this.richTextBoxHelp.ReadOnly = true;
            this.richTextBoxHelp.ScrollBars = RichTextBoxScrollBars.Vertical;
            this.richTextBoxHelp.Size = new Size(760, 468);
            this.richTextBoxHelp.TabIndex = 1;
            this.richTextBoxHelp.Text = "";

            // buttonClose
            this.buttonClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.buttonClose.Location = new Point(697, 518);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new Size(75, 28);
            this.buttonClose.TabIndex = 2;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);

            // HelpForm
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(784, 558);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.richTextBoxHelp);
            this.Controls.Add(this.labelTitle);
            this.KeyPreview = true;
            this.MinimumSize = new Size(700, 500);
            this.Name = "HelpForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "How to Use";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}