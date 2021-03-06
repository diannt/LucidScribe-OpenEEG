﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.IO.Ports;
using System.Windows.Forms;
using System.Drawing;

namespace lucidcode.LucidScribe.Plugin.OpenEEG
{
    public static class Device
    {
        static bool Initialized;
        static bool InitError;
        static SerialPort serialPort;
        static int[] eegChannels;
        static double eegValue;

        static double eegTicks;
        static bool clearRaw;

        static int[] buffer = new int[16];
        static int index = 100;
        static int lastByte = -1;
        static int channels = 2;
        public static string Algorithm = "REM Detection";
        public static int Threshold = 600;
        public static int BlinkInterval = 28;

        public static EventHandler<EventArgs> OpenEEGChanged;

        public static Boolean Initialize()
        {
            eegChannels = new int[channels];

            if (!Initialized & !InitError)
            {
                PortForm formPort = new PortForm();
                if (formPort.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Set the amount of channels
                        channels = formPort.Channels;
                        Algorithm = formPort.Algorithm;
                        BlinkInterval = formPort.BlinkInterval / 10;
                        Threshold = formPort.Threshold;

                        // Open the COM port
                        serialPort = new SerialPort(formPort.SelectedPort);
                        serialPort.BaudRate = 57600;
                        serialPort.Parity = Parity.None;
                        serialPort.DataBits = 8;
                        serialPort.StopBits = StopBits.One;
                        serialPort.Handshake = Handshake.None;
                        serialPort.ReadTimeout = 500;
                        serialPort.WriteTimeout = 500;
                        serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPort_DataReceived);
                        serialPort.Open();
                    }
                    catch (Exception ex)
                    {
                        if (!InitError)
                        {
                            MessageBox.Show(ex.Message, "LucidScribe.InitializePlugin()", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        InitError = true;
                    }
                }
                else
                {
                    InitError = true;
                    return false;
                }

                Initialized = true;
            }
            return true;
        }

        static void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                while (serialPort.BytesToRead > 0)
                {
                    int num = serialPort.ReadByte();
                    if ((lastByte == 165) && (num == 90))
                    {
                        index = 0;
                    }
                    if ((index >= 1) && (index < 16))
                    {
                        buffer[index - 1] = num;
                    }
                    if (index == 15)
                    {
                        int total = 0;
                        int activeChannels = 0;
                        for (int i = 0; i < channels; i++)
                        {
                            if (buffer.Length >= (i * 2) + 3)
                            {
                                eegChannels[i] = (buffer[(i * 2) + 2] * 256) + buffer[(i * 2) + 3];

                                if (eegChannels[i] > 0)
                                {
                                  activeChannels++;
                                  total += eegChannels[i];
                                }
                            }
                        }

                        int currentValue = 0;
                        if (activeChannels > 0)
                        {
                            currentValue = total / activeChannels;
                            if (clearRaw)
                            {
                              clearRaw = false;
                              eegValue = 0;
                              eegTicks = 0;
                            }
                            eegValue += currentValue;
                            eegTicks++;
                        }

                        if (OpenEEGChanged != null)
                        {
                          OpenEEGChanged((object)currentValue, null);
                        }
                    }

                    index++;
                    lastByte = num;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    serialPort.DataReceived -= serialPort_DataReceived;
                    serialPort.Close();
                }
                catch (Exception ex2)
                {
                }
                MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace, "OpenEEG.DataReceived()", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void Dispose()
        {
            if (Initialized)
            {
                Initialized = false;
            }
        }

        public static Double GetEEG()
        {
          if (eegTicks == 0) return 0;
          return (eegValue / eegTicks);
        }

        public static void ClearEEG()
        {
          clearRaw = true;
        }

        public static Double GetChannel1()
        {
            if (channels > 0)
            {
                return eegChannels[0];
            }
            return 0;
        }

        public static Double GetChannel2()
        {
            if (channels > 1)
            {
                return eegChannels[1];
            }
            return 0;
        }

        public static Double GetChannel3()
        {
            if (channels > 2)
            {
                return eegChannels[2];
            }
            return 0;
        }

        public static Double GetChannel4()
        {
            if (channels > 3)
            {
                return eegChannels[3];
            }
            return 0;
        }

        public static Double GetChannel5()
        {
            if (channels > 4)
            {
                return eegChannels[4];
            }
            return 0;
        }

        public static Double GetChannel6()
        {
            if (channels > 5)
            {
                return eegChannels[5];
            }
            return 0;
        }
    }

