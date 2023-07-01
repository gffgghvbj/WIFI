using System;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;



namespace WIFI
{

    public partial class Form1 : Form
    {

        bool s = true;
        bool o = true;
        bool p = true;

        //客服端线程创建
        TcpClient tcpClient;
        NetworkStream ns;
        Thread receiveThread;

        //服务端线程创建
        Thread thread1;
        Thread thread2;
        Socket socket1 = null;          //创建负责监听客户端的套接字
        Socket socket1Accept = null;    //创建负责和客户端通信的套接字

        private delegate void SetText(string text);//A声明一个带参数委托，用于控件线程间操作访问


        public Form1()//设计器生成窗体
        {

            InitializeComponent();

        }

        private void Form1_Load(object sender, EventArgs e)
        {


            

            string hostName = Dns.GetHostName(); //获取主机名

            IPAddress[] ipadrlist = Dns.GetHostAddresses(hostName);  
            foreach (IPAddress ip in ipadrlist)  
            {  
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    textBox1.Text = (ip.ToString());                
            }  


            SearchAndAddSerialToCombox(serialPort1, comboBox1);//获取串口

            serialPort1.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);

            
            
            //chart1.Series[0].ChartType=SeriesChartType.Point;//设置为散点图

            chart1.Series[0].BorderWidth = 5;//设置边框或者线的宽度（以像素为单位）

            chart1.ChartAreas[0].AxisY.Interval = 20;//Y轴间隔大小

            chart1.ChartAreas[0].AxisX.Interval = 6;//Y轴间隔大小

            chart1.ChartAreas[0].AxisY.LabelStyle.Interval = 20;//Y轴标签间隔大小

            chart1.ChartAreas[0].AxisY.Minimum = -55;//Y轴最小值

            chart1.ChartAreas[0].AxisY.Maximum = 100;//Y轴最大值

            chart1.ChartAreas[0].AxisX.Interval = 1;// 显示X轴所有数据不缩放

            chart1.Series[0].IsValueShownAsLabel = false;//标签显示数据点的值

        }

        private void button1_Click(object sender, EventArgs e)//【开启服务】按钮
        {
                try
                {
                    socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);//1.创建一个Socket实例,参数(使用ipv4,发送的是数据流,使用Tcp协议)

                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(textBox1.Text.Trim()), int.Parse(textBox2.Text.Trim()));//2.创建一个网络终结点，包含ip地址，跟端口号。//Trim()移除字符串两侧的空白字

                    socket1.Bind(endPoint); //3.Socket绑定网络终结点

                    socket1.Listen(10);//设置监听队列长度限制为10


                    thread1 = new Thread(funcAcceptInfo);//创建线程thread1，监听客户端接入，委托函数funcAcceptInfo

                    thread1.IsBackground = true;//线程设置为后台运行,关闭窗口既可退出

                    thread1.Start();//启动线程

                    functextBox1("开启监听");//追加文本到文本框

