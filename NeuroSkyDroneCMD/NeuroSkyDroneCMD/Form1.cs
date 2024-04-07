using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;         // 추가
using System.Net;               // 추가
using System.Net.Sockets;       // 추가
using System.IO;                // 추가
using System.IO.Ports;          // for serial port

namespace NeuroSkyDroneCMD
{
    public partial class Form1 : Form
    {
        //uint x = 0;
        double x;

        int ctrl_state = 0; // 0 Netuail, 1 TakeOnOff, 2 Alatitude, 3 Lotation, 4 Noting
        int prev_state;
        int nothing_cnt = 0;
        int cmd_cnt = 0;
        int time_set = 4;       // default 4초
        int High_value = 0;  // Command Max Value
        int Low_value = 0;      // command low value
        int high_cnt = 0, low_cnt = 0;
        int front_cnt = 0, back_cnt = 0;
        int simulation_state = 0;
        int simulation_value = 0;


        bool in_air = false;
        public Form1()
        {
            InitializeComponent();
        }
        StreamReader streamReader;  // 데이타 읽기 위한 스트림리더
        StreamWriter streamWriter;  // 데이타 쓰기 위한 스트림라이터 
        StreamReader streamReader2;  // 데이타 읽기 위한 스트림리더
        StreamWriter streamWriter2;  // 데이타 쓰기 위한 스트림라이터 
        private delegate void AddTextDelegate(string strText); // 크로스 쓰레드 호출
        int att;
        double latitude, longitude;
        float yaw;
        float yaw_fake;
        float yaw_input;
        float yaw_offset=0;
        float yaw_shift=0;


        struct _EXENS_PROTOCOL
        {
            public Byte header1;
            public Byte header2;
            public Byte MsgLeng;
            public Byte MsgType;
            public Byte Src;
            public Byte Dest;

            public Byte Count;
            public Byte Attention;
            public Byte Signal;
            public Double Latitude;
            public Double Longitude;
            public float Degree;

            public Byte checksum;
            public Byte EOF1;
            public Byte EOF2;
        };

