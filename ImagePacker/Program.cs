using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace ImagePacker
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3){
                Console.WriteLine(@"Usage: action bmp payload
                    -action:    'demo', 'pack' or 'unpack' without quotes
                    -bmp:       path for bmp file
                    -payload:   path for input or output payload
                ");
                return;
            }
            string action = args[0],
                   path1 = args[1],
                   path2 = args[2];
            if (action == "demo")
                Demo();
            else if (action == "pack")
                Pack(path1, path2);
            else if (action == "unpack")
                UnPack(path1, path2);
            else
                throw new Exception("Unsupported action");
        }

        /// <summary>
        /// For guys who don't love CLI
        /// </summary>
        private static void Demo()
        {
            var action = "pack";
            var bmpFilePath = "1.bmp";
            var payloadPath = "1.txt";
            Pack(bmpFilePath, payloadPath);
            bmpFilePath = "1.bmp.changed";
            payloadPath = "2.txt";
            UnPack(bmpFilePath, payloadPath);
        }

        /// <summary>
        /// Extract file from another file
        /// </summary>
        /// <param name="path1">Bmp image path</param>
        /// <param name="path2">Extracted (output) file path</param>
        private static void UnPack(string path1, string path2)
        {
            using (var bm = Bitmap.FromFile(path1) as Bitmap)
            {
                var length = GetLengthFromImage(bm);
                //Console.WriteLine("Extracted length " + length);
                var bytes = new byte[length];
                int index = 0;
                for (int i = 0; i < bm.Width; ++i)
                {
                    for (int j = 0; j < bm.Height; ++j){
                        //TODO: variable header length
                        if (i == 0 && (j == 0 || j == 1))   // length bytes
                            continue;
                        var pixel = bm.GetPixel(i, j);
                        //if (index == 0)
                        //    Console.WriteLine("First read pixel is " + pixel.ToString());
                        bytes[index++] = GetDataFromPixel(pixel);
                        if (index == length)
                            goto finish;
                    }
                }
                finish:
                File.WriteAllBytes(path2, bytes);
                Console.WriteLine("File '{0}' Unpacked From\n'{1}'", path2, path1);
            }
        }

        /// <summary>
        /// Pack one file insdile another bmp image
        /// </summary>
        /// <param name="path1">Image path</param>
        /// <param name="path2">Payload path</param>
        private static void Pack(string path1, string path2)
        {
            var data = File.ReadAllBytes(path2);
            int len = data.Length;
            using (var bm = Bitmap.FromFile(path1) as Bitmap)
            {                
                AddLengthToImage(bm, len);
                //Console.WriteLine("Writed length " + len);
                var extractedLength = GetLengthFromImage(bm);
                Debug.Assert(extractedLength == len);
                int index = 0;
                for (int i = 0; i < bm.Width; ++i)
                {
                    for (int j = 0; j < bm.Height; ++j)
                    {
                        //TODO: variable header length
                        if (i == 0 && (j == 0 || j == 1))   // length bytes
                            continue;
                        var pixel = bm.GetPixel(i, j);
                        var newPixel = SetDataToPixel(pixel, data[index]);
                        bm.SetPixel(i, j, newPixel);
                        var extractedData = GetDataFromPixel(newPixel);
                        //if (index == 0)
                        //    Console.WriteLine("First writed pixel is " + newPixel.ToString());
                        Debug.Assert(extractedData == data[index]);
                        ++index;
                        if (index == len)
                            goto finish;
                    }
                }
            finish:
                var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);
                var codec = GetEncoder(bm.RawFormat);
                var outPath = path1 + ".changed";
                bm.Save(outPath, codec, parameters);
                Console.WriteLine("File '{0}' \nPacked Into '{1}'", path2, outPath);
            }
        }

        /// <summary>
        /// Some function from msdn
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        #region Length operation
        private static void AddLengthToImage(Bitmap bm, int len)
        {
            if (len < 0 || len > 65535)
                throw new Exception("Bad file size " + len);
            bm.SetPixel(0, 0, SetDataToPixel(bm.GetPixel(0, 0), (byte)(len % 256)));
            bm.SetPixel(0, 1, SetDataToPixel(bm.GetPixel(0, 1), (byte)((len >> 8)% 256)));            
        }

        private static int GetLengthFromImage(Bitmap bm)
        {
            return GetDataFromPixel(bm.GetPixel(0, 0)) + GetDataFromPixel(bm.GetPixel(0, 1)) * 256;
        }
        #endregion

        #region Most juicy part
        public static Color SetDataToPixel(Color color, byte data)
        {
            return Color.FromArgb(255,
                SetDataToByte(color.R, (byte)((data & 0x07) >> 0)),
                SetDataToByte(color.G, (byte)((data & 0x38) >> 3)),
                SetDataToByte(color.B, (byte)((data & 0xC0) >> 6))
           );
        }

        /// <summary>
        /// Handle 256, 257 issue, direct order
        /// </summary>
        /// <param name="color"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte SetDataToByte(byte color, byte data)
        {
            int res = color - color % 10 + data;
            if (res == 256)
                res = 248;
            if (res == 257)
                res = 249;

            return (byte)res;
        }

        /// <summary>
        /// Handle 256, 257 issue, reverse order
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static byte GetDataFromByte(byte color)
        {
            if (color == 248)
                return 6;
            if (color == 249)
                return 7;
            return (byte)(color % 10);
        }

        public static byte GetDataFromPixel(Color color)
        {
            return (byte)(GetDataFromByte(color.R) +
                         (GetDataFromByte(color.G) << 3) +
                         (GetDataFromByte(color.B) << 6));
        }
        #endregion
    }
}