                    //button1.Enabled = false;
                }
                catch
                {
                    MessageBox.Show("请重新打开程序");
                }

        }
        private void funcAcceptInfo()//监听客户端接入（线程thread1）
        {

            while (true)
            {
                try
                {
                    socket1Accept = socket1.Accept();//Accept 从侦听套接字的连接请求队列中同步提取第一个挂起的连接请求，然后创建并返回新的 Socket

                    functextBox1("客户端" + socket1Accept.RemoteEndPoint.ToString() + "已连接");//获取接入客户端的IP和端口


                    thread2 = new Thread(new ParameterizedThreadStart(funcServerRecMsg));//创建线程thread2，接收客户端发来的信息，委托（含有参数）

                    thread2.IsBackground = true;

                    thread2.Start(socket1Accept);//启动线程，函数funcServerRecMsg（socket）
                }
                catch
                {
                    socket1.Close();
                }

            }

        }


        private void funcServerRecMsg(object socketClientPara)// 接收客户端发来的信息（线程thread2）
        {

            Socket socket = socketClientPara as Socket;//创建套接字并将参数（socket1Accept）付给他

            try
            {
                while (true)
                {

                    byte[] arrServerRecMsg = new byte[1024 * 1024 * 1]; //创建一个内存缓冲区 其大小为1024*1024字节  即1M               

                    int length = socket.Receive(arrServerRecMsg);//将接收到的信息存入到内存缓冲区,并返回其字节数组的长度

                    string strSRecMsg = Encoding.UTF8.GetString(arrServerRecMsg, 0, length); //把接收到的字节数组转成字符串strSRecMsg

                    strSRecMsg = Regex.Replace(strSRecMsg, @"[^\d.\d]", "");//取出其中的温度值

                    //functextBox1("接受数据:" + strSRecMsg);下边方法代替

                    #region//=========================分析数据===============================================

                    string[] array = Regex.Split(strSRecMsg, "°C", RegexOptions.IgnoreCase);//使用双字符分隔

                    float[] pitch = new float[array.Length];

                    bool x = false;

                    for (int i = 0; i < array.Length; i++)
                    {

                        x = float.TryParse(array[i], out pitch[i]);//尝试转换数组array[0],如果失败x = false

                    }

                    if (x)
                    {

                        functextBox1(pitch.Average().ToString("#0.00")); //pitch.Average()求数组平均值；ToString("#0.00")转字符串并且保留2位小数

                    }

                    else
                    {

                        functextBox1(strSRecMsg);//函数目的:防止新线程调用主线程异常

                    }

                    #endregion//==============================================================================

                }
            }
            catch
            {
                functextBox1("客户端已断开");
                
                thread2.Abort();

            }

        }



        private void functextBox1(string str)//textBox3显示信息函数 A用于控件线程间操作访问
        {

            if (textBox3.InvokeRequired)
            {

                SetText d = new SetText(functextBox1);//实例化一个委托

                this.Invoke(d, new object[] { str });

            }

            else
            {

                textBox3.AppendText(DateTime.Now.ToString("hh:mm:ss") + ":" + str + "\r\n");//显示接收时间

                #region//=============================chart显示===========================================

                chart1.Series[0].Points.AddXY(DateTime.Now.ToLongTimeString().ToString(), str);// array.Average()求数组平均值

                if (chart1.Series[0].Points.Count > 20)
                {

                    chart1.Series[0].Points.RemoveAt(0);//移除第一个数据

                }

                #endregion//==============================================================================

            }

        }



        private void button2_Click(object sender, EventArgs e)//【发送】按钮
        {
            try
            {
                string SendMsg = textBox4.Text;

                if (SendMsg != "")
                {
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(SendMsg);

                    socket1Accept.Send(buffer);

                    functextBox1(DateTime.Now.ToString("hh:mm:ss") + "向客服端发送了:" + SendMsg);
                }
            }
            catch {

                MessageBox.Show("还未建立TCP连接");

            }

        }



      

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                if (s == true)
                {
                    s = false;
                    button5.Text = "led1关";
                    string mesg = "01aa";

                    send_message(mesg);
                }
                else
                {
                    s = true;
                    
                    button5.Text = "led1开";
                    
                    string mesg = "81aa";

                    send_message(mesg);
                }
            }
            catch {

                s = true;
                button5.Text = "led1开";
                MessageBox.Show("还未建立TCP连接");

            }
        }
        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                if (p == true)
                {
                    p = false;
                    
                    button6.Text = "led2关";
                    
                    string mesg = "02aa";

                    send_message(mesg);
                }
                else
                {
                    p = true;
                    
                    button6.Text = "led2开";
                    
                    string mesg = "82aa";

                    send_message(mesg);
                }
            }
            catch {

                p = true;
                button6.Text = "led2开";
                MessageBox.Show("还未建立TCP连接");
            
            }
         }
        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                if (o == true)
                {
                    o = false;
                    
                    button7.Text = "警报关";
                   
                    string mesg = "03aa";

                    send_message(mesg);
                }
                else
                {
                    o = true;
                    
                    button7.Text = "警报开";
                    
                    string mesg = "83aa";

                    send_message(mesg);
                }
            }
            catch
            {

                o = true;
                button7.Text = "警报开";
                MessageBox.Show("还未建立TCP连接");

            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.Close();
                    button8.Text = "开启串口";

                }
                catch
                {
                    button8.Text = "开启串口";
                }
            }
            else
            {
                try
                {
                    serialPort1.PortName = comboBox1.Text;
                    serialPort1.Open();
                    button8.Text = "关闭串口";
                }
                catch
                {
                    MessageBox.Show("串口打开失败");
                }
            }
        }
        private void SearchAndAddSerialToCombox(SerialPort myport, ComboBox mybox)
        {
            //string[] mystring = new string[20];
            string buffer;
            //int count = 0;
            mybox.Items.Clear();
            for (int j = 1; j < 20; j++)
            {
                comboBox2.Items.Add(j*2400);
            }
            comboBox1.Text = "COM1";//串口号多额默认值
            comboBox2.Text = "2400";
            for (int i = 1; i < 20; i++)
            {
                try
                {
                    buffer = "COM" + i.ToString();
                    myport.PortName = buffer;
                    myport.Open();
                    //mystring[count] = buffer;
                    mybox.Items.Add(buffer);
                    myport.Close();
                    //count++;
                }
                catch
                {
                    //count--;
                }
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            SearchAndAddSerialToCombox(serialPort1, comboBox1);
        }
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)//串口数据接收事件
        {
            try
            {
                if (!radioButton1.Checked)//如果接收模式为字符模式
                {
                    string str = serialPort1.ReadExisting();//字符串方式读
                    textBox7.AppendText(str);//添加内容
                }
                else
                { //如果接收模式为数值接收
                    byte data;
                    data = (byte)serialPort1.ReadByte();//此处需要强制类型转换，将(int)类型数据转换为(byte类型数据，不必考虑是否会丢失数据
                    string str = Convert.ToString(data, 16).ToUpper();//转换为大写十六进制字符串
                    textBox7.AppendText("0x" + (str.Length == 1 ? "0" + str : str) + " ");//空位补“0”   
                    //上一句等同为：if(str.Length == 1)
                    //                  str = "0" + str;
                    //              else 
                    //                  str = str;
                    //              textBox1.AppendText("0x" + str);
                }
            }
            catch {

                serialPort1.Close();
                button1.Text = "开启串口";
            
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            byte[] Data = new byte[1];
            if (serialPort1.IsOpen)//判断串口是否打开，如果打开执行下一步操作
            {
                if (textBox6.Text != "")
                {
                    try
                    {
                        serialPort1.WriteLine(textBox6.Text);//写数据
                    }
                    catch
                    {
                        MessageBox.Show("串口数据写入错误", "错误");//出错提示
                        serialPort1.Close();
                        button8.Enabled = false;//关闭串口按钮不可用
                    }
                }
            }
            else
                MessageBox.Show("请开启串口");
        }

        private void button11_Click(object sender, EventArgs e)
        {
            try
            {
                string mesg = ("aa" + textBox5.Text);

                if (mesg != "")
                {

                    send_message(mesg);

                    functextBox1("设置温度为:" + textBox5.Text + "℃");

                }
            }
            catch
            {

                MessageBox.Show("还未建立TCP连接");

            }
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            //socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            //IPAddress ipaddress = IPAddress.Parse(textBox8.Text);
            
           // EndPoint point = new IPEndPoint(ipaddress, int.Parse(textBox9.Text));
            
            //socket2.Connect(point);
            IPAddress ipaddress = IPAddress.Parse(textBox8.Text);

            Ping p = new Ping();

            if (p.Send(ipaddress).Status == IPStatus.Success)
            {
                tcpClient = new TcpClient();  //创建一个TcpClient对象，自动分配主机IP地址和端口号                      

                int point = Int32.Parse(textBox9.Text);
                //MessageBox.Show(ipaddr);
                try
                {
                    //发起TCP连接  
                    tcpClient.Connect(ipaddress, point);
                    if (tcpClient != null)
                    {
                        MessageBox.Show("连接服务器成功");

                        //获得绑定的网络数据流  
                        ns = tcpClient.GetStream();

                        Control.CheckForIllegalCrossThreadCalls = false;
                        //实例化并启动接受消息线程  
                        receiveThread = new Thread(receive_message);

                        receiveThread.IsBackground = true;

                        receiveThread.Start();
                    }
                    else
                    {
                        MessageBox.Show("连接服务器失败");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);

                }
            }
            else
            {
                MessageBox.Show("服务端ip不可用");
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                string SendMsg = textBox10.Text;

                if (SendMsg != "")
                {

                    send_message(SendMsg);

                }
            }
            catch
            {

                MessageBox.Show("还未建立TCP连接");

            }
        }
        private void receive_message()
        {
            while (IsOnline(tcpClient))
            {
                try
                {
                    //创建接收数据的字节流  
                    byte[] getData = new byte[1024];
                    //从网络流中读取数据  
                    ns.Read(getData, 0, getData.Length);
                    //将字节数组转换成文本形式  
                    string getMsg = Encoding.Default.GetString(getData);

                    getMsg = Regex.Replace(getMsg, @"[^\d.\d]", "");//取出温度

                    System.IO.File.AppendAllText(@"E:\temp.txt", DateTime.Now.ToString() + ":" + "温度" + getMsg +"℃" + "\r\n", Encoding.UTF8);

                    string msgStr = getMsg.Trim('\0');
                   
                    textBox3.AppendText(DateTime.Now.ToString("hh:mm:ss") + "温度：" + msgStr + "℃"+"\r\n");//显示接收时间

                    #region//=============================chart显示===========================================

                    chart1.Series[0].Points.AddXY(DateTime.Now.ToLongTimeString().ToString(), msgStr);// array.Average()求数组平均值

                    if (chart1.Series[0].Points.Count > 20)
                    {

                        chart1.Series[0].Points.RemoveAt(0);//移除第一个数据

                    }

                    #endregion//==============================================================================
                //    //if (!string.IsNullOrEmpty(msgStr))
                //        //processMsgInShow(msgStr);
                }
                catch (ThreadAbortException)
                {
                    //捕捉到线程被终止异常则表示是人为的断开TCP连接  
                    //不弹出错误提示
                    break;
                }
                catch (Exception e)
                {
                    //接受消息发生异常  
                    MessageBox.Show(e.Message);
                    //并释放相关资源  
                    if (ns != null)
                        ns.Dispose();
                    break;
 
 
                }

            }
            MessageBox.Show("网络中断");
            /// 当接收中断时
            if (ns != null)
                ns.Dispose();
 
 
        }
        private void send_message(string messageText)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(messageText); //将要发送的数据，生成字节数组。

            if(ns != null)
            functextBox1("向服务端发送了:" + messageText);
            //将数据写入到网络数据流中  
            ns.Write(buffer, 0, buffer.Length);
        }

        private void clientdisconnect()
        {
            if (tcpClient != null)
            {
                //关闭客服端
                tcpClient.Client.Close();
                //关闭TCP数据流
                ns.Close();
                //断开TCP连接  
                tcpClient.Close();
                //销毁绑定的网络数据流  
                ns.Dispose();
            }
            //销毁接受消息的线程 
            if (receiveThread != null)
            {
                receiveThread.Abort();
            }
        }

        private void serverdisconnect()
        {
            if (socket1 != null)
            {
                socket1.Close();

                functextBox1("关闭监听");
            }

            if (thread1 != null)
                thread1.Abort();

            if (thread2 != null)
                thread2.Abort();

        }

        public bool IsOnline(TcpClient c)
        {
            return !((c.Client.Poll(1000, SelectMode.SelectRead) && (c.Client.Available == 0)) || !c.Client.Connected);
        }

        private void button12_Click(object sender, EventArgs e)
        {
            MessageBox.Show("关闭连接成功");

            clientdisconnect();

        }

        private void button13_Click(object sender, EventArgs e)
        {
            serverdisconnect();
        }


        
    }

}