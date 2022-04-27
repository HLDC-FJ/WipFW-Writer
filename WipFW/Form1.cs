using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using System.Collections;

using System.IO.Ports;
using System.Net;
using System.Net.NetworkInformation;




namespace WipFW
{
    public partial class Form1 : Form
    {
        private delegate void Delegate_write(string data);

        private string laststring = "";                 // シリアルデータ受信バッファ
        private int TaskNo = 0;                         // タスク番号保持
        private bool endflg = false;                    // 処理終了時 True

        public int timeOut = 0;

        public int sTimeOut = 0;

        public string sWork = "";
        public string mac = "";

        public string logfileName = "";

        enum FWTask
        {
            UBoot_Prompt,
            UBoot_Sure,
            UBoot_MyIP ,
            UBoot_ServerIP ,
            UBoot_Name,
            UBoot_NetCheck,
            UBoot_Send,
            UBoot_write,
            EraseLinux , 
            EStartPrompt,
            EErasePrompt ,
            EEndPrompt ,
            EraseReboot ,
            TFTP ,
            TFTP_Y ,
            TFTP_MyIP ,
            TFTP_ServerIP ,
            FWName ,
            FWNetCheck ,
            FWrite ,
            FWEnd ,
            ReStart,
            MemSize,
            PowerONCheck,
            OSCheck,
            MacCheck,
            MacGet,
        };










        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            System.Reflection.Assembly asm =
                System.Reflection.Assembly.GetExecutingAssembly();
            //バージョンの取得
            System.Version ver = asm.GetName().Version;
            this.Text = "WIP Firmwear Writer    Version:" + ver;


            // 通信ポート検出
            string[] portlist = SerialPort.GetPortNames();
            this.comboBox1.Items.Clear();
            foreach (string PortName in portlist)
            {
                comboBox1.Items.Add(PortName);
            }
            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }

            IPHostEntry ipHostInfo = Dns.Resolve(Dns.GetHostName());

