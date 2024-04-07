#include <SoftwareSerial.h>
#include <ESP8266WiFi.h>
#include <stdio.h>
#include "MPU9250.h"
#include <math.h>
#include "eigen.h"     
#include <Eigen/LU>  
using namespace Eigen;  

SoftwareSerial gpsSerial(D5, D6);

#define N 200
#define pi 3.141592

void print_mtxf(const Eigen::MatrixXf& K);

//GPS & TCP variable
char gps_string[500];
char cnt = 0;
char checksum_cnt = 0;
int gga = 0;          // header GPGGA
char cunt = 0;
char header[6];
float gtime;
double lat;
char north_south;
double lon;
char east_west;
int fix1;
uint16_t nsatellite;
float hdop;
float alti;
char m1;
float wgs84;
char m2;
int Receive_state = 0; //0: header, 1: Data, 2: checksum
String str = "GPGGA";
const char* ssid = "iptime2"; // iptime3로 변경
const char* password = "control505!"; // 00000000 로 변경
//const char* ssid = "Galaxy S229809";
//const char* password = "00000001";
char gps_raw[] = "$GPGGA,064422.00,3732.52240,N,12704.79333,E,1,05,2.70,42.6,M,18.7,M,,*6D";
char gps_raw2[] = "GPGGA,064422.00,3732.52240,N,12704.79333,E,1,05,2.70,42.6,M,18.7,M,,*6D";
int num = 0;
char message[100];
WiFiClient client;

//MPU9250 variable

//----------------------declare for LSM method matrix----------------------//
float x[N];
float y[N];
float xy[N];
float sqx[N];
float sqy[N];
float A_f[N];
float A[N][5];
float B[N][1];
float X[5][1];
float A_T[5][N];
double InvA[5][5];
float C[5][5] = {0.0,} ; //(A'*A)
float D[5][5] = {0.0,} ; //inv(A'*A)
MatrixXf E(5,5); //(A'*A)
MatrixXf E_inv(5,5);  //inv(A'*A)
float F[5][N] = {0.0,}; //inv(A'*A)*A'
float G[5][1] = {0.0,};  //inv(A'*A)*A'*B
//----------------------declare for ellipse to circle----------------------//
float rot_x[N];
float rot_y[N];
float delta;
float b_a;
float c_a;
float d_a;
float e_a;
float f_a;
float C_x;
float C_y;
float w_Num;
float w_Den;
float w;
float h_Num;
float h_Den;
float h;
float rot_x_n;
float rot_y_n;
float sigma;
//----------------------declare for loop----------------------//
float X_h;
float Y_h;
float az;
MPU9250 IMU(Wire,0x68);
int status;

void getvalue(){
    for(int i=0; i<N; i++){
      IMU.readSensor();
      x[i] = IMU.getMagX_uT();
      y[i] = IMU.getMagY_uT();
      delay(20);   

      sqx[i] = x[i]*x[i];
      xy[i]  = x[i]*y[i];
      sqy[i] = y[i]*y[i];
      A_f[i] = 1;

      A[i][0] = xy[i];
      A[i][1] = sqy[i];
      A[i][2] = x[i];
      A[i][3] = y[i];
      A[i][4] = A_f[i];

      A_T[0][i] = A[i][0];
      A_T[1][i] = A[i][1];
      A_T[2][i] = A[i][2];
      A_T[3][i] = A[i][3];
      A_T[4][i] = A[i][4];

      B[i][0] = -sqx[i];   
    }  
}

void mag_calibrate_LSM(){
  // calculate -> (A'*A) 
  for(int k=0;k<5;k++){
    for(int j=0;j<5;j++){
      for(int i=0;i<N;i++){
        C[k][j] += A_T[k][i]*A[i][j];
      }
    }
  }
  // calculate -> inv(A'*A) with eigen header file
  E <<
      C[0][0],C[0][1],C[0][2],C[0][3],C[0][4],
      C[1][0],C[1][1],C[1][2],C[1][3],C[1][4],
      C[2][0],C[2][1],C[2][2],C[2][3],C[2][4],
      C[3][0],C[3][1],C[3][2],C[3][3],C[3][4],
      C[4][0],C[4][1],C[4][2],C[4][3],C[4][4];
  
  E_inv = E.inverse();

  // save -> D matrix
  for(int i=0;i<5;i++){
    for(int j=0;j<5;j++){
      D[i][j] = E_inv(i,j);
    }
  }
  
  // calculate -> inv(A'*A)*A' 
  for(int k=0;k<5;k++){
    for(int j=0;j<N;j++){
      for(int i=0;i<5;i++){
        F[k][j] += D[k][i]*A_T[i][j];
      }
    }
  }  

  // calculate -> inv(A'*A)*A'*B 
  for(int k=0;k<5;k++){
    for(int j=0;j<1;j++){
      for(int i=0;i<N;i++){
        G[k][j] += F[k][i]*B[i][j];
      }
    }
  }
}

