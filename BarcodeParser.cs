using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using ZXing.Datamatrix;
using ZXing.Multi;
using ZXing.Multi.QrCode;

namespace BarCode
{
    public class BarcodeParser
    {
        int _sampleNumber = 0;
        int _maxDepth = 0;
        object locker = new object();

        /// <summary>
        /// Асинхронный запуск обработки картинки со штрихкодом
        /// </summary>
        /// <param name="bmp">Битмапа для обработки на которой содержится штрихкод</param>
        /// <param name="recursDepth">Глубина рекурсии. Иными словами сколько мелко будет делиться картинка. Делится на 4^recursDepth частей</param>
        /// <returns></returns>
        public Task<List<string>> StartParseBarCode(Bitmap bmp, int recursDepth)
        {
            _maxDepth = recursDepth;
            return Task<List<string>>.Run(()=> {
                return ParseBarCode(bmp, recursDepth);
            });
        }

        /// <summary>
        /// Запуск обработки битмапы
        /// </summary>
        /// <param name="bmp">Битмапа для обработки на которой содержится штрихкод</param>
        /// <param name="recursDepth">Глубина рекурсии. Иными словами сколько мелко будет делиться картинка. Делится на 4^recursDepth частей</param>
        /// <returns></returns>
        public List<string> ParseBarCode(Bitmap bmp, int recursDepth)
        {
            _maxDepth = recursDepth;
            if (recursDepth >= 0)
            {
                List<Task> tasks = new List<Task>();
                List<string> listOfResultString = new List<string>();
                IBarcodeReader reader = new BarcodeReader();
                reader.Options.TryHarder = false;
                //List<BarcodeFormat> formatList = new List<BarcodeFormat>();
                //formatList.Add(BarcodeFormat.QR_CODE);
                //reader.Options.PossibleFormats = formatList;
                Result result = reader.Decode(bmp);
                if (result != null)
                {
                    listOfResultString.Add(result.ToString());
                    bmp.Save($"testbmp\\{_maxDepth - recursDepth}_{_sampleNumber}_{result.ToString()}.bmp", ImageFormat.Bmp);
                }
                else
                {
                   // bmp.Save($"D:\\testbmp\\{_depth - maxDepth}_{sampleNumber}__NULL.bmp", ImageFormat.Bmp);
                }
                result = null;
                reader = null;
                _sampleNumber++;
                Bitmap[] partsOfBitmap = SplitBitmap(bmp);

                for (int bitmapSegmentNumber = 0; bitmapSegmentNumber < 8; bitmapSegmentNumber++)
                {
                    StartProcessBitmapSegment(partsOfBitmap[bitmapSegmentNumber], recursDepth, bitmapSegmentNumber, tasks, listOfResultString);
                }
                //Ожидаем окончания выполнения всех параллельных потоков поиска штрихкода. Иначе listOfResultString будет возвращаться до
                //того, как будет заполнен значениями.
                Task.WaitAll(tasks.ToArray());

                return listOfResultString;
            }
            return null;
        }