            foreach (IPAddress targetipaddr in ipHostInfo.AddressList)
            {
                textBox3.Text = targetipaddr.ToString();
                break;
            }
        }


        #region COM Port Open / Close
        /*!
         * COMポート：接続を開始します。
         */
        private bool serialPortOpen(string portname)
        {
            try
            {
                serialPort1.BaudRate = 57600;       // ボーレート
                serialPort1.PortName = portname;    // 通信ポート番号
                serialPort1.NewLine = "\r";         // 改行コード
                serialPort1.Open();
                return (true);
            }
            catch (Exception)
            {
                /*
                MessageBox.Show("ポートが開かれてます", "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                */
                MessageControl("Error!!\nポートが開かれてます",-1);

                return (false);
            }
        }
        /****************************************************************************/
        /*!
         * COMポート：接続を終了します。
         */
        private void serialPortClose()
        {
            timer1.Enabled = false;
            serialPort1.Close();
        }

        #endregion

        #region メッセージ表示処理
        private void MessageControl(string dat , int flg)
        {
            string label = textBox4.Text;
            if ((label == "") || (label == null))
            {
                label = "Message";
            }
            Form2 f2 = new Form2();

            if (flg != 0)
            {
                f2.BackColor = Color.Red;
            } else
            {
                f2.BackColor = SystemColors.Control;
            }
            f2.Left = this.Left-20;
            f2.Top = this.Top+80;

            f2.Text = label;
            f2.label1.Text = dat;
            f2.ShowDialog();

        }
        #endregion
        #region メッセージ表示 debug
        private void button5_Click(object sender, EventArgs e)
        {
            MessageControl("シリアルデータ受信タイムアウト\n\n\n電源を切り最初から実行してください。",0);
        }
        #endregion




        #region TFTP DeviceIP 設定チェックボックス
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false)
            {
                textBox2.Enabled = false;
            } else
            {
                textBox2.Enabled = true;
            }
        }
        #endregion

        #region TFTP ServerIP 設定チェックボックス
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked == false)
            {
                textBox3.Enabled = false;
            } else
            {
                textBox3.Enabled = true;
            }
        }
        #endregion



        #region ファームウェアファイル選択関連
        private void button1_Click(object sender, EventArgs e)
        {
            string st;
            openFileDialog1.Filter = "バイナリ(*.bin)|*.bin|すべてのファイル(*.*)|*.*";
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                st = this.openFileDialog1.FileName;
                int i = st.LastIndexOf('\\');
                if (i >= 0)
                {
                    i = i + 1;
                    st = st.Substring(i, (st.Length - i));
                }
                this.label3.Text = st;
            }
        }

        private void label3_DragDrop(object sender, DragEventArgs e)
        {
            string[] filename =
                    (string[])e.Data.GetData(DataFormats.FileDrop, false);
            string st = filename[0];
            int i = st.LastIndexOf('\\');
            if (i >= 0)
            {
                i = i + 1;
                st = st.Substring(i, (st.Length - i));
            }
            this.label3.Text = st;
        }

        private void label3_DragEnter(object sender, DragEventArgs e)
        {
            //コントロール内にドラッグされたとき実行される
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                //ドラッグされたデータ形式を調べ、ファイルのときはコピーとする
                e.Effect = DragDropEffects.Copy;
            else
                //ファイル以外は受け付けない
                e.Effect = DragDropEffects.None;
        }
        #endregion

        #region U-Boot 関連
        private void button3_Click(object sender, EventArgs e)
        {
            string st;
            openFileDialog1.Filter = "バイナリ(*.bin)|*.bin|すべてのファイル(*.*)|*.*";
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                st = this.openFileDialog1.FileName;
                int i = st.LastIndexOf('\\');
                if (i >= 0)
                {
                    i = i + 1;
                    st = st.Substring(i, (st.Length - i));
                }
                this.label6.Text = st;
            }
        }

        private void label6_DragDrop(object sender, DragEventArgs e)
        {
            string[] filename =
                    (string[])e.Data.GetData(DataFormats.FileDrop, false);
            string st = filename[0];
            int i = st.LastIndexOf('\\');
            if (i >= 0)
            {
                i = i + 1;
                st = st.Substring(i, (st.Length - i));
            }
            this.label6.Text = st;
        }

        private void label6_DragEnter(object sender, DragEventArgs e)
        {
            //コントロール内にドラッグされたとき実行される
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                //ドラッグされたデータ形式を調べ、ファイルのときはコピーとする
                e.Effect = DragDropEffects.Copy;
            else
                //ファイル以外は受け付けない
                e.Effect = DragDropEffects.None;
        }
        #endregion

        #region ファームウェア書換シーケンス
        private void FWrite()
        {
            // 書込み処理
            bool flg = serialPortOpen(comboBox1.SelectedItem.ToString());
            if (flg == true)
            {
                label4.ForeColor = SystemColors.ControlText;
                label4.Text = "モジュール接続待ち";
                button2.Text = "中止";
                TaskNoClear();
                laststring = "";
            }
        }
        #endregion

        #region シリアルデータ受信処理
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            sTimeOut = 100;
            string data = serialPort1.ReadExisting();
            Invoke(new Delegate_write(serialRxDTask), new Object[] { data });
            if (endflg == true)
            {
                serialPortClose();
            }
        }
        #endregion

        #region タスク初期化処理
        private void TaskNoClear()
        {
            checkBox3.Checked = false;
            checkBox4.Checked = false;
            checkBox5.Checked = false;
            checkBox6.Checked = false;
            checkBox8.Checked = false;
            checkBox10.Checked = false;

            if (checkBox12.Checked == true)
            {
                TaskNo = (int)FWTask.MemSize;
            }
            else
            {
                if (checkBox9.Checked == true)
                {
                    TaskNo = (int)FWTask.UBoot_Prompt;
                }
                else
                {
                    if (checkBox7.Checked == true)
                    {
                        TaskNo = (int)FWTask.EraseLinux;
                    }
                    else
                    {
                        TaskNo = (int)FWTask.TFTP;
                    }
                }
            }

            progressBar1.Value = 0;
        }
        #endregion


        #region ファームウェア書換処理
        /*!
         * シリアル受信データ 表示＆タスク処理
         */
        private void serialRxDTask(string rxdat)
        {
            if (rxdat != null)
            {
                laststring += rxdat;             // 受信データ文字列連結

                if (timer1.Enabled == true)
                {
                    progressBar1.PerformStep();
                    label7.Text = progressBar1.Value.ToString();
                }

                switch (TaskNo)
                {
                    case (int)FWTask.UBoot_Prompt:
                        if (laststring.IndexOf("RESET MT7628 PHY") >= 0)
                        {
                            progressBar1.Value = 0;
                            checkBox3.Checked = true;
                            label4.ForeColor = SystemColors.ControlText;
                            label4.Text = "WM0006起動";
                            serialPort1.Write("9");
                            TaskNo = (int)FWTask.UBoot_Sure;
                            timeOut = 50;
                            sTimeOut = 100;
                            timer1.Enabled = true;
                        }
                        break;
                    case (int)FWTask.UBoot_Sure:
                        if (laststring.IndexOf("Are you sure") >= 0)
                        {
                            label4.Text = "U-Boot書換モード移行";
                            serialPort1.Write("y");
                            TaskNo = (int)FWTask.UBoot_MyIP;
                            timeOut = 50;
                        }
                        break;
                    case (int)FWTask.UBoot_MyIP:
                        if (laststring.IndexOf("Input device IP") >= 0)
                        {
                            label4.Text = "Device IP指定";
                            if (checkBox1.Checked == true)
                            {
                                for (int i = 0; i < 13; i++)
                                {
                                    serialPort1.Write("\x08");
                                    System.Threading.Thread.Sleep(10);
                                }
                                serialPort1.WriteLine(textBox2.Text);
                            }
                            else
                            {
                                serialPort1.WriteLine("");
                            }
                            timeOut = 50;
                            TaskNo = (int)FWTask.UBoot_ServerIP;
                        }
                        break;
                    case (int)FWTask.UBoot_ServerIP:
                        if (laststring.IndexOf("Input server IP") >= 0)
                        {
                            label4.Text = "Server IP指定";
                            if (checkBox2.Checked == true)
                            {
                                for (int i = 0; i < 13; i++)
                                {
                                    serialPort1.Write("\x08");
                                    System.Threading.Thread.Sleep(10);
                                }
                                serialPort1.WriteLine(textBox3.Text);
                            }
                            else
                            {
                                serialPort1.WriteLine("");
                            }
                            TaskNo = (int)FWTask.UBoot_Name;
                            timeOut = 50;
                        }
                        break;
                    case (int)FWTask.UBoot_Name:
                        if (laststring.IndexOf("Uboot filename") >= 0)
                        {
                            label4.Text = "U-Bootファイル指定";
                            serialPort1.WriteLine(label6.Text);
                            TaskNo = (int)FWTask.UBoot_NetCheck;
                            timeOut = 150;
                        }
                        break;
                    case (int)FWTask.UBoot_NetCheck:
                        if (laststring.IndexOf("Got") >= 0)
                        {
                            label4.Text = "U-Bootファイル転送";
                            serialPort1.WriteLine(label3.Text);
                            TaskNo = (int)FWTask.UBoot_Send;
                            timeOut = 400;
                        }
                        break;
                    case (int)FWTask.UBoot_Send:
                        if (laststring.IndexOf("done") >= 0)
                        {
                            checkBox10.Checked = true;
                            label4.Text = "U-Boot書込み!!";
                            TaskNo = (int)FWTask.UBoot_write;
                            timeOut = 400;
                        }
                        break;
                    case (int)FWTask.UBoot_write:
                        if (laststring.IndexOf("Done!") >= 0)
                        {
                            label4.Text = "U-Boot書込み完了";
                            serialPort1.WriteLine(label3.Text);
                            if (checkBox7.Checked == true)
                            {
                                TaskNo = (int)FWTask.EraseLinux;
                            }
                            else
                            {
                                TaskNo = (int)FWTask.TFTP;
                            }
                            timeOut = 500;
                        }
                        break;
                    case (int)FWTask.EraseLinux:
                        if (laststring.IndexOf("RESET MT7628 PHY") >= 0)
                        {
                            checkBox3.Checked = true;
                            label4.ForeColor = SystemColors.ControlText;
                            label4.Text = "WM0006起動";
                            serialPort1.Write("4");
                            TaskNo = (int)FWTask.EStartPrompt;
                            timeOut = 50;
                            sTimeOut = 100;
                            timer1.Enabled = true;
                            progressBar1.Value = 100;
                        }
                        break;

                    case (int)FWTask.EStartPrompt:
                        if (laststring.IndexOf("MT7628 #") >= 0)
                        {
                            label4.Text = "Erase Linux";
                            serialPort1.WriteLine("erase linux");
                            TaskNo = (int)FWTask.EErasePrompt;
                            timeOut = 50;
                        }
                        break;

                    case (int)FWTask.EErasePrompt:
                        if (laststring.IndexOf("Erase linux kernel block") >= 0)
                        {
                            checkBox4.Checked = true;
                            label4.Text = "Flash初期化";
                            TaskNo = (int)FWTask.EEndPrompt;
                            timeOut = 1200;
                        }
                        break;


                    case (int)FWTask.EEndPrompt:
                        if (laststring.IndexOf("MT7628 #") >= 0)
                        {
                            label4.Text = "RESET";
                            serialPort1.WriteLine("reset");
                            TaskNo = (int)FWTask.TFTP;
                            timeOut = 200;
                        }
                        break;

                    case (int)FWTask.TFTP:
                        if (laststring.IndexOf("RESET MT7628 PHY") >= 0)
                        {
                            checkBox3.Checked = true;
                            checkBox5.Checked = true;
                            label4.ForeColor = SystemColors.ControlText;
                            label4.Text = "ファーム書換モード移行";
                            serialPort1.Write("2");
                            TaskNo = (int)FWTask.TFTP_Y;
                            timeOut =50;
                            timer1.Enabled = true;
                            progressBar1.Value = 600;
                        }
                        break;

                    case (int)FWTask.TFTP_Y:
                        if (laststring.IndexOf("Are you sure") >= 0)
                        {
                            label4.Text = "ファーム書換モード移行";
                            serialPort1.Write("y");
                            TaskNo = (int)FWTask.TFTP_MyIP;
                            timeOut = 50;
                        }
                        break;

                    case (int)FWTask.TFTP_MyIP:
                        if (laststring.IndexOf("Input device IP") >= 0)
                        {
                            label4.Text = "Device IP指定";
                            if (checkBox1.Checked == true)
                            {
                                for (int i = 0; i < 13; i++)
                                {
                                    serialPort1.Write("\x08");
                                    System.Threading.Thread.Sleep(10);
                                }
                                serialPort1.WriteLine(textBox2.Text);
                            }
                            else
                            {
                                serialPort1.WriteLine("");
                            }
                            timeOut = 50;
                            TaskNo = (int)FWTask.TFTP_ServerIP;
                        }
                        break;

                    case (int)FWTask.TFTP_ServerIP:
                        if (laststring.IndexOf("Input server IP") >= 0)
                        {
                            label4.Text = "Server IP指定";
                            if (checkBox2.Checked == true)
                            {
                                for (int i = 0; i < 13; i++)
                                {
                                    serialPort1.Write("\x08");
                                    System.Threading.Thread.Sleep(10);
                                }
                                serialPort1.WriteLine(textBox3.Text);
                            }
                            else
                            {
                                serialPort1.WriteLine("");
                            }
                            TaskNo = (int)FWTask.FWName;
                            timeOut = 50;
                        }
                        break;

                    case (int)FWTask.FWName:
                        if (laststring.IndexOf("Kernel filename") >= 0)
                        {
                            label4.Text = "Binファイル指定";
                            serialPort1.WriteLine(label3.Text);
                            TaskNo = (int)FWTask.FWNetCheck;
                            timeOut = 150;
                        }
                        break;

                    case (int)FWTask.FWNetCheck:
                        if (laststring.IndexOf("Got") >= 0)
                        {
                            label4.Text = "Binファイル転送";
                            TaskNo = (int)FWTask.FWrite;
                            timeOut = 1200;
                        }
                        break;

                    case (int)FWTask.FWrite:
                        if (laststring.IndexOf("done") >= 0)
                        {
                            checkBox6.Checked = true;
                            label4.Text = "Flash書込み!!";
                            TaskNo = (int)FWTask.FWEnd;
                            timeOut = 1200;
                        }
                        break;

                    case (int)FWTask.FWEnd:
                        if (laststring.IndexOf("Done!") >= 0)
                        {
                            checkBox8.Checked = true;
                            label4.Text = "再起動";
                            if (checkBox11.Checked == false)
                            {
                                TaskNo = (int)FWTask.ReStart;
                                timeOut = 200;
                            }
                            else
                            {
                                TaskNo = (int)FWTask.MacCheck;
                                timeOut = 600;
                            }
                        }
                        break;

                    case (int)FWTask.ReStart:
                        if (laststring.IndexOf("Starting kernel") >= 0)
                        {
                            label4.Text = "書込み完了!!\nOK Next!!";
                            timer1.Enabled = false;
                            progressBar1.Value = progressBar1.Maximum;
                            System.Media.SystemSounds.Beep.Play();

                            sWork = textBox4.Text + "書込み完了!!\n\n電源OFF!!";
                            MessageControl(sWork,0);

                            serialPort1.DiscardInBuffer();

                            TaskNoClear();
                        }
                        break;

                    case (int)FWTask.MemSize:
                        if (laststring.IndexOf("DRAM:  64 MB") >= 0)
                        {
                            progressBar1.Value = 0;
                            checkBox3.Checked = true;
                            label4.ForeColor = SystemColors.ControlText;
                            label4.Text = "WM0006起動";
                            LogFileTask("Board: Ralink APSoC DRAM,64MB");
                            TaskNo = (int)FWTask.PowerONCheck;
                            timeOut = 100;
                            timer1.Enabled = true;
                        }
                        break;

                    case (int)FWTask.PowerONCheck:
                        if (laststring.IndexOf("RESET MT7628 PHY") >= 0)
                        {
                            checkBox3.Checked = true;
                            label4.ForeColor = SystemColors.ControlText;
                            label4.Text = "メモリ容量確認";
                            serialPort1.Write("3");
                            TaskNo = (int)FWTask.OSCheck;
                            timeOut = 150;
                            timer1.Enabled = true;
                        }
                        break;

                    case (int)FWTask.OSCheck:
                        if (laststring.IndexOf("MIPS OpenWrt Linux-3.18.36") >= 0)
                        {
                            progressBar1.Value = 0;
                            checkBox3.Checked = true;
                            label4.ForeColor = SystemColors.ControlText;
                            label4.Text = "OS確認";
                            serialPort1.Write("");
                            LogFileTask("OpenWrt,Linux-3.18.36");
                            TaskNo = (int)FWTask.MacCheck;
                            timeOut = 600;
                            timer1.Enabled = true;
                        }
                        break;



                    case (int)FWTask.MacCheck:
#if true
                        if (laststring.IndexOf("Main bssid =") >= 0)
                        {
                            sWork = laststring.Substring(laststring.IndexOf("Main bssid ="));
                            if (sWork.IndexOf("\n") >= 0)
                            {
                                mac = sWork.Substring(13, 17);
                                mac = mac.Replace(":", "");
                                LogFileTask("OK," + mac);

                                timer1.Enabled = false;
                                if (checkBox12.Checked == false)
                                {
                                    label4.Text = "書込み完了!!\nOK Next!!";

                                    System.Media.SystemSounds.Beep.Play();
                                    sWork = textBox4.Text + "書込み完了!!\n\n電源断 → シール → 外す → OK\n\n MACアドレス: " + mac;
                                }
                                else
                                {
                                    label4.Text = "確認完了!!\nOK Next!!";

                                    System.Media.SystemSounds.Beep.Play();
                                    sWork = textBox4.Text + "確認完了!!\n\n電源断 → 外す → OK\n\n MACアドレス: " + mac;
                                }

                                MessageControl(sWork, 0);
                                serialPort1.DiscardInBuffer();

                                TaskNoClear();
                            }
                        }
#else
                        if (laststring.IndexOf("Please press Enter to activate this console.") >= 0)
                        {
                            // cat /etc/wireless/mt7628/mt7628.dat | grep 'MacAddress'
                            // ifconfig | grep 'HWaddr'
                            serialPort1.WriteLine("");
                            serialPort1.WriteLine("");
                            System.Threading.Thread.Sleep(5000);
                            serialPort1.WriteLine("cat /etc/wireless/mt7628/mt7628.dat | grep 'MacAddress'");
                            TaskNo = (int)FWTask.MacGet;
                        }
#endif
                        break;

                    case (int)FWTask.MacGet:
                        if (laststring.IndexOf("MacAddress=") >= 0)
                        {
                            sWork = laststring.Substring(laststring.IndexOf("MacAddress=")+11);
                            if (sWork.IndexOf("\n") >= 0)
                            {
                                sWork = sWork.Substring(0, 17);
                                LogFileTask("OK,"+sWork);
                                label4.Text = "書込み完了!!\nOK Next!!";
                                timer1.Enabled = false;

                                progressBar1.Value = progressBar1.Maximum;
                                System.Media.SystemSounds.Beep.Play();

                                sWork = textBox4.Text + "書込み完了!!\n\n電源OFF!!";
                                MessageControl(sWork, 0);
                                serialPort1.DiscardInBuffer();

                                TaskNoClear();
                            }
                        }
                        break;

                    default:
                        break;
                }

                if ((laststring.IndexOf("\n\r") >= 0) || (laststring.IndexOf("\r\n") >= 0))
                {
                    laststring = laststring.Replace("\n\r", "\n");
                    laststring = laststring.Replace("\r\n", "\n");
                    string[] rxd = laststring.Split('\n');
                    int cnt = rxd.Length -1;
                    for (int i = 0; i < cnt; i++)
                    {
                        textBox1.AppendText(rxd[i]+"\r\n");
                    }
                    laststring = rxd[cnt];
                }

            }
        }
