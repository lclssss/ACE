using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

namespace RTL8720D_console
{
    class Functions
    {
        [DllImport("Kernel32.dll")]
        public static extern int GetPrivateProfileString(
            [MarshalAs(UnmanagedType.LPStr)]String lpAppName,
            [MarshalAs(UnmanagedType.LPStr)]String lpKeyName,
            [MarshalAs(UnmanagedType.LPStr)]String lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            [MarshalAs(UnmanagedType.LPStr)]String lpFileName
            );
        [DllImport("Kernel32.dll")]
        public static extern int WritePrivateProfileString(
      [MarshalAs(UnmanagedType.LPStr)] String lpAppName,
      [MarshalAs(UnmanagedType.LPStr)] String lpKeyName,
      [MarshalAs(UnmanagedType.LPStr)] String lpString,
      [MarshalAs(UnmanagedType.LPStr)] String lpFileName
      );
        //补码
        public static int ConvertToComplementCode(int OriginalCode)
        {
            if (OriginalCode >= 0) 
                return OriginalCode;
            int a = 127;
            int b = -128;
            int c = a - b;
            int d = c + OriginalCode + 1;

            return d;
        }
        //源码
        public static int ConvertToOriginalCode(int ComplementCode)
        {
            if (ComplementCode < 128) return ComplementCode;

            int a = 127;
            int b = -128;
            int c = a - b;
            int d = ComplementCode - c - 1;

            return d;
        }
        //校验
        public static Byte GetCheckSum(Byte[] bytes)
        {
            Byte checksum = 0x00;

            foreach (byte bt in bytes)
            {
                checksum ^= bt;
            }

            return checksum;
        }
        public static bool parsePathLoss()
        {
            try
            {
                Global.lossListTX = new List<TEST_LOSS>[2];
                Global.lossListRX = new List<TEST_LOSS>[2];

                for(int i = 0;i<2;i++)
                {
                    Global.lossListTX[i] = new List<TEST_LOSS>();
                    Global.lossListRX[i] = new List<TEST_LOSS>();
                }


                string checkSUM = "", stationName = "", calDate = "";
                string testerSN = "", testerFW = "", testerPort = "";
                string[] lines = File.ReadAllLines("Setup\\pathloss.csv");
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] sSplit = lines[i].Split(',');
                    if (lines[i].Contains("Checksum")) checkSUM = sSplit[1];
                    if (lines[i].Contains("Station Name")) stationName = sSplit[1];
                    if (lines[i].Contains("TESTER_CALIB_DATE")) calDate = sSplit[1];
                    if (lines[i].Contains("LitePoint"))
                    {
                        TEST_LOSS loss = new TEST_LOSS();
                        loss.eqCompany = sSplit[0];
                        loss.eqModel = sSplit[1];
                        loss.eqSN = sSplit[2];
                        loss.eqFW = sSplit[3];
                        loss.rfPort = sSplit[4];
                        loss.chain = sSplit[5];
                        loss.direction = sSplit[6];
                        loss.frequency = Convert.ToInt32(sSplit[7]);
                        loss.attenuation = Convert.ToDouble(sSplit[8]);

                        int chain = 0;
                        if (loss.chain == "A") chain = 0;
                        if (loss.chain == "B") chain = 1;

                        if (loss.direction == "TX")
                            Global.lossListTX[chain].Add(loss);
                        else
                            Global.lossListRX[chain].Add(loss);

                        testerSN = loss.eqSN;
                        testerFW = loss.eqFW;
                        testerPort = loss.rfPort;
                    }
                }

                string sConten = File.ReadAllText("Setup\\pathloss.csv");
                sConten = sConten.Replace("Checksum," + checkSUM + "\r\n", "");
                byte[] decBytes = Encoding.UTF8.GetBytes(sConten);
                Byte sum = GetCheckSum(decBytes);
                if (Global.testSetup.CheckSum)
                {
                    if (sum.ToString() != checkSUM)
                    {
                        SetAppendText(String.Format("   Test sum {0} is different from loss sum {1}!\r\n", sum.ToString(), checkSUM), ConsoleColor.Red);
                        return false;
                    }
                    if (Global.testerSN != testerSN)
                    {
                        SetAppendText(String.Format("   Test sn {0} is different from loss sn {1}!\r\n", Global.testerSN, testerSN), ConsoleColor.Red);
                        return false;
                    }
                    if (Global.rfport.Replace("RF", "") != testerPort)
                    {
                        SetAppendText(String.Format("   Test rfport {0} is different from loss rfport {1}!\r\n", Global.rfport, testerPort), ConsoleColor.Red);
                        return false;
                    }
                    if (Global.testerFW != testerFW)
                    {
                        SetAppendText(String.Format("   Test FW {0} is different from loss FW {1}!\r\n", Global.testerFW, testerFW), ConsoleColor.Red);
                        return false;
                    }
                    if (System.Environment.MachineName != stationName)
                    {
                        SetAppendText(String.Format("   Test station {0} is different from loss station {1}!\r\n", System.Environment.MachineName, stationName), ConsoleColor.Red);
                        return false;
                    }
                    double dateNum = DateTime.Now.Subtract(DateTime.Parse(calDate)).TotalDays;
                    if (dateNum > Global.testSetup.CABLE_LOSS_DATE)
                    {
                        SetAppendText(String.Format("   Test cable loss date {0} is longer than {1}!\r\n", calDate, Global.testSetup.CABLE_LOSS_DATE), ConsoleColor.Red);
                        return false;
                    }
                }
                
