/*
 * SetupDialogForm.cs
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.AutomatedDustCover;
using ASCOM.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ASCOM.AutomatedDustCover
{
    [ComVisible(false)]					// Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        TraceLogger tl; // Holder for a reference to the driver's trace logger

        public SetupDialogForm(TraceLogger tlDriver)
        {
            InitializeComponent();

            // Save the provided trace logger for use within the setup dialogue
            tl = tlDriver;

            // Initialize current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            // Place any validation constraint checks here
            // Update the state variables with results from the dialogue
            Switch.comPort = (string)comboBoxComPort.SelectedItem;
            tl.Enabled = chkTrace.Checked;
        }

        private void cmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("https://ascom-standards.org/");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        private void InitUI()
        {
            chkTrace.Checked = tl.Enabled;
            // set the list of com ports to those that are currently available
            comboBoxComPort.Items.Clear();
            // use System.IO because it's static
            comboBoxComPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            // select the current port if possible
            if (comboBoxComPort.Items.Contains(Switch.comPort))
            {
                comboBoxComPort.SelectedItem = Switch.comPort;
            }
        }
    }
}