#endregion






        // ファームウェア書込み開始
        private void button2_Click(object sender, EventArgs e)
        {
            if (label4.Text == "停止中")
            {
                int errflg = 1;         // エラーフラグ

                if ((label3.Text == "") || (label3.Text == null))
                {
                    //MessageBox.Show("バイナリファイルを選択してください");
                    MessageControl("Error!!\n\nバイナリファイルを選択してください",-1);
                }
                else
                {
                    if (checkBox9.Checked == true)
                    {
                        if ((label6.Text == "") || (label6.Text == null))
                        {
                            //MessageBox.Show("Error!!\nU-Bootファイルを選択してください");
                            MessageControl("Error!!\n\nU-Bootファイルを選択してください",-1);
                        }
                        else
                        {
                            errflg = 0;
                        }

                    } else
                    {
                        errflg = 0;
                    }
                }

                if (checkBox11.Checked == true)
                {
                    if (logfileName == "")
                    {
                        errflg = 1;
                        MessageControl("Error!!\n\nLogファイルフォルダ未指定", -1);
                    }
                }


                if ((errflg == 0) && (comboBox1.SelectedIndex == -1))
                {
                    errflg = 1;

                    /*
                    DialogResult result = MessageBox.Show("パラメータ異常!!",
                            "確認",
                            MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Exclamation,
                            MessageBoxDefaultButton.Button1);
                    */
                    MessageControl("Error!!\n\nパラメータ異常!!",-1);
                }

                if (errflg == 0)
                {
                    if (checkBox12.Checked == true)
                    {
                        LogFileTask("WM0006 起動確認開始");
                    }
                    else
                    {
                        if (checkBox11.Checked == true)
                        {
                            LogFileTask("ファームウェア書込み作業開始");
                            LogFileTask("### 設定 ###");
                            LogFileTask("ファームウェア," + label3.Text);
                            LogFileTask("U-Boot書込み有効無効," + checkBox9.Checked.ToString());
                            LogFileTask("U-Bootファイル名," + label6.Text);
                            LogFileTask("Erase Linux有効無効," + checkBox7.Checked.ToString());
                            LogFileTask("############");
                        }
                    }
                    textBox1.Clear();
                    FWrite();
                }
            } else
            {
                // 動作中のはず
                if (button2.Text == "中止")
                {
                    DialogResult result = MessageBox.Show("中断しますか？",
                    "確認",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                    if (result == DialogResult.OK)
                    {
                        timer1.Enabled = false;
                        label4.Text = "停止中";
                        button2.Text = "開始";

                        if (checkBox11.Checked == true)
                        {
                            LogFileTask("ファームウェア書込み作業終了");
                        }
                        textBox1.Clear();
                        TaskNoClear();
                        serialPort1.Close();
                    }

                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (checkBox11.Checked == true)
            {
                if (logfileName != "")
                {
                    LogFileTask("=== アプリ終了 ===");
                }
            }

            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
        }

#region Logファイル書込み
        private void LogFileTask(string dat)
        {
            string data = "";
            // 書込み用ファイルオープン
            FileStream FS_logfile = new FileStream(logfileName, FileMode.Append, FileAccess.Write);
            StreamWriter SR_logfile = new StreamWriter(FS_logfile, Encoding.GetEncoding("UTF-8"));

            data = DateTime.Now.ToString("yyyy/MM/dd,HH:mm:ss");
            data = data + "," + dat.Replace("\n", "");

            SR_logfile.WriteLine(data);

            SR_logfile.Close();
            FS_logfile.Close();
        }
        #endregion

#region タイマ処理
        private void timer1_Tick(object sender, EventArgs e)
        {
            int errflg = 0;

            timeOut--;
            if (timeOut <= 0)
            {
                errflg = 1;
            }

            sTimeOut--;
            if (sTimeOut <= 0)
            {
                errflg = 2;
            }


            if (errflg != 0)
            {
                timer1.Enabled = false;
                System.Media.SystemSounds.Hand.Play();

                switch (TaskNo)
                {
                    case (int)FWTask.UBoot_Sure:
                        sWork = "Prompt返答なし";
                        break;
                    case (int)FWTask.UBoot_MyIP:
                        sWork = "Device IP\n入力Error";
                        break;
                    case (int)FWTask.UBoot_ServerIP:
                        sWork = "Server IP\n入力Error";
                        break;
                    case (int)FWTask.UBoot_Name:
                        sWork = "U-Boot\nファイル選択Error";
                        break;
                    case (int)FWTask.UBoot_NetCheck:
                        sWork = "U-Boot転送\n開始されず";
                        break;
                    case (int)FWTask.UBoot_Send:
                        sWork = "U-Boot転送\n完了せず";
                        break;
                    case (int)FWTask.UBoot_write:
                        sWork = "U-Boot書換\n完了せず";
                        break;
                    case (int)FWTask.EStartPrompt:
                        sWork = "Prompt返答なし";
                        break;
                    case (int)FWTask.EErasePrompt:
                        sWork = "Eraseコマンド\n応答なし";
                        break;
                    case (int)FWTask.EEndPrompt:
                        sWork = "Eraseコマンド\n終了せず";
                        break;
                    case (int)FWTask.EraseReboot:
                        sWork = "Restコマンド\n応答せず";
                        break;
                    case (int)FWTask.TFTP:
                        sWork = "U-Boot起動せず";
                        break;
                    case (int)FWTask.TFTP_Y:
                        sWork = "ファーム書換\n移行せず";
                        break;
                    case (int)FWTask.TFTP_MyIP:
                        sWork = "Device IP\n入力Error";
                        break;
                    case (int)FWTask.TFTP_ServerIP:
                        sWork = "Server IP\n入力Error";
                        break;
                    case (int)FWTask.FWName:
                        sWork = "ファームウェア\nファイル選択Error";
                        break;
                    case (int)FWTask.FWNetCheck:
                        sWork = "ファイル転送\n開始されず";
                        break;
                    case (int)FWTask.FWrite:
                        sWork = "ファイル転送\n完了せず";
                        break;
                    case (int)FWTask.FWEnd:
                        sWork = "ファーム書換\n完了せず";
                        break;
                    case (int)FWTask.ReStart:
                        sWork = "Kernel起動せず";
                        break;
                    case (int)FWTask.MemSize:
                        sWork = "メモリサイズNG";
                        break;
                    case (int)FWTask.OSCheck:
                        sWork = "OS NG";
                        break;
                    default:
                        sWork = "";
                        break;
                }

                if (checkBox11.Checked == true)
                {
                    LogFileTask("Error,"+sWork);
                }

                switch (errflg)
                {
                    case 1:
                        //MessageBox.Show("タイムアウト\n電源を切り最初から実行してください。", "「" + textBox4.Text + "」"+"Timeout: " +sWork , MessageBoxButtons.OK, MessageBoxIcon.Error);
                        MessageControl("タイムアウト\n\n電源を切り最初から実行してください。",-1);
                        break;

                    case 2:
                        //MessageBox.Show("シリアルデータ\n受信タイムアウト", "「" + textBox4.Text + "」"+"異常:" +sWork+"「", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        MessageControl("シリアルデータ\n\n受信タイムアウト\n電源を切り最初から実行してください。",-1);
                        break;
                }

                TaskNoClear();
                label4.ForeColor = Color.Red;
                label4.Text = "Error!!\n" + sWork;

            }
        }
#endregion

#region Eraselinux 選択チェックボックス
        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox7.Checked == true)
            {
                checkBox4.Visible = true;
            }
            else
            {
                checkBox4.Visible = false;
            }
        }
#endregion

#region Log保存用フォルダ設定
        private void button4_Click(object sender, EventArgs e)
        {
            this.saveFileDialog1.Filter = "logファイル(*.log)|*.log|csvファイル(*.csv)|*.csv|Textファイル(*.txt)|*.txt|すべてのファイル(*.*)|*.*";
            this.saveFileDialog1.FileName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
            if (this.saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                logfileName = this.saveFileDialog1.FileName;
            } else
            {
                logfileName = "";
            }

            this.label8.Text = logfileName;



            /*
                        FolderBrowserDialog fbdialog = new FolderBrowserDialog();
                        fbdialog.Description = "Log保存場所";
                        fbdialog.SelectedPath = @"c:";
                        fbdialog.ShowNewFolderButton = true;
                        if (fbdialog.ShowDialog() == DialogResult.OK)
                        {
                            logfileName = fbdialog.SelectedPath;
                            if (logfileName.EndsWith("\\") == false)
                            {
                                logfileName = logfileName + "\\";
                            }
                            if (textBox5.Text != "" && textBox5.Text != null)
                            {
                                logfileName = logfileName + textBox5.Text + "_"  + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                            }
                            else
                            {
                                logfileName = logfileName + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                            }
                            label8.Text = logfileName;
                        }
                        fbdialog.Dispose();
            */
        }
        #endregion


        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox9.Checked == true)
            {
                checkBox10.Visible = true;
            }
            else
            {
                checkBox10.Visible = false;
            }
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox11.Checked == true)
            {
                button4.Enabled = true;
            }
            else
            {
                button4.Enabled = false;
            }
        }

        private void checkBox12_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox12.Checked == true)
            {
                checkBox7.Checked = false;
                checkBox9.Checked = false;
                checkBox11.Checked = true;
            }
        }
    }
}