                return true;
            }
            catch(Exception ex)
            {
                SetAppendText("   " + ex.Message + "\r\n", ConsoleColor.Red);
                return false;
            }
        }
        public static int ChannelToFreq(int channel)
        {
            if (channel == 14)
                return 2484;
            else
                return ((channel - 1) * 5 + 2412);
        }
        public static bool TuneTxXtal(int freqMHz, double freqErr, ref int xtalcap, ref int xtalCapOriginal)
        {
            double up_limit = Global.calXtal.TargetPPM;
            double lo_limit = Global.calXtal.TargetPPM * -1;

            if ((freqErr >= lo_limit) && (freqErr <= up_limit))
            {
                return false;
            }
            else
            {
                int index_offset = (int)((freqErr * freqMHz) / Global.calXtal.StepHZ);

                xtalCapOriginal += index_offset;

                xtalcap = Functions.ConvertToComplementCode(xtalCapOriginal) & 0x7F;

                //0x0~0x7F
                if (xtalCapOriginal < -127)
                    xtalcap = -127;
                else if (xtalcap > 127)
                    xtalcap = 127;

                return true;
            }
        }
        public static string getMCSIndex(string datarate, ref PPDU ppdutype)
        {
            #region legacy
            if (datarate.Contains("M") && !datarate.Contains("MCS"))
            {
                ppdutype = PPDU.Legacy;

                if (datarate.Contains("54M"))
                    return "0xb";
                else if (datarate.Contains("48M"))
                    return "0xa";
                else if (datarate.Contains("36M"))
                    return "0x9";
                else if (datarate.Contains("24M"))
                    return "0x8";
                else if (datarate.Contains("18M"))
                    return "0x7";
                else if (datarate.Contains("12M"))
                    return "0x6";
                else if (datarate.Contains("9M"))
                    return "0x5";
                else if (datarate.Contains("6M"))
                    return "0x4";
                else if (datarate.Contains("11M"))
                {
                    ppdutype = PPDU.CCK;
                    return "0x3";
                }
                else if (datarate.Contains("5M"))
                {
                    ppdutype = PPDU.CCK;
                    return "0x2";
                }
                else if (datarate.Contains("2M"))
                {
                    ppdutype = PPDU.CCK;
                    return "0x1";
                }
                else if (datarate.Contains("1M"))
                {
                    ppdutype = PPDU.CCK;
                    return "0x0";
                }
            }
            #endregion
                
            #region HE
            else if(datarate.Contains("HE"))
            {
                ppdutype = PPDU.HE_SU;  

                if (datarate.Contains("MCS0"))
                    return "0x500";
                else if (datarate.Contains("MCS1"))
                    return "0x501";
                else if (datarate.Contains("MCS2"))
                    return "0x502";
                else if (datarate.Contains("MCS3"))
                    return "0x503";
                else if (datarate.Contains("MCS4"))
                    return "0x504";
                else if (datarate.Contains("MCS5"))
                    return "0x505";
                else if (datarate.Contains("MCS6"))
                    return "0x506";
                else if (datarate.Contains("MCS7"))
                    return "0x507";
                else if (datarate.Contains("MCS8"))
                    return "0x508";
                else if (datarate.Contains("MCS9"))
                    return "0x509";
            }
            #endregion

            #region HT
            else if (datarate.Contains("HT"))
            {
                ppdutype = PPDU.HT_MF;

                if (datarate.Contains("MCS0"))
                    return "0x200";
                else if (datarate.Contains("MCS1"))
                    return "0x201";
                else if (datarate.Contains("MCS2"))
                    return "0x202";
                else if (datarate.Contains("MCS3"))
                    return "0x203";
                else if (datarate.Contains("MCS4"))
                    return "0x204";
                else if (datarate.Contains("MCS5"))
                    return "0x205";
                else if (datarate.Contains("MCS6"))
                    return "0x206";
                else if (datarate.Contains("MCS7"))
                    return "0x207";
            }
            #endregion

            SetAppendText("   Wrong DataRate: " + datarate + "\r\n", ConsoleColor.Red);
            return "";   
        }
        public static bool showResult(string item, double value, double upper, double lower, string unit, string mode)
        {
            bool bResult = true;
            string sStr = "";
            item = "   " + item.PadRight(20, ' ') + ":".PadRight(10, ' ');
            int length = 23;
            string strItem = item.PadRight(length, ' ');
            double dUpper = upper;
            double dLower = lower;
            if (unit == "" || unit == null) unit = "   ";
            if (value >= dLower && value <= dUpper) 
            {
                if ((dLower == -999) && (dUpper == 999))
                    sStr = strItem + value.ToString("0.00").PadLeft(6, ' ') + " " + unit.PadLeft(3, ' ');
                else
                    sStr = strItem + value.ToString("0.00").PadLeft(6, ' ') + " " + unit.PadLeft(3, ' ') + "   (" + dUpper.ToString("0.00") + " ~ " + dLower.ToString("0.00") + ")\r\n";
                if(mode == "show") SetAppendText(sStr);
                bResult = true;
            }
            else
            {
                sStr = strItem + value.ToString("0.00").PadLeft(6, ' ') + " " + unit.PadLeft(3, ' ') + "   (" + dUpper.ToString("0.00") + " ~ " + dLower.ToString("0.00") + ")        fail\r\n";
                if (mode == "show") SetAppendText(sStr, ConsoleColor.Red);
                bResult = false;
            }

            return bResult;
        }
        public static PCOM_OFFSET getPCOM(double delta)
        {
            PCOM_OFFSET pcomOffset = new PCOM_OFFSET();
            int dec = 32;
            for (double pcom = 4; pcom >= -4; pcom -= 0.25)
            {
                pcomOffset.pcom = pcom;
                pcomOffset.offset = dec;
                pcomOffset.offsetC = Functions.ConvertToComplementCode(dec);
                dec -= 2;

                double low = (-1) * pcom - 0.125;
                double upper = (-1) * pcom + 0.125;

                if (delta >= low && delta <= upper)
                {
                    return pcomOffset;
                }
            }

            return pcomOffset;
        }
        public static int bleTargetPower(double power)
        {
            if (power == 15) return 0b0111;
            if (power == 12) return 0b0110;
            if (power == 10) return 0b0101;
            if (power == 8)  return 0b0100;
            if (power == 6)  return 0b0011;
            if (power == 4)  return 0b0010;
            if (power == 2)  return 0b0001;
            if (power == 0)  return 0b0000;
            if (power == -3) return 0b1111;
            if (power == -6) return 0b1110;
            if (power == -9) return 0b1101;
            if (power == -12) return 0b1100;
            if (power == -15) return 0b1011;
            if (power == -18) return 0b1010;
            if (power == -21) return 0b1001;
            if (power == -24) return 0b1000;

            SetAppendText(string.Format("BLE target power is wrong![{0}]\r\n", power), ConsoleColor.Red);

            return -1;
        }
        public static int bleMaxPower(double power)
        {
            if (power == 15) return 0b0010;
            if (power == 12) return 0b0001;
            if (power == 8)  return 0b0000;
            if (power == 4)  return 0b1111;
            if (power == 0)  return 0b1110;
            if (power == -6) return 0b1101;
            if (power == -12)return 0b1100;

            SetAppendText(string.Format("BLE Max power is wrong![{0}]\r\n", power), ConsoleColor.Red);

            return -1;
        }
        public static int getCCKTarget(double power)
        {
            int bit = 0b1000;
            for(double i = 13;i<= 20;i+=0.5)
            {
                if(power == i) return bit;

                bit += 1;

                bit = bit & 0xF;
            }

            SetAppendText(string.Format("CCK target power is wrong![{0}]\r\n", power), ConsoleColor.Red);

            return -1;
        }
        public static int getMCSTarget(double power)
        {
            int bit = 0b1000;
            for (double i = 10; i <= 15; i += 0.5)
            {
                if (power == i) return bit;

                bit += 1;

                bit = bit & 0xF;
            }

            SetAppendText(string.Format("MCS target power is wrong![{0}]\r\n", power), ConsoleColor.Red);

            return -1;
        }
        public static int get54MOffset(double offset)
        {
            if (offset == 2) return 0b0010;
            if (offset == 1.5) return 0b0001;
            if (offset == 1) return 0b0000;
            if (offset == 0.5) return 0b1111;
            if (offset == 0) return 0b1110;

            SetAppendText(string.Format("54M offset is wrong![{0}]\r\n", offset), ConsoleColor.Red);

            return -1;
        }
        public static int getCCKMax(double power)
        {
            int bit = 0b1000;
            for (double i = 14; i <= 21; i += 0.5)
            {
                if (power == i) return bit;

                bit += 1;

                bit = bit & 0xF;
            }

            SetAppendText(string.Format("CCK max power is wrong![{0}]\r\n", power), ConsoleColor.Red);

            return -1;
        }
        public static int getMCSMax(double power)
        {
            int bit = 0b1000;
            for (double i = 14; i <= 19; i += 0.5)
            {
                if (power == i) return bit;

                bit += 1;

                bit = bit & 0xF;
            }

            SetAppendText(string.Format("MCS max power is wrong![{0}]\r\n", power), ConsoleColor.Red);

            return -1;
        }
        public static int getGroup(int channel)
        {
            if (channel >= 1 && channel <= 2)
                return 1;
            else if (channel >= 3 && channel <= 5)
                return 2;
            else if (channel >= 6 && channel <= 8)
                return 3;
            else if (channel >= 9 && channel <= 11)
                return 4;
            else if (channel >= 12 && channel <= 13)
                return 5;
            else if (channel == 14)
                return 6;

            else if (channel >= 36 && channel <= 40)
                return 1;
            else if (channel >= 44 && channel <= 48)
                return 2;
            else if (channel >= 52 && channel <= 56)
                return 3;
            else if (channel >= 60 && channel <= 64)
                return 4;
            else if (channel >= 100 && channel <= 104)
                return 5;
            else if (channel >= 108 && channel <= 112)
                return 6;
            else if (channel >= 116 && channel <= 120)
                return 7;
            else if (channel >= 124 && channel <= 128)
                return 8;
            else if (channel >= 132 && channel <= 136)
                return 9;
            else if (channel >= 140 && channel <= 144)
                return 10;
            else if (channel >= 149 && channel <= 153)
                return 11;
            else if (channel >= 157 && channel <= 161)
                return 12;
            else if (channel >= 165 && channel <= 169)
                return 13;
            else if (channel >= 173 && channel <= 177)
                return 14;
            else
            {
                SetAppendText("   Wrong channel![" + channel + "]\r\n", ConsoleColor.Red);
                return 0;
            }
        }
        public static int interpolation(int x1, int y1, int x2, int y2, int x)
        {
            return y1 - ((y1 - y2) / (x1 - x2)) * (x1 - x);
        }
        public static bool checkGroup(int band, int iGroup)
        {
            if (band == 0)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (Global.channel_2G[i] == 0) continue;
                    if (iGroup == getGroup(Global.channel_2G[i])) return true;
                }
            }
            else
            {
                for (int i = 0; i < 14; i++)
                {
                    if (Global.channel_5G[i] == 0) continue;
                    if (iGroup == getGroup(Global.channel_5G[i])) return true;
                }
            }

            return false;
        }
        public static bool checkGroupCCK(int iGroup)
        {
            for (int i = 0; i < 6; i++)
            {
                if (Global.channel_2G_CCK[i] == 0) continue;
                if (iGroup == getGroup(Global.channel_2G_CCK[i])) return true;
            }

            return false;
        }
        public static void findGroup(int max, bool[] DE_Done, int x, ref int x1, ref int x2)
        {
            int x_left = 0;
            for (int i = (x - 1); i > 0; i--)
            {
                if (DE_Done[i])
                {
                    x_left = i;
                    break;
                }
            }

            int x_right = 0;
            for (int i = (x + 1); i < max; i++)
            {
                if (DE_Done[i])
                {
                    x_right = i;
                    break;
                }
            }

            if (x_left != 0 && x_right != 0)
            {
                x1 = x_left;
                x2 = x_right;
                return;
            }

            if (x_left == 0 && x_right != 0)
            {
                x1 = x_right;
                for (int i = (x1 + 1); i < max; i++)
                {
                    if (DE_Done[i])
                    {
                        x_right = i;
                        break;
                    }
                }

                x2 = x_right;

                return;
            }

            if (x_left != 0 && x_right == 0)
            {
                x2 = x_left;
                for (int i = (x2 - 1); i > 0; i--)
                {
                    if (DE_Done[i])
                    {
                        x_right = i;
                        break;
                    }
                }

                x1 = x_right;
            }
        }
        public static void calcDE()
        {
            SetAppendText("\r\n\r\n   Interpolation for finding Gain...\r\n\r\n");

            #region Calc Done
            bool[] Gain_CCK_Done = new bool[7];
            bool[] Gain_2G_Done = new bool[7];
            bool[] Gain_5G_Done = new bool[15];

            if (Global.calPower.EnableCCK)
            {
                for (int i = 1; i < 7; i++)
                {
                    Gain_CCK_Done[i] = checkGroup(0, i);
                }
            }

            if (Global.calPower.Enable2G)
            {
                for (int i = 1; i < 7; i++)
                {
                    Gain_2G_Done[i] = checkGroup(0, i);
                }
            }

            if (Global.calPower.Enable5G)
            {
                for (int i = 1; i < 15; i++)
                {
                    Gain_5G_Done[i] = checkGroup(1, i);
                }
            }
            #endregion

            #region Calc Gain
            if (Global.calPower.EnableCCK)
            {
                for (int i = 1; i < 7; i++)
                {
                    if (Gain_CCK_Done[i]) continue;

                    int x1 = 0, x2 = 0;
                    findGroup(7, Gain_CCK_Done, i, ref x1, ref x2);
                    int y1 = Global.gain_2G_CCK[x1];
                    int y2 = Global.gain_2G_CCK[x2];

                    Global.gain_2G_CCK[i] = interpolation(x1, y1, x2, y2, i);

                    Gain_CCK_Done[i] = true;
                }
            }
            if (Global.calPower.Enable2G)
            {
                for (int i = 1; i < 7; i++)
                {
                    if (Gain_2G_Done[i])
                    {
                        if (!Global.calPower.EnableCCK) Global.gain_2G_CCK[i] = Global.gain_2G[i] + Global.calPower.Offset_CCK;
                        continue;
                    }

                    int x1 = 0, x2 = 0;
                    findGroup(7, Gain_2G_Done, i, ref x1, ref x2);
                    int y1 = Global.gain_2G[x1];
                    int y2 = Global.gain_2G[x2];

                    Global.gain_2G[i] = interpolation(x1, y1, x2, y2, i);
                    if (!Global.calPower.EnableCCK) Global.gain_2G_CCK[i] = Global.gain_2G[i] + Global.calPower.Offset_CCK;

                    Gain_2G_Done[i] = true;
                }
            }
            if (Global.calPower.Enable5G)
            {
                for (int i = 1; i < 15; i++)
                {
                    if (Gain_5G_Done[i]) continue;

                    int x1 = 0, x2 = 0;
                    findGroup(15, Gain_5G_Done, i, ref x1, ref x2);
                    int y1 = Global.gain_5G[x1];
                    int y2 = Global.gain_5G[x2];

                    Global.gain_5G[i] = interpolation(x1, y1, x2, y2, i);

                    Gain_5G_Done[i] = true;
                }
            }
            #endregion   

            if (Global.calPower.Enable2G)
            {
                #region CCK show DE
                {
                    string sGroup = "   CCK Group     ";
                    string sGain = "   Offset       ";
                    string sAddr = "   Address      ";
                    int addr = 0x20;

                    for (int i = 1; i < 7; i++)
                    {
                        sGroup += "G" + i + "    ";
                        sGain += Global.gain_2G_CCK[i].ToString().PadLeft(3, ' ') + "   ";
                        sAddr += addr.ToString("X").PadLeft(3, ' ') + "   ";
                        addr++;
                    }

                    SetAppendText(sGroup + "\r\n");
                    SetAppendText(sAddr + "\r\n");
                    SetAppendText(sGain + "\r\n");
                    SetAppendText("\r\n");
                }
                #endregion

                #region 2G show DE
                {
                    string sGroup = "   2G Group      ";
                    string sGain = "   Offset       ";
                    string sAddr = "   Address      ";
                    int addr = 0x26;

                    for (int i = 1; i < 6; i++)
                    {
                        sGroup += "G" + i + "    ";
                        sGain += Global.gain_2G[i].ToString().PadLeft(3, ' ') + "   ";
                        sAddr += addr.ToString("X").PadLeft(3, ' ') + "   ";
                        addr++;
                    }

                    SetAppendText(sGroup + "\r\n");
                    SetAppendText(sAddr + "\r\n");
                    SetAppendText(sGain + "\r\n");
                    SetAppendText("\r\n");
                }
                #endregion
            }

            #region 5G show DE
            if (Global.calPower.Enable5G)
            {
                string sGroup = "   5G Group      ";
                string sGain = "   Offset       ";
                string sAddr = "   Address      ";
                int addr = 0x32;

                for (int i = 1; i < 15; i++)
                {
                    sGroup += "G" + i + "    ";
                    if (i > 9)
                    {
                        sGain += Global.gain_5G[i].ToString().PadLeft(4, ' ') + "   ";
                        sAddr += addr.ToString("X").PadLeft(4, ' ') + "   ";
                    }
                    else
                    {
                        sGain += Global.gain_5G[i].ToString().PadLeft(3, ' ') + "   ";
                        sAddr += addr.ToString("X").PadLeft(3, ' ') + "   ";
                    }
                    addr++;
                }

                SetAppendText(sGroup + "\r\n");
                SetAppendText(sAddr + "\r\n");
                SetAppendText(sGain + "\r\n");
                SetAppendText("\r\n");
            }
            #endregion
        }
        public static int getDEGroup(int channel, ref int x1, ref int x2)
        {
            if (channel >= 1 && channel <= 2)
                return 1;
            else if (channel >= 3 && channel <= 5)
                return 2;
            else if (channel >= 6 && channel <= 8)
                return 3;
            else if (channel >= 9 && channel <= 11)
                return 4;
            else if (channel >= 12 && channel <= 13)
                return 5;
            else if (channel == 14)
                return 6;

            else if (channel >= 36 && channel <= 40)
                return 1;
            else if (channel > 40 && channel < 44)
            {
                x1 = 1;
                x2 = 2;
                return 0;
            }
            else if (channel >= 44 && channel <= 48)
                return 2;
            else if (channel > 48 && channel < 52)
            {
                x1 = 2;
                x2 = 3;
                return 0;
            }
            else if (channel >= 52 && channel <= 56)
                return 3;
            else if (channel > 56 && channel < 60)
            {
                x1 = 3;
                x2 = 4;
                return 0;
            }
            else if (channel >= 60 && channel <= 64)
                return 4;
            else if (channel > 64 && channel < 100)
            {
                x1 = 4;
                x2 = 5;
                return 0;
            }
            else if (channel >= 100 && channel <= 104)
                return 5;
            else if (channel > 104 && channel < 108)
            {
                x1 = 5;
                x2 = 6;
                return 0;
            }
            else if (channel >= 108 && channel <= 112)
                return 6;
            else if (channel > 112 && channel < 116)
            {
                x1 = 6;
                x2 = 7;
                return 0;
            }
            else if (channel >= 116 && channel <= 120)
                return 7;
            else if (channel > 120 && channel < 124)
            {
                x1 = 7;
                x2 = 8;
                return 0;
            }
            else if (channel >= 124 && channel <= 128)
                return 8;
            else if (channel > 128 && channel < 132)
            {
                x1 = 8;
                x2 = 9;
                return 0;
            }
            else if (channel >= 132 && channel <= 136)
                return 9;
            else if (channel > 136 && channel < 140)
            {
                x1 = 9;
                x2 = 10;
                return 0;
            }
            else if (channel >= 140 && channel <= 144)
                return 10;
            else if (channel > 144 && channel < 149)
            {
                x1 = 10;
                x2 = 11;
                return 0;
            }
            else if (channel >= 149 && channel <= 153)
                return 11;
            else if (channel > 153 && channel < 157)
            {
                x1 = 11;
                x2 = 12;
                return 0;
            }
            else if (channel >= 157 && channel <= 161)
                return 12;
            else if (channel > 161 && channel < 165)
            {
                x1 = 12;
                x2 = 13;
                return 0;
            }
            else if (channel >= 165 && channel <= 169)
                return 13;
            else if (channel > 168 && channel < 173)
            {
                x1 = 13;
                x2 = 14;
                return 0;
            }
            else if (channel >= 173 && channel <= 177)
                return 14;
            else
            {
                return 0;
            }
        }
        public static int getGainCCK(int channel)
        {
            int x1 = 0, x2 = 0;
            int iGroup = getDEGroup(channel, ref x1, ref x2);
            int gain = Global.gain_2G_CCK[iGroup];

            SetAppendText("   Group: " + iGroup + "\r\n");
            SetAppendText("   Gain: " + gain + "\r\n");

            return gain;
        }
        public static int getGain(int channel)
        {
            int band = 0;
            if (channel > 14) band = 1;
            int x1 = 0, x2 = 0;
            int iGroup = getDEGroup(channel, ref x1, ref x2);
            int gain = 0;

            if (iGroup != 0)
            {
                if (band == 0) gain = Global.gain_2G[iGroup];
                if (band == 1) gain = Global.gain_5G[iGroup];

                SetAppendText("   Group: " + iGroup + "\r\n");
                SetAppendText("   Gain: " + gain + "\r\n");
            }
            else
            {
                int gain1 = 0, gain2 = 0;

                if (band == 0)
                {
                    gain1 = Global.gain_2G[x1];
                    gain2 = Global.gain_2G[x2];
                    gain = (gain1 + gain2) / 2;
                }
                if (band == 1)
                {
                    gain1 = Global.gain_5G[x1];
                    gain2 = Global.gain_5G[x2];
                    gain = (gain1 + gain2) / 2;
                }

                SetAppendText("   Group1: " + x1 + " Gain1:" + gain1 + "\r\n");
                SetAppendText("   Group2: " + x2 + " Gain2:" + gain2 + "\r\n");
                SetAppendText("   Gain: " + gain + "\r\n");
            }

            return gain;
        }
        public static bool getDataRate(string sDataRate, ref int bandwidth, ref string rate)
        {
            if (sDataRate.Contains("HT40"))
                bandwidth = 1;
            else
                bandwidth = 0;

            if (!sDataRate.Contains("MCS"))
            {
                if (sDataRate =="CCK-1M")
                    rate = "2";
                else if (sDataRate== "CCK-2M")
                    rate = "4";
                else if (sDataRate== "CCK-5.5M")
                    rate = "11";
                else if (sDataRate== "CCK-11M")
                    rate = "22";
                else if (sDataRate== "OFDM-6M")
                    rate = "12";
                else if (sDataRate== "OFDM-9M")
                    rate = "18";
                else if (sDataRate== "OFDM-12M")
                    rate = "24";
                else if (sDataRate== "OFDM-18M")
                    rate = "36";
                else if (sDataRate== "OFDM-24M")
                    rate = "48";
                else if (sDataRate== "OFDM-36M")
                    rate = "72";
                else if (sDataRate== "OFDM-48M")
                    rate = "96";
                else if (sDataRate== "OFDM-54M")
                    rate = "108";
                else
                {
                    SetAppendText("Error: Wrong DataRate: " + sDataRate + "\r\n", ConsoleColor.Red);
                    return false;
                }
            }
            else
            {
                if (sDataRate.Contains("MCS0"))
                    rate = "128";
                else if (sDataRate.Contains("MCS1"))
                    rate = "129";
                else if (sDataRate.Contains("MCS2"))
                    rate = "130";
                else if (sDataRate.Contains("MCS3"))
                    rate = "131";
                else if (sDataRate.Contains("MCS4"))
                    rate = "132";
                else if (sDataRate.Contains("MCS5"))
                    rate = "133";
                else if (sDataRate.Contains("MCS6"))
                    rate = "134";
                else if (sDataRate.Contains("MCS7"))
                    rate = "135";
                else
                {
                    SetAppendText("Error: Wrong DataRate: " + sDataRate + "\r\n", ConsoleColor.Red);
                    return false;
                }
            }

            return true;

        }
        public static bool parseFlow()
        {
            try
            {
                string filename = "Setup/Test_Flow.txt";

                Global.aFlowList = new List<string>();
                FileStream fsFlow = File.Open(filename, FileMode.Open, FileAccess.Read);
                StreamReader srReader = new StreamReader(fsFlow);
                string sReadLine = "";
                char[] sSeparator = { '=', '\r', '\n', '\t', ' ' };
                while (sReadLine != null)
                {
                    sReadLine = srReader.ReadLine();
                    if (sReadLine == null)
                        break;
                    string[] sSplit = sReadLine.Trim().Split(sSeparator);
                    if (!sSplit[0].Contains("//") && sSplit[0] != "")
                    {
                        for (int i = 0; i < sSplit.Length; i++)
                        {
                            if (sSplit[i] != "")
                                Global.aFlowList.Add(sSplit[i]);
                        }
                    }
                }
                srReader.Close();
                fsFlow.Close();
            }
            catch (Exception ex)
            {
                SetAppendText("   parseFlow Error: " + ex.Message + "\r\n", ConsoleColor.Red);
                return false;
            }
            return true;
        }
        public static bool parseConfig()
        {
            string path = Environment.CurrentDirectory + "/Setup/config.ini";
            IniParser iniParser = new IniParser(path);

            #region Setting
            Global.testSetup.OS = iniParser.GetSetting("Setting", "OS", "Linux");
            Global.testSetup.Model = iniParser.GetSetting("Setting", "Model", "8733BU");
            Global.testSetup.COMPort = iniParser.GetSetting("Setting", "COMPort", "COM51");
            Global.testSetup.ANT_NUM = Convert.ToInt32(iniParser.GetSetting("Setting", "ANT_NUM", "1"));
            Global.testSetup.FAIL_TYPE = Convert.ToInt32(iniParser.GetSetting("Setting", "FAIL_TYPE", "0"));
            Global.testSetup.MAX_RETRY_COUNT = Convert.ToInt32(iniParser.GetSetting("Setting", "MAX_RETRY_COUNT", "0"));
            Global.testSetup.TX_SETTLE_TIME_MS = Convert.ToInt32(iniParser.GetSetting("Setting", "TX_SETTLE_TIME_MS", "0"));
            Global.testSetup.RX_SETTLE_TIME_MS = Convert.ToInt32(iniParser.GetSetting("Setting", "RX_SETTLE_TIME_MS", "0"));
            Global.testSetup.RXsweepMAXcount = Convert.ToInt32(iniParser.GetSetting("Setting", "RXSWEEPMAXCOUNT", "50"));
            Global.testSetup.CheckSum = Convert.ToBoolean(Convert.ToInt32(iniParser.GetSetting("Setting", "CheckSum", "1")));
            Global.testSetup.AUTO_RUN = Convert.ToInt32(iniParser.GetSetting("Setting", "AUTO_RUN", "0"));
            Global.testSetup.CommandDelayMS = Convert.ToInt32(iniParser.GetSetting("Setting", "CommandDelayMS", "0"));
            Global.testSetup.Debug = Convert.ToInt32(iniParser.GetSetting("Setting", "Debug", "0"));
            Global.testSetup.CSV_LOG = Convert.ToInt32(iniParser.GetSetting("Setting", "CSV_LOG", "0"));
            Global.testSetup.CABLE_LOSS_DATE = Convert.ToInt32(iniParser.GetSetting("Setting", "CABLE_LOSS_DATE", "60"));
            Global.testSetup.SwitchtoNormal = Convert.ToInt32(iniParser.GetSetting("Setting", "SwitchtoNormal", "0"));
            Global.testSetup.Reboot = Convert.ToInt32(iniParser.GetSetting("Setting", "Reboot", "0"));
            Global.testSetup.ATST = Convert.ToInt32(iniParser.GetSetting("Setting", "ATST", "0"));
            Global.testSetup.CheckStatus = iniParser.GetSetting("Setting", "CheckStatus", "wifiver");
            Global.testSetup.CheckATSC = iniParser.GetSetting("Setting", "CheckATSC", "CLEAR_OTA");
            Global.testSetup.MAP_FILE = iniParser.GetSetting("Setting", "MAP_FILE", "");
            Global.testSetup.WaitForDebug = iniParser.GetSetting("Setting", "WaitForDebug", "0");
            Global.testSetup.WRITE_MAC = Convert.ToInt32(iniParser.GetSetting("Setting", "WRITE_MAC", "1"));
            #endregion

            #region CAL_XTAL
            if (Convert.ToInt32(iniParser.GetSetting("CAL_XTAL", "Enable", "0")) == 1)
                Global.calXtal.Enable = true;
            else
                Global.calXtal.Enable = false;
            Global.calXtal.Channel = Convert.ToInt32(iniParser.GetSetting("CAL_XTAL", "Channel", "7"));
            Global.calXtal.TxPower = Convert.ToDouble(iniParser.GetSetting("CAL_XTAL", "TxPower", "17"));
            Global.calXtal.TargetPPM = Convert.ToDouble(iniParser.GetSetting("CAL_XTAL", "TargetPPM", "0"));


            Global.calXtal.Init_Gain = Convert.ToInt32(iniParser.GetSetting("CAL_XTAL", "Init_Gain", "64"));
            Global.calXtal.Init_Gap = Convert.ToInt32(iniParser.GetSetting("CAL_XTAL", "Init_Cap", "63"));


            Global.calXtal.StepHZ = Convert.ToInt32(iniParser.GetSetting("CAL_XTAL", "StepHZ", "2000"));
            Global.calXtal.SETTLE_MS = Convert.ToInt32(iniParser.GetSetting("CAL_XTAL", "SETTLE_MS", "100"));
            #endregion

            #region CAL_POWER
            if (Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "EnableCCK", "0")) == 1)
                Global.calPower.EnableCCK = true;
            else
                Global.calPower.EnableCCK = false;

            if (Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Enable2G", "0")) == 1)
                Global.calPower.Enable2G = true;
            else
                Global.calPower.Enable2G = false;

            if (Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Enable5G", "0")) == 1)
                Global.calPower.Enable5G = true;
            else
                Global.calPower.Enable5G = false;

            Global.calPower.TxPowerCCK = Convert.ToDouble(iniParser.GetSetting("CAL_POWER", "TxPowerCCK", "20"));
            Global.calPower.TxPower2G = Convert.ToDouble(iniParser.GetSetting("CAL_POWER", "TxPower2G", "17"));
            Global.calPower.TxPower5G = Convert.ToDouble(iniParser.GetSetting("CAL_POWER", "TxPower5G", "17"));

            Global.calPower.TxPowerOFDM_2G = Convert.ToDouble(iniParser.GetSetting("CAL_POWER", "TxPowerOFDM_2G", "15"));
            Global.calPower.TxPowerOFDM_5G = Convert.ToDouble(iniParser.GetSetting("CAL_POWER", "TxPowerOFDM_5G", "13"));

            Global.calPower.Limit = Convert.ToDouble(iniParser.GetSetting("CAL_POWER", "Limit", "0.5"));
            Global.calPower.Init_Gain_2G = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Init_Gain_2G", "64"));
            Global.calPower.Init_Gain_5G = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Init_Gain_5G", "64"));
            Global.calPower.Init_Gain_CCK = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Init_Gain_CCK", "64"));
            Global.calPower.SETTLE_MS = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "SETTLE_MS", "100"));

            {
                string CCK_Offset = iniParser.GetSetting("CAL_POWER", "Offset_CCK", "0");
                Global.calPower.Offset_CCK = Convert.ToInt32(CCK_Offset);
            }

            Global.calPower.Offset_54M_2G = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Offset_54M_2G", "0"));
            Global.calPower.Offset_54M_5G = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Offset_54M_5G", "0"));

            Global.calPower.Offset_HT20_2G = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Offset_HT20_2G", "0"));
            Global.calPower.Offset_HT20_5G = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "Offset_HT20_5G", "0"));

            Global.calPower.GainMax = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "GainMax", "0x7F"), 16);
            Global.calPower.GainMin = Convert.ToInt32(iniParser.GetSetting("CAL_POWER", "GainMin", "0x30"), 16);


            {
                string CCK_Offset = iniParser.GetSetting("CAL_POWER", "CCK_Channel", "0,4,0,10,0,0");
                string[] cckSplit = CCK_Offset.Split(',');
                Global.channel_2G_CCK = new int[6];
                for (int i = 0; i < 6; i++)
                {
                    Global.channel_2G_CCK[i] = Convert.ToInt32(cckSplit[i]);
                }
            }

            {
                string CCK_Offset = iniParser.GetSetting("CAL_POWER", "2G_Channel", "0,4,0,10,0");
                string[] cckSplit = CCK_Offset.Split(',');
                Global.channel_2G = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    Global.channel_2G[i] = Convert.ToInt32(cckSplit[i]);
                }
            }

            {
                string CCK_Offset = iniParser.GetSetting("CAL_POWER", "5G_Channel", "38,46,54,62,102,0,0,0,0,142,151,0,0,175");
                string[] cckSplit = CCK_Offset.Split(',');
                Global.channel_5G = new int[14];
                for (int i = 0; i < 14; i++)
                {
                    Global.channel_5G[i] = Convert.ToInt32(cckSplit[i]);
                }
            }

            {
                string gain = iniParser.GetSetting("CAL_POWER", "Gain_CCK", "102,102,102,102,102,102");
                string[] gainSplit = gain.Split(',');
                Global.calPower.Gain_CCK = new int[6];
                for (int i = 0; i < 6; i++)
                {
                    Global.calPower.Gain_CCK[i] = Convert.ToInt32(gainSplit[i]);
                }
            }

            {
                string gain = iniParser.GetSetting("CAL_POWER", "Gain_2G", "78,78,78,78,78");
                string[] gainSplit = gain.Split(',');
                Global.calPower.Gain_2G = new int[5];
                for (int i = 0; i < 5; i++)
                {
                    Global.calPower.Gain_2G[i] = Convert.ToInt32(gainSplit[i]);
                }
            }

            {
                string gain = iniParser.GetSetting("CAL_POWER", "Gain_5G", "102,102,102,102,102,102,102,102,102,102,102,102,102,102");
                string[] gainSplit = gain.Split(',');
                Global.calPower.Gain_5G = new int[14];
                for (int i = 0; i < 14; i++)
                {
                    Global.calPower.Gain_5G[i] = Convert.ToInt32(gainSplit[i]);
                }
            }
            #endregion

            #region WIFI_LIMIT
            Global.testSetup.EVM_ABG = new double[12];
            Global.testSetup.EVM_HT = new double[8];
            Global.testSetup.EVM_VHT_20 = new double[10];
            Global.testSetup.EVM_VHT_40 = new double[10];
            Global.testSetup.EVM_VHT_80 = new double[10];
            Global.testSetup.EVM_HE = new double[12];
            Global.testSetup.EVM_LOW = -80;

            string sTemp = iniParser.GetSetting("WIFI_LIMIT", "EVM_ABG", "-25,-22,-19,-16,-13,-10,-8,-5,-10,-10,-10,-10");
            string[] sSplit = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sSplit.Length; i++)
            {
                Global.testSetup.EVM_ABG[i] = Convert.ToDouble(sSplit[i]);
            }

            sTemp = iniParser.GetSetting("WIFI_LIMIT", "EVM_HT", "-5, -10, -13, -16, -19, -22, -25, -28");
            sSplit = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sSplit.Length; i++)
            {
                Global.testSetup.EVM_HT[i] = Convert.ToDouble(sSplit[i]);
            }

            sTemp = iniParser.GetSetting("WIFI_LIMIT", "EVM_VHT_20", "-5, -10, -13, -16, -19, -22, -25, -28, -30, -32");
            sSplit = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sSplit.Length; i++)
            {
                Global.testSetup.EVM_VHT_20[i] = Convert.ToDouble(sSplit[i]);
            }

            sTemp = iniParser.GetSetting("WIFI_LIMIT", "EVM_VHT_40", "-5, -10, -13, -16, -19, -22, -25, -28, -30, -32");
            sSplit = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sSplit.Length; i++)
            {
                Global.testSetup.EVM_VHT_40[i] = Convert.ToDouble(sSplit[i]);
            }

            sTemp = iniParser.GetSetting("WIFI_LIMIT", "EVM_VHT_80", "-5, -10, -13, -16, -19, -22, -25, -28, -30, -32");
            sSplit = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sSplit.Length; i++)
            {
                Global.testSetup.EVM_VHT_80[i] = Convert.ToDouble(sSplit[i]);
            }


            sTemp = iniParser.GetSetting("WIFI_LIMIT", "EVM_HE", "-5, -10, -13, -16, -19, -22, -25, -28, -30, -32, -35, -35");
            sSplit = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < sSplit.Length; i++)
            {
                Global.testSetup.EVM_HE[i] = Convert.ToDouble(sSplit[i]);
            }

            Global.testSetup.EVM_LOW = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "EVM_LOW", "-80"));
            Global.testSetup.POW_UP = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "POW_UP", "2"));
            Global.testSetup.POW_LO = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "POW_LO", "-2"));
            Global.testSetup.FREQ_UP = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "FREQ_UP", "20"));
            Global.testSetup.FREQ_LO = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "FREQ_LO", "-20"));
            Global.testSetup.MASK = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "MASK", "10"));
            Global.testSetup.per = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "PER", "10"));
            Global.testSetup.per_b = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "PER_B", "10"));
            Global.testSetup.SYMBLE_UP = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "SYMBLE_UP", "10"));
            Global.testSetup.SYMBLE_LO = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "SYMBLE_LO", "-10"));
            Global.testSetup.LO_CCK = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_CCK", "-30"));
            Global.testSetup.LO_AG = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_AG", "-10"));
            Global.testSetup.LO_HT20_5G = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_HT20_5G", "-21"));
            Global.testSetup.LO_HT40_5G = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_HT40_5G", "-21"));
            Global.testSetup.LO_HT20_2G = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_HT20_2G", "-18"));
            Global.testSetup.LO_HT40_2G = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_HT40_2G", "-18"));
            Global.testSetup.LO_VHT = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_VHT", "-24"));
            Global.testSetup.LO_HE = Convert.ToDouble(iniParser.GetSetting("WIFI_LIMIT", "LO_HE", "-18"));
            Global.testSetup.PER_NUM_ABG = Convert.ToInt32(iniParser.GetSetting("WIFI_LIMIT", "PER_NUM_ABG", "100"));
            Global.testSetup.PER_NUM_HT = Convert.ToInt32(iniParser.GetSetting("WIFI_LIMIT", "PER_NUM_HT", "100"));
            #endregion

            #region WIFI_ERROR_CODE
            Global.wifiErrorCode.EVM = iniParser.GetSetting("WIFI_ERROR_CODE", "EVM", "E0000");
            Global.wifiErrorCode.Power = iniParser.GetSetting("WIFI_ERROR_CODE", "Power", "E0000");
            Global.wifiErrorCode.FreqErr = iniParser.GetSetting("WIFI_ERROR_CODE", "FreqErr", "E0000");
            Global.wifiErrorCode.Mask = iniParser.GetSetting("WIFI_ERROR_CODE", "Mask", "E0000");
            Global.wifiErrorCode.SymbleClock = iniParser.GetSetting("WIFI_ERROR_CODE", "SymbleClock", "E0000");
            Global.wifiErrorCode.Loleakage = iniParser.GetSetting("WIFI_ERROR_CODE", "Loleakage", "E0000");
            Global.wifiErrorCode.PER = iniParser.GetSetting("WIFI_ERROR_CODE", "PER", "E0000");
            #endregion

            #region TEST_ERROR_CODE
            Global.testErrorCode.txCalibrationWIFI = iniParser.GetSetting("TEST_ERROR_CODE", "txCalibrationWIFI", "E0000");
            Global.testErrorCode.txCalibrationBT = iniParser.GetSetting("TEST_ERROR_CODE", "txCalibrationBT", "E0000");
            Global.testErrorCode.rssiCalibration = iniParser.GetSetting("TEST_ERROR_CODE", "rssiCalibration", "E0000");
            Global.testErrorCode.rssiVerify = iniParser.GetSetting("TEST_ERROR_CODE", "rssiVerify", "E0000");
            Global.testErrorCode.connectIQ = iniParser.GetSetting("TEST_ERROR_CODE", "connectIQ", "E0000");
            Global.testErrorCode.openWIFI = iniParser.GetSetting("TEST_ERROR_CODE", "openWIFI", "E0000");
            Global.testErrorCode.openBT = iniParser.GetSetting("TEST_ERROR_CODE", "openBT", "E0000");
            #endregion

            #region WIFI_ERROR_CODE
            Global.btErrorCode.Power = iniParser.GetSetting("BT_ERROR_CODE", "Power", "E0000");
            Global.btErrorCode.FreqErr = iniParser.GetSetting("BT_ERROR_CODE", "FreqErr", "E0000");
            Global.btErrorCode.DEVMAVG = iniParser.GetSetting("BT_ERROR_CODE", "DEVMAVG", "E0000");
            Global.btErrorCode.DEVMPEAK = iniParser.GetSetting("BT_ERROR_CODE", "DEVMPEAK", "E0000");
            Global.btErrorCode.DEVM99 = iniParser.GetSetting("BT_ERROR_CODE", "DEVM99", "E0000");
            Global.btErrorCode.OMGI = iniParser.GetSetting("BT_ERROR_CODE", "OMGI", "E0000");
            Global.btErrorCode.OMGO = iniParser.GetSetting("BT_ERROR_CODE", "OMGO", "E0000");
            Global.btErrorCode.OMGIO = iniParser.GetSetting("BT_ERROR_CODE", "OMGIO", "E0000");
            Global.btErrorCode.DIFFPOWER = iniParser.GetSetting("BT_ERROR_CODE", "DIFFPOWER", "E0000");
            Global.btErrorCode.DeltaF2avg = iniParser.GetSetting("BT_ERROR_CODE", "DeltaF2avg", "E0000");
            Global.btErrorCode.DeltaF2max = iniParser.GetSetting("BT_ERROR_CODE", "DeltaF2max", "E0000");
            Global.btErrorCode.DeltaF1avg = iniParser.GetSetting("BT_ERROR_CODE", "DeltaF1avg", "E0000");
            Global.btErrorCode.DELTAF2F1avg = iniParser.GetSetting("BT_ERROR_CODE", "DELTAF2F1avg", "E0000");
            Global.btErrorCode.PER = iniParser.GetSetting("BT_ERROR_CODE", "PER", "E0000");
            Global.btErrorCode.BER = iniParser.GetSetting("BT_ERROR_CODE", "BER", "E0000");
            Global.btErrorCode.MASK = iniParser.GetSetting("BT_ERROR_CODE", "MASK", "E0000");
            Global.btErrorCode.F0FnMax = iniParser.GetSetting("BT_ERROR_CODE", "F0FnMax", "E0000");
            Global.btErrorCode.FnMax = iniParser.GetSetting("BT_ERROR_CODE", "FnMax", "E0000");
            Global.btErrorCode.F1F0 = iniParser.GetSetting("BT_ERROR_CODE", "F1F0", "E0000");
            Global.btErrorCode.FnFn5Max = iniParser.GetSetting("BT_ERROR_CODE", "FnFn5Max", "E0000");
            #endregion

            #region BT
            Global.btSetup.HOST = Convert.ToInt32(iniParser.GetSetting("BT", "HOST", "1"));
            Global.btSetup.TX_SETTLE_TIME_MS = Convert.ToInt32(iniParser.GetSetting("BT", "TX_SETTLE_TIME_MS", "100"));
            Global.btSetup.RX_SETTLE_TIME_MS = Convert.ToInt32(iniParser.GetSetting("BT", "RX_SETTLE_TIME_MS", "100"));
            Global.btSetup.CAL_SETTLE_TIME_MS = Convert.ToInt32(iniParser.GetSetting("BT", "CAL_SETTLE_TIME_MS", "2500"));
            #endregion

            #region CAL_POWER_BT
            if (Convert.ToInt32(iniParser.GetSetting("CAL_POWER_BT", "Enable", "0")) == 1)
                Global.calPowerBT.Enable = true;
            else
                Global.calPowerBT.Enable = false;


            if (Convert.ToInt32(iniParser.GetSetting("CAL_POWER_BT", "Flatness", "0")) == 1)
                Global.calPowerBT.Flatness = true;
            else
                Global.calPowerBT.Flatness = false;





            sTemp = iniParser.GetSetting("CAL_POWER_BT", "POW", "1, -1");
            Global.calPowerBT.POW = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            Global.calPowerBT.TxPower = Convert.ToDouble(iniParser.GetSetting("CAL_POWER_BT", "TxPower", "4"));

            Global.calPowerBT.FreqMHz = Convert.ToInt32(Convert.ToInt32(iniParser.GetSetting("CAL_POWER_BT", "FreqMHz", "2402")));
            #endregion

            #region BR LIMIT
            sTemp = iniParser.GetSetting("BR_LIMIT", "POW", "12, -4");
            Global._BRLimit.POW = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("BR_LIMIT", "DELTA_F1_AVG", "275, 225");
            Global._BRLimit.DELTA_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("BR_LIMIT", "DELTA_F2_AVG", "275, 225");
            Global._BRLimit.DELTA_F2_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("BR_LIMIT", "DELTA_F2_MAX", "275, 185");
            Global._BRLimit.DELTA_F2_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("BR_LIMIT", "INIT_FREQ_ERR", "150, -150");
            Global._BRLimit.INIT_FREQ_ERR = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("BR_LIMIT", "DELTA_F2_F1_AVG", "1.1, 0.8");
            Global._BRLimit.DELTA_F2_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("BR_LIMIT", "PER", "50, 0");
            Global._BRLimit.PER = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("BR_LIMIT", "PER_NUM", "500");
            Global._BRLimit.PER_NUM = sTemp.ToString().Trim();
            #endregion

            #region EDR LIMIT
            sTemp = iniParser.GetSetting("EDR_LIMIT", "POW", "12, -4");
            Global._EDRLimit.POW = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("EDR_LIMIT", "DEVM_AVG", "275, 225");
            Global._EDRLimit.DEVM_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("EDR_LIMIT", "DEVM_PEAK", "275, 225");
            Global._EDRLimit.DEVM_PEAK = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("EDR_LIMIT", "DEVM_99", "275, 185");
            Global._EDRLimit.DEVM_99 = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("EDR_LIMIT", "OMG_I", "150, -150");
            Global._EDRLimit.OMG_I = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("EDR_LIMIT", "OMG_O", "1.1, 0.8");
            Global._EDRLimit.OMG_O = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("EDR_LIMIT", "OMG_IO", "1.1, 0.8");
            Global._EDRLimit.OMG_IO = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = sTemp = iniParser.GetSetting("EDR_LIMIT", "DIFF_POWER", "1.1, 0.8");
            Global._EDRLimit.DIFF_POWER = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("EDR_LIMIT", "INIT_FREQ_ERR", "1.1, 0.8");
            Global._EDRLimit.INIT_FREQ_ERR = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("EDR_LIMIT", "PER", "50, 0");
            Global._EDRLimit.PER = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("EDR_LIMIT", "MASK", "2, 0");
            Global._EDRLimit.MASK = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = sTemp = iniParser.GetSetting("EDR_LIMIT", "PER_NUM", "500");
            Global._EDRLimit.PER_NUM = sTemp.ToString().Trim();
            #endregion

            #region LE1 LIMIT
            sTemp = iniParser.GetSetting("1LE_LIMIT", "POW", "12, -4");
            Global._1LELimit.POW = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_F1_AVG", "275, 225");
            Global._1LELimit.DELTA_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_F2_AVG", "275, 225");
            Global._1LELimit.DELTA_F2_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_F2_MAX", "275, 185");
            Global._1LELimit.DELTA_F2_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "INIT_FREQ_ERR", "150, -150");
            Global._1LELimit.INIT_FREQ_ERR = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_F2_F1_AVG", "1.1, 0.8");
            Global._1LELimit.DELTA_F2_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "PER", "50, 0");
            Global._1LELimit.PER = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_F0_FN_MAX", "50, 0");
            Global._1LELimit.DELTA_F0_FN_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_F1_F0", "23, 0");
            Global._1LELimit.DELTA_F1_F0 = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_FN_MAX", "150, -150");
            Global._1LELimit.DELTA_FN_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "DELTA_FN_FN5_MAX", "20, 0");
            Global._1LELimit.DELTA_FN_FN5_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("1LE_LIMIT", "PER_NUM", "500");
            Global._1LELimit.PER_NUM = Convert.ToInt32(sTemp.ToString().Trim());
            #endregion

            #region LE2 LIMIT
            sTemp = iniParser.GetSetting("2LE_LIMIT", "POW", "12, -4");
            Global._2LELimit.POW = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_F1_AVG", "275, 225");
            Global._2LELimit.DELTA_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_F2_AVG", "275, 225");
            Global._2LELimit.DELTA_F2_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_F2_MAX", "275, 185");
            Global._2LELimit.DELTA_F2_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "INIT_FREQ_ERR", "150, -150");
            Global._2LELimit.INIT_FREQ_ERR = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_F2_F1_AVG", "1.1, 0.8");
            Global._2LELimit.DELTA_F2_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "PER", "50, 0");
            Global._2LELimit.PER = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_F0_FN_MAX", "50, 0");
            Global._2LELimit.DELTA_F0_FN_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_F1_F0", "23, 0");
            Global._2LELimit.DELTA_F1_F0 = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_FN_MAX", "150, -150");
            Global._2LELimit.DELTA_FN_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "DELTA_FN_FN5_MAX", "20, 0");
            Global._2LELimit.DELTA_FN_FN5_MAX = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("2LE_LIMIT", "PER_NUM", "500");
            Global._2LELimit.PER_NUM = Convert.ToInt32(sTemp.ToString().Trim());
            #endregion

            #region LR LIMIT
            sTemp = iniParser.GetSetting("LR_LIMIT", "POW", "12, -4");
            Global._LRLimit.POW = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("LR_LIMIT", "INIT_FREQ_ERR", "150, -150");
            Global._LRLimit.INIT_FREQ_ERR = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("LR_LIMIT", "PER", "50, 0");
            Global._LRLimit.PER = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            sTemp = iniParser.GetSetting("LR_LIMIT", "PER_NUM", "500");
            Global._LRLimit.PER_NUM = sTemp.ToString().Trim();

            sTemp = iniParser.GetSetting("LR_LIMIT", "DELTA_F1_AVG", "275, 225");
            Global._LRLimit.DELTA_F1_AVG = sTemp.ToString().Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            #endregion

            return true;
        }
        public static int FreqToChannel(int freq)
        {
            if (freq < 3000)
            {
                if (freq == 2484)
                    return 14;
                else
                    return ((freq - 2412) / 5 + 1);
            }
            else if ((freq >= 4915) && (freq <= 4980))
            {
                return 183 + (freq - 4915) / 5;
            }
            else if ((freq >= 5035) && (freq <= 5885))
            {
                return 7 + (freq - 5035) / 5;
            }
            else
            {
                return -1;
            }
        }
        public static int ChannelToFreq(int band, int channel)
        {
            if (band == 0)
            {
                if (channel == 14)
                    return 2484;
                else
                    return ((channel - 1) * 5 + 2412);
            }
            else
            {
                //183 - 196
                if ((channel >= 183) && (channel <= 196))
                {
                    return ((channel - 183) * 5 + 4915);
                }
                else if ((channel >= 7) && (channel <= 177))
                {
                    return ((channel - 7) * 5 + 5035);
                }
                else
                    return -1;
            }
        }
        public static double Attn_interpolate(int freq, int freq_min, int freq_max, double attn_min, double attn_max)
        {
            return (attn_min + ((attn_max - attn_min) / (freq_max - freq_min)) * (freq - freq_min));
        }
        public static bool TuneTxXtal(int freqMHz, double freqErr, ref int xtalcap)
        {
            double up_limit = Global.calXtal.TargetPPM;
            double lo_limit = Global.calXtal.TargetPPM * -1;
            try
            {
                if ((freqErr >= lo_limit) && (freqErr <= up_limit))
                {
                    return false;
                }
                else
                {
                    //double dOffsetIndex = (targetFreqOffsetPpm - freqErr) * freqMHz / Global.calXtal.StepHZ;

                    double dOffsetIndex = xtalcap - (freqErr * freqMHz) / Global.calXtal.StepHZ;

                    int index_offset = (int)dOffsetIndex;

                    xtalcap = index_offset;

                    //if (index_offset < -20)
                    //    index_offset = -20;
                    //else if (index_offset < -10)
                    //    index_offset = -10;
                    //else if (index_offset > 20)
                    //    index_offset = 20;
                    //else if (index_offset > 10)
                    //    index_offset = 10;

                    //if (index_offset == 0)
                    //{
                    //    if (dOffsetIndex > 0)
                    //        index_offset = 1;
                    //    else
                    //        index_offset = -1;
                    //}

                    //xtalcap += index_offset;

                    //0x0~0x7F
                    if (xtalcap < 0)
                        xtalcap = 0;
                    else if (xtalcap > 127)
                        xtalcap = 127;

                    return true;
                }
            }
            catch (Exception E)
            {
                SetAppendText("TuneTxXtal Fail!!!\r\n" + E.Message, ConsoleColor.Red);
                return false;
            }
            
        }

        public static void SetAppendText(string text, ConsoleColor nColor)
        {
            Program.SetAppendText(text, nColor);
        }
        public static void SetAppendText(string text)
        {
            Program.SetAppendText(text);
        }
        public static int getMCSIndex(string datarate, ref PPDU ppdutype, ref int bandwidth)
        {
            if (datarate.Contains("40"))
                bandwidth = (int)Bandwidth.BANDWIDTH_40M;
            else
                bandwidth = (int)Bandwidth.BANDWIDTH_20M;


            #region legacy
            if (datarate.Contains("M") && !datarate.Contains("MCS"))
            {
                ppdutype = PPDU.Legacy;

                if (datarate.Contains("54M"))
                    return (int)AG_MCS.OFDM54;
                else if (datarate.Contains("48M"))
                    return (int)AG_MCS.OFDM48;
                else if (datarate.Contains("36M"))
                    return (int)AG_MCS.OFDM36;
                else if (datarate.Contains("24M"))
                    return (int)AG_MCS.OFDM24;
                else if (datarate.Contains("18M"))
                    return (int)AG_MCS.OFDM18;
                else if (datarate.Contains("12M"))
                    return (int)AG_MCS.OFDM12;
                else if (datarate.Contains("9M"))
                    return (int)AG_MCS.OFDM9;
                else if (datarate.Contains("6M"))
                    return (int)AG_MCS.OFDM6;
                else if (datarate.Contains("11M"))
                {
                    ppdutype = PPDU.CCK;
                    return (int)CCK_MCS.CCK11;
                }
                else if (datarate.Contains("5M"))
                {
                    ppdutype = PPDU.CCK;
                    return (int)CCK_MCS.CCK5;
                }
                else if (datarate.Contains("2M"))
                {
                    ppdutype = PPDU.CCK;
                    return (int)CCK_MCS.CCK2;
                }
                else if (datarate.Contains("1M"))
                {
                    ppdutype = PPDU.CCK;
                    return (int)CCK_MCS.CCK1;
                }
            }
            #endregion

            #region HT
            else if (datarate.Contains("HT"))
            {
                ppdutype = PPDU.HT_MF;

                if (datarate.Contains("MCS0"))
                    return (int)MCS.MCS0;
                else if (datarate.Contains("MCS1"))
                    return (int)MCS.MCS1;
                else if (datarate.Contains("MCS2"))
                    return (int)MCS.MCS2;
                else if (datarate.Contains("MCS3"))
                    return (int)MCS.MCS3;
                else if (datarate.Contains("MCS4"))
                    return (int)MCS.MCS4;
                else if (datarate.Contains("MCS5"))
                    return (int)MCS.MCS5;
                else if (datarate.Contains("MCS6"))
                    return (int)MCS.MCS6;
                else if (datarate.Contains("MCS7"))
                    return (int)MCS.MCS7;
                else
                {
                    SetAppendText("   Wrong DataRate: " + datarate + "\r\n", ConsoleColor.Red);
                    return 108;
                }
            }
            #endregion

            SetAppendText("   Wrong DataRate: " + datarate + "\r\n", ConsoleColor.Red);
            return 108;
        }
        public static int ConvertDataRateToSpec(string sDataRate)
        {
            if (sDataRate.Contains("5.5M"))
                return 9;
            else if (sDataRate.Contains("11M"))
                return 8;
            else if (sDataRate.Contains("9M"))
                return 6;
            else if (sDataRate.Contains("12M"))
                return 5;
            else if (sDataRate.Contains("18M"))
                return 4;
            else if (sDataRate.Contains("24M"))
                return 3;
            else if (sDataRate.Contains("36M"))
                return 2;
            else if (sDataRate.Contains("48M"))
                return 1;
            else if (sDataRate.Contains("54M"))
                return 0;
            else if (sDataRate.Contains("6M"))
                return 7;
            else if (sDataRate.Contains("1M"))
                return 11;
            else if (sDataRate.Contains("2M"))
                return 10;
            else if (sDataRate.Contains("MCS10"))
                return 10;
            else if (sDataRate.Contains("MCS11"))
                return 11;
            else if (sDataRate.Contains("MCS0"))
                return 0;
            else if (sDataRate.Contains("MCS1"))
                return 1;
            else if (sDataRate.Contains("MCS2"))
                return 2;
            else if (sDataRate.Contains("MCS3"))
                return 3;
            else if (sDataRate.Contains("MCS4"))
                return 4;
            else if (sDataRate.Contains("MCS5"))
                return 5;
            else if (sDataRate.Contains("MCS6"))
                return 6;
            else if (sDataRate.Contains("MCS7"))
                return 7;
            else if (sDataRate.Contains("MCS8"))
                return 8;
            else if (sDataRate.Contains("MCS9"))
                return 9;
            else
            {
                SetAppendText("   Wrong DataRate: " + sDataRate + "\r\n", ConsoleColor.Red);
                return -999;
            }
        }
        public static bool showResult(string item, double value, double upper, double lower, string unit)
        {
            bool bResult = true;
            string sStr = "";
            item = "   " + item.PadRight(20, ' ') + ":".PadRight(10, ' ');
            int length = 23;
            string strItem = item.PadRight(length, ' ');
            double dUpper = upper;
            double dLower = lower;
            if (unit == "" || unit == null) unit = "   ";
            if (value >= dLower && value <= dUpper)
            {
                if ((dLower == -999) && (dUpper == 999))
                    sStr = strItem + value.ToString("0.00").PadLeft(6, ' ') + " " + unit.PadLeft(3, ' ');
                else
                    sStr = strItem + value.ToString("0.00").PadLeft(6, ' ') + " " + unit.PadLeft(3, ' ') + "   (" + dUpper.ToString("0.00") + " ~ " + dLower.ToString("0.00") + ")\r\n";
                SetAppendText(sStr);
                bResult = true;
            }
            else
            {
                sStr = strItem + value.ToString("0.00").PadLeft(6, ' ') + " " + unit.PadLeft(3, ' ') + "   (" + dUpper.ToString("0.00") + " ~ " + dLower.ToString("0.00") + ")        fail\r\n";
                SetAppendText(sStr, ConsoleColor.Red);
                bResult = false;
            }

            return bResult;
        }
        public static bool checkfile(string file)
        {
            try
            {
                string system = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                DirectoryInfo root = new DirectoryInfo(system + "\\Sysnative\\drivers\\");

                //DirectoryInfo root = new DirectoryInfo("C:\\Windows\\System32\\drivers\\");
                FileInfo[] files = root.GetFiles();

                for (int i = 0; i < files.Length; i++)
                {
                    if (files[i].Name == file)
                        return true;
                }
            }
            catch
            {
                try
                {
                    DirectoryInfo root = new DirectoryInfo("C:\\Windows\\System32\\drivers\\");
                    FileInfo[] files = root.GetFiles();

                    for (int i = 0; i < files.Length; i++)
                    {
                        if (files[i].Name == file)
                            return true;
                    }
                }
                catch (Exception exx)
                {
                    SetAppendText("   checkfile ERR:" + exx.Message + "\r\n", ConsoleColor.Red);
                    return false;
                }
            }

            return false;
        }
        public static int getvalue(int txpower)
        {
            int x = 0;
            switch (txpower)
            {
                case 12:
                    x = 0x0C;
                    break;
                case 11:
                    x = 0x0B;
                    break;
                case 10:
                    x = 0x0A;
                    break;
                case 9:
                    x = 0x09;
                    break;
                case 8:
                    x = 0x08;
                    break;
                case 7:
                    x = 0x07;
                    break;
                case 6:
                    x = 0x06;
                    break;
                case 5:
                    x = 0x05;
                    break;
                case 4:
                    x = 0x04;
                    break;
                case 3:
                    x = 0x03;
                    break;
                case 2:
                    x = 0x02;
                    break;
                case 1:
                    x = 0x01;
                    break;
                case 0:
                    x = 0;
                    break;
                case -1:
                    x = 0xFF;
                    break;
                case -2:
                    x = 0xFE;
                    break;
                case -3:
                    x = 0xFD;
                    break;
                case -4:
                    x = 0xFC;
                    break;
                case -5:
                    x = 0xFB;
                    break;
                case -6:
                    x = 0xFA;
                    break;
                case -7:
                    x = 0xF9;
                    break;
                case -8:
                    x = 0xF8;
                    break;
                case -9:
                    x = 0xF7;
                    break;
                case -10:
                    x = 0xF6;
                    break;
                case -11:
                    x = 0xF5;
                    break;
                case -12:
                    x = 0xF4;
                    break;
                default:
                    //x = 8;
                    //PrintMessage(GUI_handle, "TX_GAIN_K SETTING ERROR", RESULT_AREA);
                    break;
            }
            return x;

        }
        public static void writelog(string text)
        {
            try
            {
                if (!Directory.Exists("./log")) Directory.CreateDirectory("./log");

                string sLogName = "./log/" + Global.start_time.ToString("yyyyMMdd_hh-mm-ss") + "_debug.txt";

                if (!File.Exists(sLogName))
                {
                    FileStream fsFile1 = File.Open(sLogName, FileMode.OpenOrCreate, FileAccess.Write);
                    StreamWriter srWriter = new StreamWriter(fsFile1);
                    srWriter.Write(text);
                    srWriter.Close();
                    fsFile1.Close();
                }
                else
                {
                    FileStream fsFile1 = File.Open(sLogName, FileMode.Append, FileAccess.Write);
                    StreamWriter srWriter = new StreamWriter(fsFile1);
                    srWriter.Write(text);
                    srWriter.Close();
                    fsFile1.Close();
                }
            }
            catch
            {

            }
        }
        public static bool parseTestattnCSV()
        {
            try
            {
                string attenfile = Environment.CurrentDirectory + "/Setup/path_loss.csv";
                string[] lines = File.ReadAllLines(attenfile);

                for (int i = 0; i < lines.Length; i++)
                {
                    string[] sSplit = lines[i].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    Global.testAttenCSV.Add(Convert.ToInt32(sSplit[0]), Convert.ToDouble(sSplit[1]));
                }
            }
            catch (Exception ex)
            {
                SetAppendText("   parseTestattnCSV Error: " + ex.Message + "\r\n", ConsoleColor.Red);
                return false;
            }

            return true;
        }
        public static double getAtten(int freq, string direction)
        {
            if (Global.testSetup.LOSS_METHOD == 0)//Fulian
            {
                int chain = 0;

                List<TEST_LOSS> x = new List<TEST_LOSS>();
                if (direction == "TX")
                    x = Global.lossListTX[chain];
                else
                    x = Global.lossListRX[chain];

                List<TEST_LOSS> lossList = x.OrderBy(t => t.frequency).ToList();//升序



                for (int i = 0; i < lossList.Count; i++)
                {
                    TEST_LOSS loss = lossList[i];

                    if (loss.frequency == freq) return loss.attenuation;
                }

                if (lossList[0].frequency > freq) return lossList[0].attenuation;
                if (lossList[lossList.Count - 1].frequency < freq) return lossList[lossList.Count - 1].attenuation;


                int key = 0;
                for (int i = 0; i < lossList.Count; i++)
                {
                    if (lossList[i].frequency > freq)
                    {
                        key = i;
                        break;
                    }
                }

                double atten = Attn_interpolate(freq, lossList[key - 1].frequency, lossList[key].frequency, lossList[key - 1].attenuation, lossList[key].attenuation);

                return Math.Round(atten, 2);
            }
            else
            {
                foreach (var item in Global.testAttenCSV)
                {
                    if (item.Key == freq) return Global.testAttenCSV[freq];
                }

                List<int> Keys = new List<int>(Global.testAttenCSV.Keys);
                if (Keys[0] > freq) return Global.testAttenCSV[Keys[0]];
                if (Keys[Keys.Count - 1] < freq) return Global.testAttenCSV[Keys[Keys.Count - 1]];

                int key = 0;
                for (int i = 0; i < Keys.Count; i++)
                {
                    if (Keys[i] > freq)
                    {
                        key = i;
                        break;
                    }
                }

                int freq_min = Keys[key - 1];
                int freq_max = Keys[key];

                double atten = Attn_interpolate(freq, freq_min, freq_max, Global.testAttenCSV[freq_min], Global.testAttenCSV[freq_max]);

                return Math.Round(atten, 2);
            }
        }
        public static string ByteToString(byte bByte)
        {
            int t1 = bByte;
            string s1 = ((char)t1).ToString(); //这个10进制转对应ASCII字符才有意义。

            return s1;
        }
    }
}