void mag_distortion_calibrate(){
  b_a = G[0][0];
  c_a = G[1][0];
  d_a = G[2][0];
  e_a = G[3][0];
  f_a = G[4][0];  

  delta = 1/2*atan(b_a/(1-c_a));  
  C_x = (2*c_a*d_a-b_a*e_a)/(pow(b_a,2)-4*c_a);
  C_y = (2*1*e_a-b_a*d_a)/(pow(b_a,2)-4*c_a);

  w_Num = pow(C_x,2) + b_a*C_x*C_y + c_a*pow(C_y,2) - f_a;
  w_Den = pow(cos(delta),2)+b_a*cos(delta)*sin(delta)+c_a*pow(sin(delta),2);
  w = sqrt(w_Num/w_Den);

  h_Num = pow(C_x,2) + b_a*C_x*C_y + c_a*pow(C_y,2) - f_a;
  h_Den = pow(sin(delta),2)+b_a*cos(delta)*sin(delta)+c_a*pow(cos(delta),2);
  h = sqrt(h_Num/h_Den);

  for(int i=0;i<N;i++){
    rot_x[i] = cos(delta)*(x[i]-C_x) + sin(delta)*(y[i]-C_y);
    rot_y[i] = -sin(delta)*(x[i]-C_x) +cos(delta)*(y[i]-C_y);
  }  

  sigma = h/w;

  for(int j=0;j<N;j++){
    rot_x[j] = sigma *rot_x[j];
  }

}

void plot(){
  Serial.println();
  Serial.println("-------------------variable A----------------");
  for(int i=0; i<N; i++){
      Serial.print(A_T[0][i]);
      Serial.print('\t');        
      Serial.print(A_T[1][i]);
      Serial.print('\t');
      Serial.print(A_T[2][i]);
      Serial.print('\t');        
      Serial.print(A_T[3][i]);
      Serial.print('\t');
      Serial.print(A_T[4][i]);
      Serial.println('\t'); 
  }

  Serial.println("-------------------variable A-(x, y)-----------");
  for(int i=0; i<N; i++){
      Serial.print(A_T[2][i]);
      Serial.print('\t');        
      Serial.print(A_T[3][i]);
      Serial.print('\t');
      Serial.println('\t'); 

  }  

  Serial.println("-------------------variable B----------------");
  for(int i=0; i<N; i++){
      Serial.println(B[i][0]); 
  }

  Serial.println("-------------------variable (A'*A)----------------");
  for(int i=0;i<5;i++){
    for(int j=0;j<5;j++){
      Serial.printf("%6.2f",C[i][j]);
      Serial.print('\t');
    }
    Serial.println();
  }

  Serial.println("-------------------variable inv(A'*A)----------------");  
  print_mtxf(E.inverse()); 

  Serial.println("-------------------variable inv(A'*A)*A'----------------");
  Serial.println(); 
  for(int i=0;i<5;i++){
    for(int j=0;j<N;j++){
      Serial.print(F[i][j]);
      Serial.print('\t');
      if (j%10==9) Serial.println();
      if (j%100==99) Serial.println();
    }
    Serial.println();
  }

  Serial.println("-----variable inv(A'*A)*A'*B---(ellipse Coefficent)-----");
  Serial.println();
  Serial.print(G[0][0]);
  Serial.print('\t');
  Serial.print(G[1][0]);
  Serial.print('\t');
  Serial.print(G[2][0]);
  Serial.print('\t');
  Serial.print(G[3][0]);
  Serial.print('\t');
  Serial.print(G[4][0]);
  Serial.println('\t');  

  Serial.println("-------------------(circle Coefficent)-------------");
  Serial.print(pow(w,2)*pow(sigma,2));


}

