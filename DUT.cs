using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.IO.Ports;
using System.Threading.Channels;

namespace RTL8720D_console
{
    class DUT
    {
        public static SerialPort serialPort = null;
        public static string buffer = "";
        public static bool bNORead = false;

        public static void SetAppendText(string text, ConsoleColor nColor)
        {
            Program.SetAppendText(text, nColor);
        }
        public static void SetAppendText(string text)
        {
            Program.SetAppendText(text);
        }
        public static void OnSerialDataReceived(Object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null) return;

                string str = serialPort.ReadExisting();

                if (str == null) return;

                buffer += str;


                //if (Global.testSetup.Debug == 1)
                //    Functions.writelog(str.Replace("\r", ""));
                //else if (Global.testSetup.Debug == 2)
                //    SetAppendText(str.Replace("\r", ""), ConsoleColor.DarkYellow);

                if (str.Contains("#") && !str.EndsWith("\n")) 
                    str += "\n"; 

                if (Global.testSetup.Debug == 1)
                    Functions.writelog(str.Replace("\r", ""));
                else if (Global.testSetup.Debug == 2)
                    SetAppendText(str.Replace("\r", ""), ConsoleColor.DarkYellow);
            }
            catch (Exception)
            {
                //if (Global.testSetup.Debug == 1)
                //    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + " [Error] " + ex.Message);
                //else if (Global.testSetup.Debug == 2)
                //    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + " [Error] " + ex.Message + "\r\n");
            }
        }

        public static bool open()
        {
            bool bResult = true;
            DateTime sDateTime = DateTime.Now;

            SetAppendText(++Global.iCount + ". OPEN_WIFI\r\n", ConsoleColor.Blue);

            try
            {
                serialPort = new SerialPort();
                serialPort.BaudRate = 115200;
                serialPort.PortName = Global.testSetup.COMPort;
                serialPort.ReadTimeout = 3000;
                serialPort.WriteTimeout = 3000;
                serialPort.Open();

                serialPort.DataReceived += new SerialDataReceivedEventHandler(OnSerialDataReceived);

                if (!(bResult = SendCmd("ATWP=1", "#"))) goto OpenEnd;
                if (!(bResult = SendCmd("iwpriv mp_start", "#"))) goto OpenEnd;//Enter WiFi MP mode
                if (!(bResult = SendCmd(string.Format("iwpriv mp_pwrctldm stop"), "#"))) goto OpenEnd;

            }
            catch (Exception ex)
            {
                SetAppendText("   [Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                bResult = false;
            }

        OpenEnd:

            TimeSpan time = DateTime.Now.Subtract(sDateTime);
            double dTime = Math.Round(time.TotalSeconds, 2);
            SetAppendText("   Test time: " + dTime + "sec\r\n\r\n");

            if (bResult)
                SetAppendText("   pass\r\n\r\n", ConsoleColor.Green);
            else
                SetAppendText("   fail\r\n\r\n", ConsoleColor.Red);

            return bResult;
        }
        public static bool close()
        {
            SetAppendText(++Global.iCount + ". CLOSE_WIFI\r\n", ConsoleColor.Blue);
            DateTime sDateTime = DateTime.Now;

            bool bResult = true;

            try
            {
                if (serialPort != null) serialPort.Close();
            }
            catch (Exception ex)
            {
                SetAppendText("   [Close Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                bResult = false;
            }

            TimeSpan time = DateTime.Now.Subtract(sDateTime);
            double dTime = Math.Round(time.TotalSeconds, 2);
            SetAppendText("   Test time: " + dTime + "sec\r\n\r\n");

            if (bResult)
                SetAppendText("   pass\r\n\r\n", ConsoleColor.Green);
            else
                SetAppendText("   fail\r\n\r\n", ConsoleColor.Red);


            return bResult;
        }
        public static bool open2()
        {
            bool bResult = true;

            try
            {
                serialPort = new SerialPort();
                serialPort.BaudRate = 115200;
                serialPort.PortName = Global.testSetup.COMPort;
                serialPort.ReadTimeout = 3000;
                serialPort.WriteTimeout = 3000;
                serialPort.Open();

                serialPort.DataReceived += new SerialDataReceivedEventHandler(OnSerialDataReceived);


                if (!(bResult = SendCmd("iwpriv mp_start", "#"))) return false;//Enter WiFi MP mode

            }
            catch (Exception ex)
            {
                SetAppendText("   [Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                bResult = false;
            }

            return bResult;
        }
        public static bool close2()
        {
            bool bResult = true;

            try
            {
                if (serialPort != null) serialPort.Close();
            }
            catch (Exception ex)
            {
                SetAppendText("   [Close Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                bResult = false;
            }

            return bResult;
        }
        public static bool wifiToBT()
        {
            bool bResult = true;

            try
            {
                serialPort = new SerialPort();
                serialPort.BaudRate = 115200;
                serialPort.PortName = Global.testSetup.COMPort;
                serialPort.ReadTimeout = 3000;
                serialPort.WriteTimeout = 3000;
                serialPort.Open();

                serialPort.DataReceived += new SerialDataReceivedEventHandler(OnSerialDataReceived);

                SetAppendText("   Enter BT mode...\r\n");
                if (!(bResult = DUT.SendCmd3("ATM2=bt_power,on", "success", "reopen"))) goto wifiToBTEnd;
                if (!(bResult = DUT.SendCmd("ATM2=gnt_bt,bt", "#"))) goto wifiToBTEnd;
                if (!(bResult = DUT.SendCmd("ATM2=bridge", "open"))) goto wifiToBTEnd;

                //Thread.Sleep(600);

                DUT.serialPort.Close();

                //Thread.Sleep(400);

                SetAppendText("   Enter BT mode OK\r\n");

            }
            catch (Exception ex)
            {
                SetAppendText("   [Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                bResult = false;
            }

        wifiToBTEnd:

            return bResult;
        }
        public static bool BTtoWifi()
        {
            bool bResult = false;
            try
            {
                serialPort = new SerialPort();
                serialPort.BaudRate = 115200;
                serialPort.PortName = Global.testSetup.COMPort;
                serialPort.ReadTimeout = 3000;
                serialPort.WriteTimeout = 3000;
                serialPort.Open();

                serialPort.DataReceived += new SerialDataReceivedEventHandler(OnSerialDataReceived);

                if (!(bResult = SendCmd("ATM2=bridge,close", "#"))) goto OpenEnd;

                //serialPort.Close();


                //serialPort = new SerialPort();
                //serialPort.BaudRate = 115200;
                //serialPort.PortName = Global.testSetup.COMPort;
                //serialPort.ReadTimeout = 3000;
                //serialPort.WriteTimeout = 3000;
                //serialPort.Open();

                //serialPort.DataReceived += new SerialDataReceivedEventHandler(OnSerialDataReceived);

                //if (!(bResult = SendCmd("", "#"))) goto OpenEnd;

                if (!(bResult = SendCmd("ATM2=gnt_bt,wifi", "#"))) goto OpenEnd;

                serialPort.Close();
            }
            catch (Exception ex)
            {
                SetAppendText("   Error: " + ex.Message + "\r\n", ConsoleColor.Red);
                bResult = false;
            }

        OpenEnd:

            return bResult;
        }
        public static bool changeStatus()
        {
            if (Global.testSetup.SwitchtoNormal == 1)
            {
                SetAppendText("   switch to normal mode...");
            }
            else
            {
                SetAppendText("   skip switch to normal mode...");
                return true;
            }

            bool bResult = true;

            if (!(bResult = DUT.open2())) goto changeStatusEnd;

            {
                if (!(bResult = SendCmd("ATSC", "#"))) goto changeStatusEnd;
                for (int i = 0; i < 10; i++)
                {
                    if (buffer.Contains(Global.testSetup.CheckATSC)) break;
                    Thread.Sleep(1000);

                }
                if (!buffer.Contains(Global.testSetup.CheckATSC))
                {
                    SetAppendText("================================");
                    SetAppendText(Global.testSetup.CheckATSC);
                    SetAppendText("================================");
                    SetAppendText(buffer);
                    SetAppendText("================================");

                    bResult = false;
                    goto changeStatusEnd;
                }
            }

            if (Global.testSetup.Reboot == 1)
            {
                if (!(bResult = SendCmd("REBOOT", "Available"))) goto changeStatusEnd;
                for (int i = 0; i < 10; i++)
                {
                    if (buffer.Contains(Global.testSetup.CheckStatus)) break;
                    Thread.Sleep(1000);

                }
                if (!buffer.Contains(Global.testSetup.CheckStatus))
                {
                    SetAppendText("================================");
                    SetAppendText(Global.testSetup.CheckStatus);
                    SetAppendText("================================");
                    SetAppendText(buffer);
                    SetAppendText("================================");

                    bResult = false;
                    goto changeStatusEnd;
                }
            }

            if (Global.testSetup.ATST == 1)
            {
                if (!(bResult = SendCmd("ATST", Global.testSetup.CheckStatus)))
                {
                    SetAppendText("================================");
                    SetAppendText(Global.testSetup.CheckStatus);
                    SetAppendText("================================");
                    SetAppendText(buffer);
                    SetAppendText("================================");

                    goto changeStatusEnd;
                }
            }

            DUT.close2();

        changeStatusEnd:

            if (bResult)
                SetAppendText("   switch to normal mode pass\r\n");
            else
                SetAppendText("   switch to normal mode fail\r\n", ConsoleColor.Red);

            return bResult;
        }
        public static bool txStartCal(int channel, string datarate, int xcap, int gain)
        {
            bool bResult = true;

            int bandwidth = 0;
            string rate = "";
            try 
            {
                if (!(bResult = Functions.getDataRate(datarate, ref bandwidth, ref rate))) goto txStartEnd;


                {
                    if (!(bResult = SendCmd("iwpriv mp_ant_tx a", "#"))) goto txStartEnd;
                    Global.dutSetup.channel = channel;
                }

                //if (Global.dutSetup.channel != channel)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_channel {0}", channel), "#"))) goto txStartEnd;
                    Global.dutSetup.channel = channel;
                }
                //if (Global.dutSetup.bandwidth != bandwidth)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_bandwidth 40M={0},shortGI=0", bandwidth), "#"))) goto txStartEnd;
                    Global.dutSetup.bandwidth = bandwidth;
                }
                //if (Global.dutSetup.rate != rate)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_rate {0}", rate), "#"))) goto txStartEnd;
                    Global.dutSetup.rate = rate;
                }
                //if (Global.dutSetup.gain != gain)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_txpower patha={0:D},pathb=0", gain), "#"))) goto txStartEnd;
                    Global.dutSetup.gain = gain;
                }

                //if (Global.dutSetup.cap != xcap)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_phypara xcap={0:D}", xcap), "#"))) goto txStartEnd;
                    Global.dutSetup.cap = xcap;
                }
                if (!(bResult = SendCmd("iwpriv mp_ctx background,pkt", "#"))) goto txStartEnd;
            }
            catch (Exception E)
            {
                SetAppendText("txStartCal Fail!!!\r\n" + E.Message, ConsoleColor.Red);
                bResult = false;
            }
            


            txStartEnd:

            return bResult;
        }
        public static bool SetTxGain(int gain)
        {
            bool bResult = SendCmd(string.Format("iwpriv mp_txpower patha={0:D},pathb=0", gain), "#");

            return bResult;
        }
        public static bool SetTxCap(int cap)
        {
            bool bResult = SendCmd(string.Format("iwpriv mp_phypara xcap={0:D}", cap), "#");

            return bResult;
        }
        public static bool txStop()
        {
            bool bResult = SendCmd("iwpriv mp_ctx stop", "#");

            return bResult;
        }
        public static bool writefakevalue(int addr, int value)
        {
            bool bResult = SendCmd(string.Format("/tmp/rtwpriv wlan0 efuse_set wlwfake,{0:X02},{1:X02}", addr, value), "#");

            return bResult;
        }
        public static bool writerealvalue(int addr, int value)
        {
            bool bResult = SendCmd(string.Format("/tmp/rtwpriv wlan0 efuse_set wmap,{0:X02},{1:X02}", addr, value), "#");

            return bResult;
        }
        public static int readrealvalue(int addr)
        {
            SendCmd(string.Format("/tmp/rtwpriv wlan0 efuse_get rmap,{0:X02},1", addr), "#");

            string str = buffer.Substring(buffer.IndexOf(":") + 1);
            char[] sSeparator = { '=', '\r', '\n', '\t', ':', '/' };
            string[] sSplit = str.Split(sSeparator, StringSplitOptions.RemoveEmptyEntries);
            return Convert.ToInt32(sSplit[0].Trim(), 16);
        }
        public static string readrealvalue(int addr, int len)
        {
            SendCmd(string.Format("/tmp/rtwpriv wlan0 efuse_get rmap,0x{0:X02},{1:D}", addr, len), "#");

            string str = buffer.Substring(buffer.IndexOf(":") + 1);
            char[] sSeparator = { '=', '\r', '\n', '\t', ':', '/' };
            string[] sSplit = str.Split(sSeparator, StringSplitOptions.RemoveEmptyEntries);
            return sSplit[0].Replace(" ", "").Replace("0x", "");
        }
        public static bool updatefakevalue()
        {
            bool bResult = SendCmd("/tmp/rtwpriv wlan0 efuse_set update,fake", "#");

            //SendCmd("/tmp/rtwpriv wlan0 efuse_get wlrfkmap", "#");
            //Thread.Sleep(50);
            //SendCmd("/tmp/rtwpriv wlan0 efuse_get wlrfkmap", "#");

            return bResult;
        }
        public static void getThermalValue()
        {
            try
            {
                SendCmd("iwpriv mp_ther", "#");
                string str = buffer.Substring(buffer.IndexOf(":") + 1);
                char[] sSeparator = { '=', '\r', '\n', '\t', ':', '/', '[', ']' };
                string[] sSplit = str.Split(sSeparator, StringSplitOptions.RemoveEmptyEntries);
                Global.thermal = Convert.ToInt32(sSplit[0]);

                SetAppendText("   Thermal [0xCA] : " + Global.thermal + "\r\n");
            }
            catch (Exception ex)
            {
                SetAppendText("   getThermalValue Error: " + ex.Message + "\r\n", ConsoleColor.Red);
            }
        }
        public static bool rxStart(int channel, string datarate)
        {
            bool bResult = true;

            int bandwidth = 0;
            string rate = "";
            try
            {
                if (!(bResult = Functions.getDataRate(datarate, ref bandwidth, ref rate))) goto rxStartCal;

                //if (Global.dutSetup.channel != channel)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_channel {0}", channel), "#"))) goto rxStartCal;
                    Global.dutSetup.channel = channel;
                }
                //if (Global.dutSetup.bandwidth != bandwidth)
                {
                    if (!(bResult = SendCmd(string.Format("iwpriv mp_bandwidth 40M={0},shortGI=0", bandwidth), "#"))) goto rxStartCal;
                    Global.dutSetup.bandwidth = bandwidth;
                }

                SendCmd("iwpriv mp_arx start", "#");
                SendCmd("iwpriv mp_reset_stats", "#");
            }
            catch (Exception E)
            {
                SetAppendText("rxStart Fail!!!\r\n" + E.Message, ConsoleColor.Red);
                return false;
            }
            

        rxStartCal:
            return bResult;
        }
        public static int getRXPacket()
        {
            try
            {
                int num = 0;
                SendCmd("iwpriv mp_arx phy", "#");

                string str = buffer;

                char[] sSeparator = { '=', '\r', '\n', '\t', ':', ' ' };
                string[] sSplit = str.Split(sSeparator);
                for (int i = 0; i < sSplit.Length; i++)
                {
                    if (sSplit[i].Contains("OK"))
                    {
                        num = Convert.ToInt32(sSplit[i + 1]);
                        break;
                    }
                }

                SendCmd("iwpriv mp_arx stop", "#");

                return num;
            }
            catch (Exception ex)
            {
                SetAppendText("   [Error]: " + ex.Message, ConsoleColor.Red);
                SetAppendText("[" + buffer + "]", ConsoleColor.Red);
                return 0;
            }
        }
        public static bool SendCmd(string sCmd, string prompt)
        {
            bNORead = false;
            try
            {
                if (Global.testSetup.Debug == 1)
                {
                    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Send: " + sCmd.Trim() + "\r\n");
                    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Read: ");
                }
                else if (Global.testSetup.Debug == 2)
                {
                    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Send: " + sCmd.Trim() + "\r\n", ConsoleColor.DarkMagenta);
                    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Read: ", ConsoleColor.DarkCyan);
                }

                buffer = "";

                serialPort.Write(sCmd + "\r\n");


                Thread.Sleep(50);


                bool bFind = false;

                for (int i = 0; i < 100; i++)
                {
                    if (buffer.Contains(prompt))
                    {
                        bFind = true;
                        break;
                    }
                    else
                        Thread.Sleep(100);
                }

                return bFind;

            }
            catch (Exception ex)
            {
                SetAppendText("   [SendCmd Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                return false;
            }

        }
        public static bool SendCmd2(string sCmd, string prompt)
        {
            bNORead = true;
            try
            {
                if (Global.testSetup.Debug == 1)
                {
                    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Send: " + sCmd.Trim() + "\r\n");
                    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Read: ");
                }
                else if (Global.testSetup.Debug == 2)
                {
                    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Send: " + sCmd.Trim() + "\r\n", ConsoleColor.DarkMagenta);
                    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Read: ", ConsoleColor.DarkCyan);
                }

                buffer = "";

                serialPort.Write(sCmd + "\r\n");


                Thread.Sleep(100);


                //buffer2 = serialPort.ReadExisting();

                if (buffer.Contains(prompt))
                    return true;
                else
                    return false;

            }
            catch (Exception ex)
            {
                SetAppendText("   [SendCmd2 Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                return false;
            }

        }
        public static bool SendCmd3(string sCmd, string prompt1, string prompt2)
        {
            bNORead = false;
            try
            {
                if (Global.testSetup.Debug == 1)
                {
                    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Send: " + sCmd.Trim() + "\r\n");
                    Functions.writelog(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Read: ");
                }
                else if (Global.testSetup.Debug == 2)
                {
                    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Send: " + sCmd.Trim() + "\r\n", ConsoleColor.DarkMagenta);
                    SetAppendText(DateTime.Now.ToString("yyyyMMdd HH:mm:ss fff") + "Read: ", ConsoleColor.DarkCyan);
                }

                buffer = "";

                serialPort.Write(sCmd + "\r\n");


                Thread.Sleep(50);


                bool bFind = false;

                for (int i = 0; i < 100; i++)
                {
                    if (buffer.Contains(prompt1))
                    {
                        bFind = true;
                        break;
                    }
                    else if (buffer.Contains(prompt2))
                    {
                        if (buffer.Contains("#"))
                        {
                            bFind = true;
                            break;
                        }
                    }
                    else
                        Thread.Sleep(100);
                }

                return bFind;

            }
            catch (Exception ex)
            {
                SetAppendText("   [SendCmd Error]: " + ex.Message + "\r\n", ConsoleColor.Red);
                return false;
            }

        }
    }
}
