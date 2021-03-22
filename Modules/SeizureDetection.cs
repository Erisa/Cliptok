using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;

namespace Cliptok.Modules
{


    public class SeizureDetection
    {

        static int comparisonAccuracy = Program.cfgjson.SeizureDetection.ComparisonAccuracy; // Only compares every [value] pixels
        static int maxDifferingPixels = Program.cfgjson.SeizureDetection.MaxDifferingPixels; // Ignore [value] differing pixels per cycle
        static int maxComparableFrames = Program.cfgjson.SeizureDetection.MaxComparableFrames; // If GIF has more than [value] frames, don't compare it frame-by-frame
        static int harmlessAverageFrameDifference = Program.cfgjson.SeizureDetection.HarmlessAverageFrameDifference; // Ignore GIFs where the average difference between all their frames is less than [value]%
        static int safeAverageFrameDifference = Program.cfgjson.SeizureDetection.SafeAverageFrameDifference; // Treat GIFs less harshly if the average difference between all their frames is less than [value]%
        static int penaltyAverageFrameDifference = Program.cfgjson.SeizureDetection.PenaltyAverageFrameDifference; // Apply a penalty for GIFs where the average difference between all their frames is greater than [value]%
        static int maxAverageFrameDifference = Program.cfgjson.SeizureDetection.MaxAverageFrameDifference; // Flag GIFs where the average difference between all their frames is greater than [value]%
        static List<int> unsafeGifValues = Program.cfgjson.SeizureDetection.UnsafeGifValues; // If GIF has only [key] frames, then the average length of each frame has to be at least [value] ms long

        public static ImageInfo GetGifProperties(string input)
        {
            ImageInfo Gif = GetImageInfo(input);

            // The actual check for whether or not the GIF might trigger a seizure
            try
            {
                if (Gif.AverageFrameDifference > maxAverageFrameDifference && Gif.AverageContrast < 2)
                {
                    Gif.IsSeizureInducing = false;
                }
                else
                {
                    if (Gif.AverageFrameDifference < harmlessAverageFrameDifference) // Checks to see if its too low to possibly be harmful
                    {
                        Gif.IsSeizureInducing = false;
                    }
                    else if (Gif.AverageFrameDifference > maxAverageFrameDifference && Gif.FrameCount <= (2 * unsafeGifValues.Count)) // Checks to see if its high enough to be potentially risky
                    {
                        Gif.IsSeizureInducing = true;
                    }
                    else
                    {
                        if (safeAverageFrameDifference > Gif.AverageFrameDifference && unsafeGifValues[Gif.UniqueFrameCount] > (Gif.Duration + (Gif.Duration / 2)))
                        {
                            Gif.IsSeizureInducing = false;
                        }
                        else if (Gif.UniqueFrameCount <= unsafeGifValues.Count)
                        {
                            if (unsafeGifValues[Gif.UniqueFrameCount] > Gif.Duration)
                            {
                                Gif.IsSeizureInducing = true;
                            }
                            else if (penaltyAverageFrameDifference < Gif.AverageFrameDifference && unsafeGifValues[Gif.UniqueFrameCount] > (double)Gif.Duration * 1.5)
                            {
                                Gif.IsSeizureInducing = true;
                            }
                        }
                        else
                        {
                            Gif.IsSeizureInducing = false;
                        }
                    }
                }
                
            }
            catch (ArgumentOutOfRangeException)
            {
                Gif.IsSeizureInducing = false;
            }

            Console.WriteLine($"----------\nGIF: {input}");
            Console.WriteLine($"Frame count: {Gif.FrameCount}");
            Console.WriteLine($"Unique frame count: {Gif.UniqueFrameCount}");
            Console.WriteLine($"Average frame difference: {Math.Round(Gif.AverageFrameDifference, 2)}");
            Console.WriteLine($"Average frame contrast: {Math.Round(Gif.AverageContrast, 2)}");
            Console.WriteLine($"Length: {Gif.Length}ms");
            Console.WriteLine($"Frame duration: {Math.Round(Gif.Duration, 2)}ms");
            Console.WriteLine($"Framerate: {Math.Round(Gif.FrameRate, 2)}fps");
            Console.Write($"Seizure-inducing: ");
            if (Gif.UniqueFrameCount != Gif.FrameCount && Gif.IsSeizureInducing)
                Console.ForegroundColor = ConsoleColor.DarkRed;
            else if (Gif.UniqueFrameCount != Gif.FrameCount)
                Console.ForegroundColor = ConsoleColor.DarkGreen;
            else if (Gif.IsSeizureInducing)
                Console.ForegroundColor = ConsoleColor.Red;
            else
                Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{Gif.IsSeizureInducing}");
            Console.ResetColor();
            Console.Write(".\n----------\n\n");
            return Gif;
        }

