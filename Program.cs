using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace BarCode
{
    class Program
    {

        static int Main(string[] args)
        {
            Run(args);
            Console.ReadKey();
            return 0;
        }

        private static async void ProcessingFile(string FilePath, List<Task> tasks)
        {
            int fileNumber = 0;
            BarcodeParser barcodeParser = new BarcodeParser();
            List<string> resultStrings = null;
            Bitmap barcodeBitmap = null;
            barcodeBitmap = Image.FromFile(FilePath) as Bitmap;
            if (barcodeBitmap != null)
            {
                //barcodeBitmap = barcodeBitmap.Contrast(1);
                // barcodeBitmap.Save("D:\\testcontrast.bmp", ImageFormat.Bmp);
                Task<List<string>> task = barcodeParser.StartParseBarCode(barcodeBitmap, 2);
                tasks.Add(task);
                resultStrings = await task;
                fileNumber++;
            }
            //Console.WriteLine($"filepath {FilePath}:");
            if (resultStrings != null)
            {
                foreach (var barString in resultStrings)
                {
                    Console.WriteLine($"bar {barString}");
                }
            }
            Console.WriteLine();
        }

        private static void Run(string[] args)
        {
            List<Task> tasks = new List<Task>();

            BarcodeParser barcodeParser = new BarcodeParser();
            foreach (var item in args)
            {
                ProcessingFile(item, tasks);
            }
            Task.WaitAll(tasks.ToArray());
        }
    }
}
