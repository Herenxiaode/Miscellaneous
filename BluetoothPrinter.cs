using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android;
using Android.App;
using Android.Graphics;
//using Android.Support.V4.Content;
//using Android.Support.V4.App;
namespace Invoice.Droid
{
    using L;
    class BluetoothPrinter:IPrinter
    {
        //width=384
        static string InnerPrinter_Address = "00:11:22:33:44:55";//商米打印机地址
        static Java.Util.UUID UUID = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");//商米打印机UUID
        static Activity Activity;
        Android.Bluetooth.BluetoothDevice BluetoothDevice;
        Android.Bluetooth.BluetoothSocket Socket;
        Stream Stream;

        //void InformUser(string a, string b) { throw new L.Exception($"{b}:{a}"); }
        Stream ReadStream(string n)
        {
            //var ass = AppDomain.CurrentDomain.GetAssemblies();
            //for (int i = 0; i < ass.Length; i++)
            {
                //var p = "Invoice.Droid.Embedded." + n;
                var a = typeof(MainActivity).Assembly;
                //var a = ass[i];
                var os = a.GetManifestResourceNames();
                for (int j = 0; j < os.Length; j++) if (os[j].Contains(n)) return a.GetManifestResourceStream(os[j]);
            }
            return null;
        }
        public BluetoothPrinter(Activity Activity) { BluetoothPrinter.Activity = Activity; GetBluetoothDevice(Activity); }
        public void GetBluetoothDevice(Activity Activity)
        {
            var o = Android.Bluetooth.BluetoothAdapter.DefaultAdapter;
            if (o == null) { Log.Error("No bluetooth adapter found", "Bluetooth"); return; }
            else Log.Success("Bluetooth found", "Success");
            //var PCheck = ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.Bluetooth);
            //if (Android.Support.V4.Content.ContextCompat.CheckSelfPermission(Activity, Manifest.Permission.BluetoothAdmin) == Android.Content.PM.Permission.Denied)
                //    ActivityCompat.ShouldShowRequestPermissionRationale(Activity,Manifest.Permission.BluetoothAdmin);
                //Android.Support.V4.App.ActivityCompat.RequestPermissions(Activity, new string[] { Manifest.Permission.BluetoothAdmin }, 0);
            if (!o.IsEnabled) o.Enable();
            else Log.Success("Bluetooth Enabled", "Success");

            foreach (var b in o.BondedDevices)
                if (b.Name == "InnerPrinter")
                {
                    BluetoothDevice = b;
                    Socket = BluetoothDevice.CreateInsecureRfcommSocketToServiceRecord(UUID);
                    return;
                }
            if(BluetoothDevice==null) Log.Error("Paired Devices not found", "Bluetooth");
        }

        void Write(byte[] OS) => Stream.Write(OS, 0, OS.Length);
        
        public void Begin()
        {
            Socket = BluetoothDevice.CreateInsecureRfcommSocketToServiceRecord(UUID);
            if (!Socket.IsConnected) Socket.Connect();
            Stream = Socket.OutputStream;
            //Write(new byte[] { 0x1C, 0x2E });
            //Write(new byte[] { 0x1C, 0x26 });
            Write(new byte[] {
                0x1C, 0x43, 0xFF,   //设置为UTF-8
                0x1B, 0x45, 0x00,   //默认不粗体     //DoAction(ActionType.TurnEmphasizedMode, 0);
                0x1B, 0x61, 0x00,   //默认左对齐     //DoAction(ActionType.SelectJustification, 0);
                0x1D, 0x21, 0x00    //默认最小字体   //DoAction(ActionType.SelectJustification, 0);
            });
            //DoAction(ActionType.TurnEmphasizedMode, 0);
            //DoAction(ActionType.SelectJustification, 0);
            //SetFontSize(0);
        }
        public void DoAction(ActionType Action, int Value)
        {
            var OS = PrintAction(Action, Value);
            Stream.Write(OS, 0, OS.Length);
        }
        public void SetFontSize(int Size)
        {
            if (1 > Size || Size > 8) Size = 0;
            Size *= 16;
            DoAction(ActionType.SelectCharacterSize, (Size << 4) | Size);
        }
        
        public void Write(string String)
        {
            var OS = Encoding.UTF8.GetBytes(String);
            Stream.Write(OS, 0, OS.Length);
        }
        public void WriteImage(Stream Image)
        {
            // GS v 0 m xL xH yL yH d1...dk
            var bitmap = BitmapFactory.DecodeStream(Image);
            if (bitmap == null) throw new Exception("bitmap is null");
            byte GetPointValue(int S)
            {
                var s = S;
                byte a = (byte)(s >> 24);
                byte r = (byte)(s >> 16);
                byte b = (byte)(s >> 8);
                byte g = (byte)s;
                return (byte)((a > 0x7F && r < 0x7F && g < 0x7F && b < 0x7F) ? 1 : 0);
            }
            Bitmap resizeImage(Bitmap O, int W)
            {
                //将位图宽度规范化为8的整数倍
                W = (W + 7) / 8 * 8;
                var H = (int)(W * bitmap.Height / (float)bitmap.Width);
                return Bitmap.CreateScaledBitmap(bitmap, W, H, true);
            }

            bitmap = resizeImage(bitmap, 390);//规范化位图宽高

            int width = bitmap.Width / 8;
            int height = bitmap.Height;
            var cmd = new byte[width * height + 8];
            cmd[0] = 0x1D;  //cmd[0] = 29;
            cmd[1] = 0x76;  //cmd[1] = 118;
            cmd[2] = 0x30;  //cmd[2] = 48;
            cmd[3] = 0;
            cmd[4] = (byte)(width % 256);//计算xL
            cmd[5] = (byte)(width / 256);//计算xH
            cmd[6] = (byte)(height % 256);//计算yL
            cmd[7] = (byte)(height / 256);//计算yH 

            int index = 8;
            for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x += 8, index++)//横向每8个像素点组成一个字节.
                    for (var a = 0; a < 8; a++) cmd[index] |= (byte)(GetPointValue(bitmap.GetPixel(x + a, y)) << (7 - a));

            Write(cmd);
        }
        public void End()
        {
            Socket.Dispose();
        }


        byte[] PrintAction(ActionType Action, int Value)
        {
            switch (Action)
            {
                //byte[] start = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1B, 0x40, 0x1B, 0x33, 0x00 };
                //                                                                 0x1B, 0x40初始化
                //                                                                             0x1B, 0x33, 0x00设置行高为0
                case ActionType.ImageStart:         return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1B, 0x40, 0x1B, 0x33, 0x00 };
                case ActionType.TurnEmphasizedMode: return new byte[] { 0x1B, 0x45, (byte)Value };
                case ActionType.FeedPaper:          return new byte[] { 0x1B, 0x4A, (byte)Value };
                case ActionType.SelectJustification:return new byte[] { 0x1B, 0x61, (byte)Value };
                case ActionType.SelectCharacterSize:return new byte[] { 0x1D, 0x21, (byte)Value };
                case ActionType.SetLeftMargin: return new byte[] { 0x1D, 0x4C, (byte)Value, (byte)(Value >> 8) };
                default: return new byte[0]; 
            }
        }

    }
}