        /// <summary>
        /// Обработка части битмапы. Метод добавлен т.к. полученные от разбиения битмапы части 
        /// обрабатываются по-разному. (Из-за того, что первые четыре части являются четвертями 
        /// битмапы, а ещё четыре - её половинами.) Четверти обрабатываются рекурсивно, а половины нет.
        /// </summary>
        /// <param name="bitmapSegment"></param>
        /// <param name="recursDepth"></param>
        /// <param name="bitmapSegmentNumber"></param>
        /// <returns></returns>
        private Task<List<string>> ProcessBitmapSegment(Bitmap bitmapSegment, int recursDepth, int bitmapSegmentNumber)
        {
            return Task.Run(()=> {
                try
                {
                    List<string> list = new List<string>();
                    //relativeDepth - это относительная глубина рекрсии. По мере углубления её значение уменьшается.
                    int relativeDepth = recursDepth;
                    /*Рекурсивно обрабатываются только четырех первые части разбитой битмапы (они являются четвертями).
                      Оставшиеся четыре части являются половинами разделенной бимапы. Их рекурсивная обработка 
                      смещает центр парсера только по одной оси. И вообще является избыточной. Но нерекурсивная обработка 
                      их всё же требуется для исключения ситуации, когда штрихкод может находится одновременно в двух четвертях битмапы.
                    */
                    if (bitmapSegmentNumber < 4)
                    {
                        list = ParseBarCode(bitmapSegment, --relativeDepth);
                    }
                    else
                    {
                        IBarcodeReader reader = new BarcodeReader();
                        reader.Options.TryHarder = false;
                        //List<BarcodeFormat> formatList = new List<BarcodeFormat>();
                        //formatList.Add(BarcodeFormat.QR_CODE);
                        //reader.Options.PossibleFormats = formatList;
                        Result r = reader.Decode(bitmapSegment);
                        if (r != null)
                            list.Add(r.ToString());
                        r = null;
                        reader = null;
                    }
                    return list;
                }
                catch
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// Асинхронный запуск обработки части изображения.
        /// </summary>
        /// <param name="bitmapSegment"></param>
        /// <param name="recursDepth"></param>
        /// <param name="bitmapSegmentNumber"></param>
        /// <param name="tasks"></param>
        /// <param name="listOfResultString"></param>
        private async void StartProcessBitmapSegment(Bitmap bitmapSegment,  int recursDepth, int bitmapSegmentNumber, List<Task> tasks, List<string> listOfResultString)
        {
            try
            {
                Task<List<string>> task = ProcessBitmapSegment(bitmapSegment, recursDepth, bitmapSegmentNumber);
                tasks.Add(task);
                List<string> listOfBarString = await task;
                if (listOfBarString != null)
                {
                    foreach (var resultString in listOfBarString)
                    {
                        AddResultString(resultString, listOfResultString);
                    }
                }
            }
            catch(Exception ex)
            {
                
            }
        }

        /// <summary>
        /// Проверяем не был ли уже данный штрихкод найден и добавлен в коллекцию штрихкодов.
        /// </summary>
        /// <param name="resultString"></param>
        /// <param name="listOfResultString"></param>
        /// <returns></returns>
        private List<string> AddResultString(string resultString, List<string> listOfResultString)
        {
            if (!listOfResultString.Contains(resultString))
                lock (locker)
                {
                    listOfResultString.Add(resultString);
                }
            return listOfResultString;
        }


        /// <summary>
        /// ZXing распознает штрихкод лишь в том случае если часть штрихкода находится в центре изображения.
        /// Т.о. для поиска штрихкодов не удовлетворящих это условие мы делим изображения на части.
        /// Сначала на четыре четверти. А затем чтобы избежать потреи штрихкодов, которые находятся на пересечении четвертей
        /// на четыре взаимопересекающихся половины нчального изборажения.
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        private Bitmap[] SplitBitmap(Bitmap bmp)
        {
            float halfHeight = (float)bmp.Height / 2;
            float halfWidth = (float)bmp.Width / 2;
            float lCorrection = 50;
            float sCorrection = lCorrection - 0.01f;

            Bitmap[] result = new Bitmap[8];
            PointF location = new PointF(0, 0);
            SizeF size = new SizeF(halfWidth+ sCorrection, halfHeight+ sCorrection);
            RectangleF rec = new RectangleF(location, size);
            result[0] = bmp.Clone(rec, bmp.PixelFormat);

            rec = new RectangleF(new PointF(halfWidth - lCorrection, 0), size);
            result[1] = bmp.Clone(rec, bmp.PixelFormat);

            rec = new RectangleF(new PointF(0, halfHeight - lCorrection), size);
            result[2] = bmp.Clone(rec, bmp.PixelFormat);

            rec = new RectangleF(new PointF(halfWidth - lCorrection, halfHeight - lCorrection), size);
            result[3] = bmp.Clone(rec, bmp.PixelFormat);

            rec = new RectangleF(new PointF(0, 0), size);
            rec.Size = new SizeF(bmp.Width, halfHeight + sCorrection);
            result[4] = bmp.Clone(rec, bmp.PixelFormat);

            rec = new RectangleF(new PointF(0, halfHeight - lCorrection), size);
            rec.Size = new SizeF(bmp.Width, halfHeight + sCorrection);
            result[5] = bmp.Clone(rec, bmp.PixelFormat);

            size = new SizeF(halfWidth + sCorrection, bmp.Height);
            rec = new RectangleF(new PointF(0, 0), size);
            result[6] = bmp.Clone(rec, bmp.PixelFormat);

            rec = new RectangleF(new PointF(halfWidth - lCorrection, 0), size);
            result[7] = bmp.Clone(rec, bmp.PixelFormat);
            return result;
        }


    }
}