        public static ImageInfo GetImageInfo(string url)
        {
            ImageInfo info = new ImageInfo();

            using (Image image = Image.FromStream(GetStreamFromUrl(url)))
            {
                info.Height = image.Height;
                info.Width = image.Width;

                if (image.RawFormat.Equals(ImageFormat.Gif))
                {
                    if (ImageAnimator.CanAnimate(image))
                    {
                        FrameDimension frameDimension = new FrameDimension(image.FrameDimensionsList[0]);

                        int frameCount = image.GetFrameCount(frameDimension);
                        decimal delay = 0;
                        decimal this_delay = 0;
                        int index = 0;
                        decimal averageFrameDifference = 0;
                        decimal averageContrast = 0;
                        List<Bitmap> ExtractedFrames = new List<Bitmap>();
                        List<List<int>> isUniqueList = new List<List<int>>(frameCount);
                        for (int i = 0; i < frameCount; i++)
                        {
                            isUniqueList.Add(new List<int>()); // -1 means the image hasn't yet been checked at all
                        }

                        int uniqueFrameCount = 0;

                        if (frameCount < maxComparableFrames)
                        {
                            for (int e = 0; e < frameCount; e++)
                            {
                                image.SelectActiveFrame(frameDimension, e);
                                ExtractedFrames.Add(new Bitmap(image));
                            }
                        }

                        //\\ ================ Beginning of compare loop ================ //\\
                        for (int f = 0; f < frameCount; f++)
                        {
                            this_delay = BitConverter.ToInt32(image.GetPropertyItem(20736).Value, index) * 10;
                            delay += this_delay;
                            index += 4;

                            if (frameCount < maxComparableFrames)
                            {
                                int p = 0;
                                foreach (Bitmap bmp1 in ExtractedFrames)
                                {
                                    bool compareFullFrame = false;
                                    if (p == f + 1)
                                    {
                                        compareFullFrame = true;
                                    }
                                    if (p != f && isUniqueList[f].Count == 0)
                                    {
                                        var comparsionData = Compare(bmp1, ExtractedFrames[f], compareFullFrame);
                                        if (comparsionData.Item1)
                                        {
                                            isUniqueList[p].Add(f);

                                        }
                                        if (comparsionData.Item2 != -1)
                                        {
                                            averageFrameDifference += comparsionData.Item2;
                                            averageContrast += comparsionData.Item3;
                                        }
                                    }
                                    else if (compareFullFrame == true && p != f)
                                    {
                                        var comparsionData = Compare(bmp1, ExtractedFrames[f], compareFullFrame);
                                        if (comparsionData.Item2 != -1)
                                        {
                                            averageFrameDifference += comparsionData.Item2;
                                            averageContrast += comparsionData.Item3;
                                        }
                                    }
                                    p++;
                                }
                            }
                        }
                        //\\ =================== End of compare loop =================== //\\

                        // Get number of unique frames
                        foreach (List<int> b in isUniqueList)
                        {
                            if (b.Count == 0)
                            {
                                uniqueFrameCount++;
                            }
                        }
                        // If it's zero then every frame is unique uwu
                        if (uniqueFrameCount == 0)
                        {
                            uniqueFrameCount = frameCount;
                        }
                        // Dispose of all those nasty bitmaps
                        foreach (Bitmap bmp in ExtractedFrames)
                        {
                            bmp.Dispose();
                        }


                        // Adds info to struct
                        try
                        {
                            info.Duration = delay / frameCount;
                            info.FrameCount = frameCount;
                            info.UniqueFrameCount = uniqueFrameCount;
                            info.AverageFrameDifference = averageFrameDifference / (frameCount - 1);
                            info.AverageContrast = averageContrast / (frameCount - 1);
                            info.IsAnimated = true;
                            info.IsLooped = BitConverter.ToInt16(image.GetPropertyItem(20737).Value, 0) != 1;
                            info.Length = delay;
                            info.FrameRate = frameCount / (info.Length / 1000);
                        }
                        // If anything is accidentally zero because GIFs suck then just assume its safe
                        // Mods can deal with the chaos that ensues
                        catch (DivideByZeroException)
                        {
                            info.Duration = 1000;
                            info.FrameCount = frameCount;
                            info.UniqueFrameCount = uniqueFrameCount;
                            info.AverageFrameDifference = 0;
                            info.AverageContrast = 0;
                            info.IsAnimated = true;
                            info.IsLooped = BitConverter.ToInt16(image.GetPropertyItem(20737).Value, 0) != 1;
                            info.Length = 1000 * frameCount;
                            info.FrameRate = -1;
                        }
                    }
                }
            }
            return info; // Finally! We're free of this mess!
        }