void setup() {
  Serial.begin(9600);
  gpsSerial.begin(9600);
  
  int n = WiFi.scanNetworks();
  Serial.println("Scan Done");
  if(n==0){
    Serial.println("no networks found");
  }
  else{
    Serial.print(n);
    Serial.println("  networks found");
  }

  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);

  WiFi.begin(ssid, password);

  while(WiFi.status() != WL_CONNECTED){
    delay(500);
    Serial.print(".");
  }
  Serial.println("");
  Serial.println("WiFi conncected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());
  Serial.println("connection Start");

  if(!client.connect("192.168.0.2", 3500)){ // 서버 ip로 변경
    Serial.println("connection failed");
    delay(500);
  }  

  // start communication with IMU 
  status = IMU.begin();
  if (status < 0) {
    Serial.println("IMU initialization unsuccessful");
    Serial.println("Check IMU wiring or try cycling power");
    Serial.print("Status: ");
    Serial.println(status);
    while(1) {}
  }
  // setting the accelerometer full scale range to +/-8G 
  IMU.setAccelRange(MPU9250::ACCEL_RANGE_8G);
  // setting the gyroscope full scale range to +/-500 deg/s
  IMU.setGyroRange(MPU9250::GYRO_RANGE_500DPS);
  // setting DLPF bandwidth to 20 Hz
  IMU.setDlpfBandwidth(MPU9250::DLPF_BANDWIDTH_20HZ);
  // setting SRD to 19 for a 50 Hz update rate
  IMU.setSrd(19);
  
  //get variable for magnetometer calibration
  getvalue();
  //get Matrix for LSM 
  mag_calibrate_LSM();

  //ellipse to circle
  mag_distortion_calibrate();

  //Serial data for plot
  //plot();
}

void loop() {
  
  static unsigned long previousMillis = 0;
  unsigned long currentMillis = millis();

  //if (currentMillis - previousMillis >= 1000) { // test코드

    if (gpsSerial.available() > 0) {
      char a = gpsSerial.read();
      //char a = gps_raw[0]; // test 코드
      if(a=='$'){
        String gngga = gpsSerial.readStringUntil('\n');
        //String gngga = gps_raw2; // test 코드
        
        if(gngga.substring(0,5).equals(str)){
          //Serial.println(gngga);
          //gngga.toCharArray(gps_string, gngga.length());
          //Serial.println(gps_string);

          gngga.replace(" ", ""); // 빈칸 제거
          //Serial.println(gngga);
  
          gngga.toCharArray(gps_string, gngga.length());
          sscanf(gps_string, "%5c,%f,%lf,%c,%lf,%c,%d,%d,%f,%f,%c,%f,%c",
            &header, &gtime, &lat, &north_south, &lon, &east_west, &fix1, &nsatellite,
            &hdop, &alti, &m1, &wgs84, &m2);

            double lat_min = ((lat/100) - (int)(lat/100))*100/60;
            double lat_angle = (int)(lat/100);
            lat = lat_min + lat_angle;

            double lon_min = ((lon/100) - (int)(lon/100))*100/60;
            double lon_angle = (int)(lon/100);  
            lon = lon_min + lon_angle;  

//            IMU.readSensor();
//            X_h = cos(delta)*(IMU.getMagX_uT()-C_x) + sin(delta)*(IMU.getMagY_uT()-C_y);
//            Y_h = -sin(delta)*(IMU.getMagX_uT()-C_x) + cos(delta)*(IMU.getMagY_uT()-C_y);
//            az = atan(Y_h/X_h)-8.0 * pi / 180;
//          
//            float yaw = az*180/pi;
          
            int yaw = (analogRead(A0)-512) * 180 / 512;
            
                      
            memset(message, 0, sizeof(message));
            sprintf(message, "%lf, %lf, %lf", lat, lon, yaw);
            Serial.println(message);
            client.write(message);
            client.write('\n');
   
          
        }
      }
    //} // test 코드

    //previousMillis = currentMillis; // test 코드
  }

}

void print_mtxf(const Eigen::MatrixXf& X)  
{
   int i, j, nrow, ncol;
   nrow = X.rows();
   ncol = X.cols();
   Serial.print("nrow: "); Serial.println(nrow);
   Serial.print("ncol: "); Serial.println(ncol);       
   Serial.println();
   for (i=0; i<nrow; i++)
   {
       for (j=0; j<ncol; j++)
       {
           Serial.print(X(i,j), 6);   // print 6 decimal places
           Serial.print(", ");
       }
       Serial.println();
   }
   Serial.println();
}
