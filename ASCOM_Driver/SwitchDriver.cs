/*
 * SwitchDriver.cs
 * Copyright (C) 2022 - Present, Julien Lecomte - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.DeviceInterface;
using ASCOM.Utilities;

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace ASCOM.AutomatedDustCover
{
    //
    // Your driver's DeviceID is ASCOM.AutomatedDustCover.Switch
    //
    // The Guid attribute sets the CLSID for ASCOM.AutomatedDustCover.Switch
    // The ClassInterface/None attribute prevents an empty interface called
    // _AutomatedDustCover from being created and used as the [default] interface
    //

    /// <summary>
    /// ASCOM Switch Driver for AutomatedDustCover.
    /// </summary>
    [Guid("a8047099-516a-43f4-bf01-c714f2d144b4")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Switch : ISwitchV2
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        internal const string driverID = "ASCOM.AutomatedDustCover.Switch";

        /// <summary>
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
        private const string driverDescription = "ASCOM Switch Driver for Automated Dust Cover";

        // Constants used for Profile persistence
        internal static readonly string comPortProfileName = "COM Port";
        internal static readonly string comPortDefault = "COM1";
        internal static readonly string traceStateProfileName = "Trace Level";
        internal static readonly string traceStateDefault = "false";

        // Variables to hold the current device configuration
        internal static string comPort;

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        internal TraceLogger tl;

        // The object used to communicate with the device using serial port communication.
        private Serial objSerial;

        // Constants used to communicate with the device
        private const string COMMAND_PING = "COMMAND:PING";
        private const string COMMAND_GETSTATE = "COMMAND:GETSTATE";
        private const string COMMAND_OPEN = "COMMAND:OPEN";
        private const string COMMAND_CLOSE = "COMMAND:CLOSE";

        private const string RESULT_PING = "RESULT:OK";
        private const string RESULT_STATE_OPEN = "RESULT:STATE:OPEN";
        private const string RESULT_STATE_CLOSED = "RESULT:STATE:CLOSED";

        private const string SEPARATOR = "\n";

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomatedDustCover"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Switch()
        {
            tl = new TraceLogger("", "AutomatedDustCover");
            ReadProfile();
            tl.LogMessage("Switch", "Starting initialisation");
            connectedState = false;
            tl.LogMessage("Switch", "Completed initialisation");
        }

        //
        // PUBLIC COM INTERFACE ISwitchV2 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm(tl))
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    // Persist device configuration values to the ASCOM Profile store
                    WriteProfile();
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            LogMessage("", "Action {0}, parameters {1} not implemented", actionName, actionParameters);
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");

            // The optional CommandBlind method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBlind must send the supplied command to the device and return immediately without waiting for a response

            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");

            // The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBool must send the supplied command to the device, wait for a response and parse this to return a True or False value

            // string retString = CommandString(command, raw); // Send the command and wait for the response
            // bool retBool = XXXXXXXXXXXXX; // Parse the returned string and create a boolean True / False value
            // return retBool; // Return the boolean value to the client

            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");

            // The optional CommandString method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandString must send the supplied command to the device and wait for a response before returning this to the client

            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            // Clean up the trace logger object
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
        }

        public bool Connected
        {
            get
            {
                LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set
            {
                tl.LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected)
                    return;

                if (value)
                {
                    if (!System.IO.Ports.SerialPort.GetPortNames().Contains(comPort))
                    {
                        throw new InvalidValueException("Invalid COM port", comPort.ToString(), String.Join(", ", System.IO.Ports.SerialPort.GetPortNames()));
                    }

                    LogMessage("Connected Set", "Connecting to port {0}", comPort);

                    objSerial = new Serial
                    {
                        Speed = SerialSpeed.ps57600,
                        PortName = comPort,
                        Connected = true
                    };

                    // Wait a second for the serial connection to establish
                    System.Threading.Thread.Sleep(1000);

                    objSerial.ClearBuffers();

                    // Poll the device (with a short timeout value) until successful,
                    // or until we've reached the retry count limit...
                    objSerial.ReceiveTimeout = 1;
                    bool success = false;
                    for (int retries = 10; retries >= 0; retries--)
                    {
                        objSerial.Transmit(COMMAND_PING + SEPARATOR);
                        string response = "";
                        try
                        {
                            response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
                        }
                        catch (Exception)
                        {
                            // Timeout exceptions will likely happen here!
                            // We ignore them.
                        }
                        if (response == RESULT_PING)
                        {
                            success = true;
                            break;
                        }
                    }

                    if (!success)
                    {
                        objSerial.Connected = false;
                        objSerial.Dispose();
                        objSerial = null;
                        throw new ASCOM.NotConnectedException("Failed to connect");
                    }

                    // Restore default timeout value...
                    objSerial.ReceiveTimeout = 5;

                    connectedState = true;
                }
                else
                {
                    connectedState = false;

                    LogMessage("Connected Set", "Disconnecting from port {0}", comPort);

                    objSerial.Connected = false;
                    objSerial.Dispose();
                    objSerial = null;
                }
            }
        }

        public string Description
        {
            get
            {
                tl.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = "Automated Dust Cover Driver Version " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                LogMessage("InterfaceVersion Get", "2");
                return Convert.ToInt16("2");
            }
        }

        public string Name
        {
            get
            {
                string name = "Automated Dust Cover";
                tl.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ISwitchV2 Implementation

        private short numSwitch = 1;

        /// <summary>
        /// The number of switches managed by this driver
        /// </summary>
        public short MaxSwitch
        {
            get
            {
                tl.LogMessage("MaxSwitch Get", numSwitch.ToString());
                return this.numSwitch;
            }
        }

        /// <summary>
        /// Return the name of switch n
        /// </summary>
        /// <param name="id">The switch number to return</param>
        /// <returns>
        /// The name of the switch
        /// </returns>
        public string GetSwitchName(short id)
        {
            Validate("GetSwitchName", id);
            tl.LogMessage("GetSwitchName", $"GetSwitchName({id})");
            return "Automated Dust Cover";
        }

        /// <summary>
        /// Sets a switch name to a specified value
        /// </summary>
        /// <param name="id">The number of the switch whose name is to be set</param>
        /// <param name="name">The name of the switch</param>
        public void SetSwitchName(short id, string name)
        {
            Validate("SetSwitchName", id);
            tl.LogMessage("SetSwitchName", $"SetSwitchName({id}) = {name} - not implemented");
            throw new MethodNotImplementedException("SetSwitchName");
        }

        /// <summary>
        /// Gets the description of the specified switch. This is to allow a fuller description of the switch to be returned, for example for a tool tip.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <exception cref="InvalidValueException">If id is outside the range 0 to MaxSwitch - 1</exception>
        /// <returns></returns>
        public string GetSwitchDescription(short id)
        {
            Validate("GetSwitchDescription", id);
            tl.LogMessage("GetSwitchDescription", $"GetSwitchDescription({id}) - not implemented");
            throw new MethodNotImplementedException("GetSwitchDescription");
        }

        /// <summary>
        /// Reports whether the specified switch can be written to.
        /// Returns false if the switch cannot be written to, for example a limit switch or a sensor.
        /// </summary>
        /// <param name="id">The number of the switch whose write state is to be returned</param>
        /// <returns>
        /// <c>true</c> if the switch can be written to, otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="MethodNotImplementedException">If the method is not implemented</exception>
        /// <exception cref="InvalidValueException">If id is outside the range 0 to MaxSwitch - 1</exception>
        public bool CanWrite(short id)
        {
            bool writable = true;
            Validate("CanWrite", id);
            // default behavour is to report true
            tl.LogMessage("CanWrite", $"CanWrite({id}): {writable}");
            return true;
        }

        #region Boolean switch members

        /// <summary>
        /// Return the state of switch n.
        /// A multi-value switch must throw a not implemented exception
        /// </summary>
        /// <param name="id">The switch number to return</param>
        /// <returns>
        /// True or false
        /// </returns>
        public bool GetSwitch(short id)
        {
            Validate("GetSwitch", id);
            tl.LogMessage("GetSwitch", $"GetSwitch({id})");
            return QueryDeviceState();
        }

        /// <summary>
        /// Sets a switch to the specified state
        /// If the switch cannot be set then throws a MethodNotImplementedException.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="state"></param>
        public void SetSwitch(short id, bool state)
        {
            Validate("SetSwitch", id);
            if (!CanWrite(id))
            {
                var str = $"SetSwitch({id}) - Cannot Write";
                tl.LogMessage("SetSwitch", str);
                throw new MethodNotImplementedException(str);
            }
            tl.LogMessage("SetSwitch", $"SetSwitch({id}) = {state}");
            if (state)
                OpenDustCover();
            else
                CloseDustCover();
        }

        #endregion

        #region Analogue members

        /// <summary>
        /// Returns the maximum value for this switch
        /// Boolean switches must return 1.0
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public double MaxSwitchValue(short id)
        {
            Validate("MaxSwitchValue", id);
            tl.LogMessage("MaxSwitchValue", $"MaxSwitchValue({id})");
            return 1.0;
        }

        /// <summary>
        /// Returns the minimum value for this switch
        /// Boolean switches must return 0.0
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public double MinSwitchValue(short id)
        {
            Validate("MinSwitchValue", id);
            tl.LogMessage("MinSwitchValue", $"MinSwitchValue({id})");
            return 0.0;
        }

        /// <summary>
        /// Returns the step size that this switch supports. This gives the difference between successive values of the switch.
        /// The number of values is ((MaxSwitchValue - MinSwitchValue) / SwitchStep) + 1
        /// boolean switches must return 1.0, giving two states.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public double SwitchStep(short id)
        {
            Validate("SwitchStep", id);
            tl.LogMessage("SwitchStep", $"SwitchStep({id})");
            return 1.0;
        }

        /// <summary>
        /// Returns the analogue switch value for switch id.
        /// Boolean switches must return either 0.0 (false) or 1.0 (true).
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public double GetSwitchValue(short id)
        {
            Validate("GetSwitchValue", id);
            tl.LogMessage("GetSwitchValue", $"GetSwitchValue({id}) - not implemented");
            return QueryDeviceState() ? 1.0 : 0.0;
        }

        /// <summary>
        /// Set the analogue value for this switch.
        /// A MethodNotImplementedException should be thrown if CanWrite returns False
        /// If the value is not between the maximum and minimum then throws an InvalidValueException
        /// </summary>
        /// <param name="id"></param>
        /// <param name="value"></param>
        public void SetSwitchValue(short id, double value)
        {
            Validate("SetSwitchValue", id, value);
            if (!CanWrite(id))
            {
                tl.LogMessage("SetSwitchValue", $"SetSwitchValue({id}) - Cannot write");
                throw new ASCOM.MethodNotImplementedException($"SetSwitchValue({id}) - Cannot write");
            }
            tl.LogMessage("SetSwitchValue", $"SetSwitchValue({id}) = {value}");
            if (value == 1.0)
                OpenDustCover();
            else
                CloseDustCover();
        }

        #endregion

        #endregion

        #region Private methods

        /// <summary>
        /// Checks that the switch id is in range and throws an InvalidValueException if it isn't
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The id.</param>
        private void Validate(string message, short id)
        {
            if (id < 0 || id >= numSwitch)
            {
                tl.LogMessage(message, string.Format("Switch {0} not available, range is 0 to {1}", id, numSwitch - 1));
                throw new InvalidValueException(message, id.ToString(), string.Format("0 to {0}", numSwitch - 1));
            }
        }

        /// <summary>
        /// Checks that the switch id and value are in range and throws an
        /// InvalidValueException if they are not.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The id.</param>
        /// <param name="value">The value.</param>
        private void Validate(string message, short id, double value)
        {
            Validate(message, id);
            var min = MinSwitchValue(id);
            var max = MaxSwitchValue(id);
            if (value < min || value > max)
            {
                tl.LogMessage(message, string.Format("Value {1} for Switch {0} is out of the allowed range {2} to {3}", id, value, min, max));
                throw new InvalidValueException(message, value.ToString(), string.Format("Switch({0}) range {1} to {2}", id, min, max));
            }
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered.
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Switch";
                if (bRegister)
                {
                    P.Register(driverID, driverDescription);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Switch";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Switch";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString());
            }
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }

        /// <summary>
        /// Sends a COMMAND:GETSTATE command to the device and return its value as a boolean
        /// </summary>
        private bool QueryDeviceState()
        {
            tl.LogMessage("QueryDeviceState", "Sending request to device...");
            objSerial.Transmit(COMMAND_GETSTATE + SEPARATOR);
            tl.LogMessage("QueryDeviceState", "Waiting for response from device...");
            string response;
            try
            {
                response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
            }
            catch (Exception e)
            {
                tl.LogMessage("QueryDeviceState", "Exception: " + e.Message);
                throw e;
            }
            tl.LogMessage("QueryDeviceState", "Response from device: " + response);
            if (response == RESULT_STATE_OPEN)
                return true;
            if (response == RESULT_STATE_CLOSED)
                return false;
            tl.LogMessage("QueryDeviceState", "Invalid response from device: " + response);
            throw new ASCOM.DriverException("Invalid response from device: " + response);
        }

        /// <summary>
        /// Sends a COMMAND:OPEN command to the device and wait until it responds
        /// </summary>
        private void OpenDustCover()
        {
            tl.LogMessage("OpenDustCover", "Sending request to device...");
            objSerial.Transmit(COMMAND_OPEN + SEPARATOR);
            tl.LogMessage("OpenDustCover", "Waiting for response from device...");
            string response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
            tl.LogMessage("OpenDustCover", "Response from device: " + response);
            if (response != RESULT_STATE_OPEN)
            {
                tl.LogMessage("OpenDustCover", "Invalid response from device: " + response);
                throw new ASCOM.DriverException("Invalid response from device: " + response);
            }
        }

        /// <summary>
        /// Sends a COMMAND:CLOSE command to the device and wait until it responds
        /// </summary>
        private void CloseDustCover()
        {
            tl.LogMessage("CloseDustCover", "Sending request to device...");
            objSerial.Transmit(COMMAND_CLOSE + SEPARATOR);
            tl.LogMessage("CloseDustCover", "Waiting for response from device...");
            string response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
            tl.LogMessage("CloseDustCover", "Response from device: " + response);
            if (response != RESULT_STATE_CLOSED)
            {
                tl.LogMessage("CloseDustCover", "Invalid response from device: " + response);
                throw new ASCOM.DriverException("Invalid response from device: " + response);
            }
        }

        #endregion
    }
}