        // The almighty comparison mechanism. Takes in two images and checks to see if they're the same. Optionally it will compare the whole image.
        // Returns a bool stating whether or not the images are identical, and a decimal that contains a percentage of how similar the two images are (if compareFullFrame is true)
        public static (bool, decimal, decimal) Compare(Bitmap bmp1, Bitmap bmp2, bool compareFullFrame)
        {
            int pixelCount = bmp1.Width * bmp1.Height; // Total pixels in the frame
            int differingPixels = 0; // Keeps track of how many pixels differ between the two images
            decimal pixelDifference = 0; // Keeps a percentage of how many differing pixels there are
            decimal pixelContrast = 0;

            for (int x = 0; x < bmp1.Width; x += comparisonAccuracy)
            {
                for (int y = 0; y < bmp1.Height; y += comparisonAccuracy)
                {
                    if (bmp1.GetPixel(x, y) != bmp2.GetPixel(x, y))
                    {
                        differingPixels++;
                        //Console.WriteLine($"[NOTE] Pixel {x},{y} in bitmap 1 did not match pixel {x},{y} in bitmap 2");
                        if (differingPixels > maxDifferingPixels)
                        {
                            if (!compareFullFrame)
                            {
                                return (false, pixelDifference, pixelContrast);
                            }
                        }
                    }
                }
            }
            if (compareFullFrame)
            {
                pixelDifference = (Convert.ToDecimal(differingPixels) / Convert.ToDecimal(pixelCount)) * Convert.ToDecimal(100) * (comparisonAccuracy * comparisonAccuracy);
                pixelContrast = (decimal)GetContrast(GetAverageColour(bmp1), GetAverageColour(bmp2));
            }
            if (differingPixels > maxDifferingPixels && compareFullFrame)
            {
                return (false, pixelDifference, pixelContrast);
            }
            else
            {
                return (true, -1, -1);
            }
        }

        // Gets the average colour of an image
        private static Color GetAverageColour(Bitmap bmp)
        {
            Bitmap bmp1px = new Bitmap(1, 1);
            using (Graphics g = Graphics.FromImage(bmp1px))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(bmp, new Rectangle(0, 0, 1, 1));
            }
            return bmp1px.GetPixel(0, 0);
        }

        private static double GetLuminance(Color c)
        {
            byte[] colourArray = { c.R, c.G, c.B };
            double[] luminanceArray = new double[3];
            for (int i = 0; i < 3; i++)
            {
                luminanceArray[i] = colourArray[i] / 255.0;
                luminanceArray[i] = luminanceArray[i] <= 0.03928
                   ? luminanceArray[i] / 12.92
                   : Math.Pow((luminanceArray[i] + 0.055) / 1.055, 2.4);
            }
            return luminanceArray[0] * 0.2126 + luminanceArray[1] * 0.7152 + luminanceArray[2] * 0.0722;
        }

        private static double GetContrast(Color c1, Color c2)
        {
            var lum1 = GetLuminance(c1);
            var lum2 = GetLuminance(c2);
            var brightest = Math.Max(lum1, lum2);
            var darkest = Math.Min(lum1, lum2);
            return (brightest + 0.05)
                 / (darkest + 0.05);
        }

        // Nothing fancy, just gets the GIF.
        private static Stream GetStreamFromUrl(string url)
        {
            byte[] imageData = null;

            using (var wc = new System.Net.WebClient())
                imageData = wc.DownloadData(url);

            Console.WriteLine("Downloaded GIF");
            return new MemoryStream(imageData);
        }

        // Definition for a loaded GIF
        public struct ImageInfo
        {
            public bool IsSeizureInducing; // Whether or not the GIF likely to be painful
            public int Width; // Width of GIF
            public int Height; // Height of GIF
            public int FrameCount; // Number of frames in the GIF
            public int UniqueFrameCount; // Number of unique frames in the GIF
            public decimal AverageFrameDifference; // Average difference between all frames
            public decimal AverageContrast; // Average contrast between all frames
            public bool IsAnimated; // Whether or not the GIF is animated
            public bool IsLooped; // Whether or not the GIF loops
            public decimal Duration; // Average duration of one frame in milliseconds
            public decimal Length; // Length of entire GIF in milliseconds
            public decimal FrameRate; // Average framerate of entire GIF in FPS
        }
    }
}