    namespace EEG
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetEEG();
                    Device.ClearEEG();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
    }

    namespace RAW
    {
      public class PluginHandler : lucidcode.LucidScribe.Interface.ILluminatedPlugin
      {

        private double m_dblValue = 256;

        public string Name
        {
          get
          {
            return "OpenEEG RAW";
          }
        }

        public bool Initialize()
        {
          try
          {
            bool initialized = Device.Initialize();
            Device.OpenEEGChanged += OpenEEGChanged;
            return initialized;
          }
          catch (Exception ex)
          {
            throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
          }
        }

        public event Interface.SenseHandler Sensed;
        public void OpenEEGChanged(object sender, EventArgs e)
        {
          if (ClearTicks)
          {
            ClearTicks = false;
            TickCount = "";
          }
          TickCount += sender + ",";

          if (ClearBuffer)
          {
            ClearBuffer = false;
            BufferData = "";
          }
          BufferData += sender + ",";
        }

        public void Dispose()
        {
          Device.OpenEEGChanged -= OpenEEGChanged;
          Device.Dispose();
        }

        public Boolean isEnabled = false;
        public Boolean Enabled
        {
          get
          {
            return isEnabled;
          }
          set
          {
            isEnabled = value;
          }
        }

        public Color PluginColor = Color.White;
        public Color Color
        {
          get
          {
            return Color;
          }
          set
          {
            Color = value;
          }
        }

        private Boolean ClearTicks = false;
        public String TickCount = "";
        public String Ticks
        {
          get
          {
            ClearTicks = true;
            return TickCount;
          }
          set
          {
            TickCount = value;
          }
        }

        private Boolean ClearBuffer = false;
        public String BufferData = "";
        public String Buffer
        {
          get
          {
            ClearBuffer = true;
            return BufferData;
          }
          set
          {
            BufferData = value;
          }
        }

        int lastHour;
        public int LastHour
        {
          get
          {
            return lastHour;
          }
          set
          {
            lastHour = value;
          }
        }
      }
    }

    namespace REM
    {
      public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
      {

        static int TicksSinceLastArtifact = 0;
        static int TicksAbove = 0;

        public override string Name
        {
          get
          {
            return "OpenREM";
          }
        }

        public override bool Initialize()
        {
          try
          {
            return Device.Initialize();
          }
          catch (Exception ex)
          {
            throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
          }
        }

        List<int> m_arrHistory = new List<int>();

        public override double Value
        {
          get
          {

            if (Device.Algorithm == "REM Detection")
            {
              // Update the mem list
              m_arrHistory.Add(Convert.ToInt32(Device.GetEEG()));
              if (m_arrHistory.Count > 512) { m_arrHistory.RemoveAt(0); }

              // Check for blinks
              int intBlinks = 0;
              bool boolBlinking = false;

              int intBelow = 0;
              int intAbove = 0;

              bool boolDreaming = false;
              foreach (Double dblValue in m_arrHistory)
              {
                if (dblValue > Device.Threshold)
                {
                  intAbove += 1;
                  intBelow = 0;
                }
                else
                {
                  intBelow += 1;
                  intAbove = 0;
                }

                if (!boolBlinking)
                {
                  if (intAbove >= 1)
                  {
                    boolBlinking = true;
                    intBlinks += 1;
                    intAbove = 0;
                    intBelow = 0;
                  }
                }
                else
                {
                  if (intBelow >= Device.BlinkInterval)
                  {
                    boolBlinking = false;
                    intBelow = 0;
                    intAbove = 0;
                  }
                  else
                  {
                    if (intAbove >= 12)
                    {
                      // reset
                      boolBlinking = false;
                      intBlinks = 0;
                      intBelow = 0;
                      intAbove = 0;
                    }
                  }
                }

                if (intBlinks > 6)
                {
                  boolDreaming = true;
                  break;
                }

                if (intAbove > 12)
                { // reset
                  boolBlinking = false;
                  intBlinks = 0;
                  intBelow = 0;
                  intAbove = 0; ;
                }
                if (intBelow > 80)
                { // reset
                  boolBlinking = false;
                  intBlinks = 0;
                  intBelow = 0;
                  intAbove = 0; ;
                }
              }

              if (boolDreaming)
              { return 888; }

              if (intBlinks > 10) { intBlinks = 10; }
              return intBlinks * 100;
            }
            else if (Device.Algorithm == "Motion Detection")
            {
                if (Device.GetEEG() > 980)
                {
                  TicksAbove++;
                  if (TicksAbove > 5)
                  {
                    TicksAbove = 0;
                    TicksSinceLastArtifact = 0;
                    if (TicksSinceLastArtifact > 19200)
                    {
                      return 888;
                    }
                  }
                }

              TicksSinceLastArtifact++;
              return 0;
            }

            return 0;
          }
        }

        public override void Dispose()
        {
          Device.Dispose();
        }
      }
    }

    namespace EEG1
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG Ch1";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetChannel1();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
    }

    namespace EEG2
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG Ch2";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetChannel2();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
    }

    namespace EEG3
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG Ch3";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetChannel3();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
    }

    namespace EEG4
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG Ch4";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetChannel4();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
    }

    namespace EEG5
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG Ch5";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetChannel5();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
    }

    namespace EEG6
    {
        public class PluginHandler : lucidcode.LucidScribe.Interface.LucidPluginBase
        {

            public override string Name
            {
                get
                {
                    return "OpenEEG Ch6";
                }
            }

            public override bool Initialize()
            {
                try
                {
                    return Device.Initialize();
                }
                catch (Exception ex)
                {
                    throw (new Exception("The '" + Name + "' plugin failed to initialize: " + ex.Message));
                }
            }

            public override double Value
            {
                get
                {
                    double tempValue = Device.GetChannel6();
                    if (tempValue > 999) { tempValue = 999; }
                    if (tempValue < 0) { tempValue = 0; }
                    return tempValue;
                }
            }

            public override void Dispose()
            {
                Device.Dispose();
            }
        }
      }

}