        _EXENS_PROTOCOL exens;
        private void button1_Click(object sender, EventArgs e)
        {
            if (timer1.Enabled)
            {
                timer1.Stop();
                timer2.Stop();
                button1.Text = "Start";
                cmd_cnt = 0;
                high_cnt = 0;
                low_cnt = 0;
                writeRichTextbox4("프로그램 정지 및 초기화");
                switch (ctrl_state)
                {
                    case 0:
                        break;
                    case 1:     // Stop 클릭시 초기화
                        writeRichlabel("명령대기");
                        in_air = false;    
                        break;
                    case 2:
                        writeRichlabel("명령대기");
                        break;
                    case 3:     
                        writeRichlabel("명령대기");
                        break;


                }
            }
            else
            {
                timer1.Start();
                timer2.Start();
                button1.Text = "Stop";

                writeRichTextbox4("프로그램 시작");
            }
            
        }
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            this.Invoke(new EventHandler(MySerialReceived));
        }
        private void MySerialReceived(object s, EventArgs e)  //여기에서 수신 데이타를 사용자의 용도에 따라 처리한다.
        {
            int ReceiveData = serialPort1.ReadByte();  //시리얼 버터에 수신된 데이타를 ReceiveData 읽어오기
            //richTextBox_received.Text = richTextBox_received.Text + string.Format("{0:X2}", ReceiveData);  //int 형식을 string형식으로 변환하여 출력
        }
        private void button4_Click(object sender, EventArgs e)
        {
            if (!serialPort1.IsOpen)  //시리얼포트가 열려 있지 않으면
            {
                serialPort1.PortName = comboBox1.Text;  //콤보박스의 선택된 COM포트명을 시리얼포트명으로 지정
                serialPort1.BaudRate = 115200;  //보레이트 변경이 필요하면 숫자 변경하기
                serialPort1.DataBits = 8;
                serialPort1.StopBits = StopBits.One;
                serialPort1.Parity = Parity.None;
                serialPort1.DataReceived += new SerialDataReceivedEventHandler(serialPort1_DataReceived); //이것이 꼭 필요하다

                serialPort1.Open();  //시리얼포트 열기

                writeRichTextbox4("포트가 열렸습니다.");
                comboBox1.Enabled = false;  //COM포트설정 콤보박스 비활성화
                button4.Text = "연결 종료";
            }
            else  //시리얼포트가 열려 있으면
            {
                button4.Text = "연결";
                writeRichTextbox4("연결 종료");
                serialPort1.Close();
                comboBox1.Enabled = true;       // 콤보박스 활성화
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            chart1.Series[0].Points.AddXY(x, att);
            //chart1.Series[0].Points.AddXY(x, 3*Math.Sin(5*x) +5*Math.Cos(3*x));

            if (chart1.Series[0].Points.Count > 100)
                chart1.Series[0].Points.RemoveAt(0);

            chart1.ChartAreas[0].AxisX.Minimum = chart1.Series[0].Points[0].XValue;
            chart1.ChartAreas[0].AxisX.Maximum = x;
            chart1.ChartAreas[0].AxisX.Interval = 1;
            chart1.ChartAreas[0].AxisY.Minimum = 0;
            chart1.ChartAreas[0].AxisY.Maximum = 100;
            //x += 0.1;
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.DataSource = SerialPort.GetPortNames();

            button1.Text = "Start";
            timer1.Tick += timer1_Tick;
            timer1.Interval = 100;
            timer2.Tick += timer2_Tick;
            timer2.Interval = 500;
            ctrl_state = 0;
            radioButton1.Checked = true;
            textBox3.Text = "0";
            textBox5.Text = "0";

            chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chart1.Series[0].LegendText = "Attention";

            

        }

        private void button2_Click(object sender, EventArgs e)  // '연결하기' 버튼이 클릭되면
        {
            Thread thread1 = new Thread(connect);  // Thread 객채 생성, Form과는 별도 쓰레드에서 connect 함수가 실행됨.
            thread1.IsBackground = true;           // Form이 종료되면 thread1도 종료.
            thread1.Start();
            Thread thread2 = new Thread(connect2);  // Thread 객채 생성, Form과는 별도 쓰레드에서 connect 함수가 실행됨.
            thread2.IsBackground = true;           // Form이 종료되면 thread1도 종료.
            thread2.Start(); // thread1 시작.
        }

        private void writeRichlabel(string data)  // richTextbox1 에 쓰기 함수
        {
            label4.Invoke((MethodInvoker)delegate { label4.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        private void writeRichTextbox1(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox1.Invoke((MethodInvoker)delegate { textBox1.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        private void writeRichTextbox2(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox2.Invoke((MethodInvoker)delegate { textBox2.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        
        // ------- Scroll text
        private void writeRichTextbox3(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox3.Invoke((MethodInvoker)delegate { textBox3.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        private void writeRichTextbox5(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox5.Invoke((MethodInvoker)delegate { textBox5.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        private void writeRichTextbox7(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox7.Invoke((MethodInvoker)delegate { textBox7.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        private void writeRichTextbox6(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox6.Invoke((MethodInvoker)delegate { textBox6.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        private void writeRichTextbox8(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox8.Invoke((MethodInvoker)delegate { textBox8.Text = data; }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
        }
        // ------- Debug Box
        private void writeRichTextbox4(string data)  // richTextbox1 에 쓰기 함수
        {
            textBox4.Invoke((MethodInvoker)delegate { textBox4.AppendText(data + "\r\n"); }); //  데이타를 수신창에 표시, 반드시 invoke 사용. 충돌피함.
            textBox4.Invoke((MethodInvoker)delegate { textBox4.ScrollToCaret(); });  // 스크롤을 젤 밑으로.
        }

        private void connect2()  // thread1에 연결된 함수. 메인폼과는 별도로 동작한다.
        {
            TcpListener tcpListener1 = new TcpListener(IPAddress.Parse("192.168.0.84"), int.Parse("3000")); //IPAdress 자기 IP 주소로 수정
            //TcpListener tcpListener1 = new TcpListener(IPAddress.Parse("103.218.162.87"), int.Parse("3500")); 
            tcpListener1.Start();  // 서버 시작
            writeRichTextbox4("서버1 준비...클라이언트 기다리는 중...");

            TcpClient tcpClient1 = tcpListener1.AcceptTcpClient(); // 클라이언트 접속 확인
            writeRichTextbox4("클라이언트1 연결됨...");

            streamReader = new StreamReader(tcpClient1.GetStream());  // 읽기 스트림 연결
            streamWriter = new StreamWriter(tcpClient1.GetStream());  // 쓰기 스트림 연결
            streamWriter.AutoFlush = true;  // 쓰기 버퍼 자동으로 뭔가 처리..

            while (tcpClient1.Connected)  // 클라이언트가 연결되어 있는 동안
            {

                string[] receiveData = streamReader.ReadLine().Split(',');  // 수신 데이타를 읽어서 receiveData1 변수에 저장
                                                                          //수신 데이터 어떤 형식으로 들어오는지 알아야함
                
                latitude = double.Parse(receiveData[0]);
                longitude = double.Parse(receiveData[1]);
                yaw_input = float.Parse(receiveData[2]);
                yaw_shift = yaw_input;
                if (yaw_offset > 0)
                {
                    if (yaw_input > -180 && yaw_input < -180 + yaw_offset)
                        yaw_shift = 360 + yaw_input;
                }
                else if (yaw_offset < 0)
                {
                    if (yaw_input < 180 && yaw_input > 180 + yaw_offset)
                        yaw_shift = -360 + yaw_input;
                }
                yaw = yaw_shift - yaw_offset;
                

                writeRichTextbox4(latitude.ToString());//latitude
                writeRichTextbox4(longitude.ToString());//longitude
                writeRichTextbox4(yaw.ToString());//yaw
                
                //writeRichTextbox4(trackBar4.Value.ToString());

                //BitConverter를 이용하여 데이터 Byte로 변환
                byte[] latBytes = BitConverter.GetBytes(latitude);
                byte[] lonBytes = BitConverter.GetBytes(longitude);
                byte[] yawBytes = BitConverter.GetBytes(yaw);
                byte[] yawfakeBytes = BitConverter.GetBytes(yaw_fake);

                //int startIdx = receiveData1.IndexOf(":");
                //string att_word = receiveData1.Substring(startIdx +1);
                //att = int.Parse(att_word);
                writeRichTextbox8(yaw.ToString());
                //writeRichTextbox1(att.ToString()); // 데이타를 수신창에 쓰기

                //writeRichTextbox4(startIdx.ToString());


                
                if(serialPort1.IsOpen)
                {
                    byte[] arr_send = new byte[34];
                    arr_send[0] = 0xF1; //헤더1
                    arr_send[1] = 0xD1; //헤더2 
                    arr_send[2] = 0x00; //메세지 크기
                    arr_send[3] = 0x01; //메세지 타입
                    arr_send[4] = 0xFF; //송신객체 ID
                    arr_send[5] = 0xC9; //FCC ID

                    arr_send[6] = (Byte)att; ; //rate
                    arr_send[7] = (Byte)ctrl_state; ; //Focus mode
                    arr_send[8] = (Byte)High_value; ; //Focus High
                    arr_send[9] = (Byte)Low_value; ; //Focus Low
                    arr_send[10] = (Byte)time_set; //Focus time

                    //데이터 arr_send에 삽입
                    Array.Copy(latBytes, 0, arr_send, 11, 8); //lat
                    Array.Copy(lonBytes, 0, arr_send, 19, 8); //lon
                    Array.Copy(yawBytes, 0, arr_send, 27, 4); //deg
                    //Array.Copy(yawfakeBytes, 0, arr_send, 27, 4); //deg

                    arr_send[31] = 0x00; //Checksum
                    arr_send[32] = 0xFF; //테일1
                    arr_send[33] = 0xFF; //테일2

                    

                    serialPort1.Write(arr_send, 0, arr_send.Length);
                }
                switch(ctrl_state)
                {
                    case 1:         // 이착륙모드의 경우 High Value만 검색해서 카운트
                        if (att >= High_value) cmd_cnt++;
                        else cmd_cnt = 0;
                        writeRichTextbox2(cmd_cnt.ToString());
                        break;
                    case 2:
                        if (att >= High_value)
                        {
                            high_cnt++;          // 상승 카운트만 증가
                            writeRichTextbox2(high_cnt.ToString());
                        }
                        else
                        {
                            if(att > Low_value)             // 중립 상태라면
                            {
                                high_cnt = 0;        // 카운트 초기화
                                low_cnt = 0;
                                writeRichTextbox2("0");
                            }
                            else
                            {
                                low_cnt++;                             // 하강 카운트만 증가
                                writeRichTextbox2(low_cnt.ToString());
                            }
                        }
                        break;


                    case 3:
                        if (att >= High_value)
                        {
                            front_cnt++;          // 전진 카운트만 증가
                            writeRichTextbox2(front_cnt.ToString());
                        }
                        else
                        {
                            if (att > Low_value)             // 중립 상태라면
                            {
                                front_cnt = 0;        // 카운트 초기화
                                back_cnt = 0;
                                writeRichTextbox2("0");
                            }
                            else
                            {
                                back_cnt++;                             // 후진 카운트만 증가
                                writeRichTextbox2(back_cnt.ToString());
                            }
                        }
                        break;

                   
                } 
            }
            Thread.Sleep(500);
        }
        private void connect()
        {
            TcpListener tcpListener2 = new TcpListener(IPAddress.Parse("127.0.0.1"), int.Parse("11133"));
            tcpListener2.Start();  // 서버 시작
            writeRichTextbox4("서버2 준비...클라이언트 기다리는 중...");

            TcpClient tcpClient2 = tcpListener2.AcceptTcpClient(); // 클라이언트 접속 확인
            writeRichTextbox4("클라이언트2 연결됨...");
            streamReader2 = new StreamReader(tcpClient2.GetStream());  // 읽기 스트림 연결
            streamWriter2 = new StreamWriter(tcpClient2.GetStream());  // 쓰기 스트림 연결
            streamWriter2.AutoFlush = true;  // 쓰기 버퍼 자동으로 뭔가 처리..
            while (tcpClient2.Connected)
            {
                string receiveData = streamReader2.ReadLine();
                int startIdx = receiveData.IndexOf(":");
                string att_word = receiveData.Substring(startIdx +1);
                if (simulation_state == 0)
                {
                    att = int.Parse(att_word);

                    writeRichTextbox1(att.ToString()); // 데이타를 수신창에 쓰기
                }
                else if (simulation_state == 1)
                {
                    att = simulation_value;

                    writeRichTextbox1(att.ToString());
                }
                x++;
            }

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if(ctrl_state != 0)
            { 
                writeRichTextbox4("중립 모드 선택");
                ctrl_state = 0;
                label4.Text = "수동조종";
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (ctrl_state != 1)
            { 
                writeRichTextbox4("이착륙 모드 선택");
                ctrl_state = 1;
                label4.Text = "명령대기";
                High_value = 90;
                trackBar1.Value = 90;
                textBox3.Text = "90";
                trackBar2.Value = 0;
                textBox5.Text = "0";
                trackBar3.Value = 4;
                textBox6.Text = "4";

            }
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (ctrl_state != 2)
            { 
                writeRichTextbox4("고도제어 모드 선택");
                ctrl_state = 2;
                label4.Text = "명령대기";
                High_value = 80;
                Low_value = 30;
                trackBar1.Value = 80;
                textBox3.Text = "80";
                trackBar2.Value = 30;
                textBox5.Text = "30";
                trackBar3.Value = 2;
                textBox6.Text = "2";
            }
        }
        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            if (ctrl_state != 3)
            {
                writeRichTextbox4("각도 거리 제어 모드 선택");
                ctrl_state = 3;
                label4.Text = "명령대기";
                High_value = 80;
                Low_value = 30;
                trackBar1.Value = 80;
                textBox3.Text = "80";
                trackBar2.Value = 30;
                textBox5.Text = "30";
                trackBar3.Value = 2;
                textBox6.Text = "2";
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            writeRichTextbox3(trackBar1.Value.ToString()); 
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            writeRichTextbox5(trackBar2.Value.ToString());
        }
        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            writeRichTextbox6(trackBar3.Value.ToString());
        }
        

        private void button3_Click(object sender, EventArgs e)      // 적용 버튼
        {
            High_value = int.Parse(textBox3.Text);
            writeRichTextbox4("최대값변경:" + High_value.ToString());
            Low_value  = int.Parse(textBox5.Text);
            writeRichTextbox4("최소값변경:" + Low_value.ToString());
            time_set = int.Parse(textBox6.Text);
            writeRichTextbox4("유지시간변경" + time_set.ToString());
        }

        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                trackBar1.Value = int.Parse(textBox3.Text);
            }
        }

        private void textBox5_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                trackBar2.Value = int.Parse(textBox5.Text);
            }
        }
        private void textBox6_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                trackBar3.Value = int.Parse(textBox6.Text);
            }
        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e) //yaw 초기화 버튼
        {
            yaw_offset = yaw_input;
        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void label9_Click(object sender, EventArgs e)
        {

        }

        private void label10_Click(object sender, EventArgs e)
        {

        }

        private void textBox6_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox8_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox7_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void trackBar5_Scroll(object sender, EventArgs e)
        {
            writeRichTextbox8(trackBar5.Value.ToString());
            yaw_fake = trackBar5.Value;
        }

        private void trackBar4_Scroll(object sender, EventArgs e)
        {
            if (simulation_state == 1)
            {
                writeRichTextbox7(trackBar4.Value.ToString());
                simulation_value = trackBar4.Value;
                
            }
            
        }
        private void radioButton8_CheckedChanged(object sender, EventArgs e)
        {
            if (simulation_state != 1)
            {
                simulation_state = 1;
                
            }

        }
        private void radioButton10_CheckedChanged(object sender, EventArgs e)
        {
            if (simulation_state != 0)
            {
                simulation_state = 0;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private void timer2_Tick(object sender, EventArgs e)
        {

            switch (ctrl_state)
            {
                case 0:
                    break;
                case 1:
                    if (cmd_cnt >= time_set)        // 만약 90 이상이 지정시간보다 지속된다면
                    { 
                        if (in_air == false)     // 착륙상태였다면
                        {
                            writeRichlabel("이륙");
                            in_air = true;
                        }
                        else
                        {
                            writeRichlabel("착륙");
                            in_air = false;
                        }
                    }
                    else
                    {
                        writeRichlabel("명령대기");

                    }
                    break;
                case 2:
                    if(high_cnt >= time_set)            // 상승상태
                    {
                        writeRichlabel("상승");
                      
                    }
                    else if(low_cnt >= time_set)        // 하강상태
                    {
                        writeRichlabel("하강");
                 
                    }
                    else
                    {
                        writeRichlabel("명령대기");
                    }
                    break;
                case 3:
                    if (front_cnt >= time_set)            // 전진상태
                    {
                        writeRichlabel("전진");
                    }
                    else if (back_cnt >= time_set)        // 후진상태
                    {
                        writeRichlabel("후진");
                    }
                    else
                    {
                        writeRichlabel("명령대기");
                    }
                    break;
            }
        }

        
    }
}
