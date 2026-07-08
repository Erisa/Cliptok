namespace Cliptok.Helpers
{
    public class SeizureDetectionHelpers
    {
        static int comparisonAccuracy = Program.cfgjson.SeizureDetection.ComparisonAccuracy; // Only compares every [value] pixels
        static int maxDifferingPixels = Program.cfgjson.SeizureDetection.MaxDifferingPixels; // Ignore [value] differing pixels per cycle
        static int maxComparableFrames = Program.cfgjson.SeizureDetection.MaxComparableFrames; // If GIF has more than [value] frames, don't compare it frame-by-frame
        static int harmlessAverageFrameDifference = Program.cfgjson.SeizureDetection.HarmlessAverageFrameDifference; // Ignore GIFs where the average difference between all their frames is less than [value]%
        static int safeAverageFrameDifference = Program.cfgjson.SeizureDetection.SafeAverageFrameDifference; // Treat GIFs less harshly if the average difference between all their frames is less than [value]%
        static int penaltyAverageFrameDifference = Program.cfgjson.SeizureDetection.PenaltyAverageFrameDifference; // Apply a penalty for GIFs where the average difference between all their frames is greater than [value]%
        static int maxAverageFrameDifference = Program.cfgjson.SeizureDetection.MaxAverageFrameDifference; // Flag GIFs where the average difference between all their frames is greater than [value]%
        static List<int> unsafeGifValues = Program.cfgjson.SeizureDetection.UnsafeGifValues; // If GIF has only [key] frames, then the average length of each frame has to be at least [value] ms long

        public static async Task<ImageInfo> GetGifPropertiesAsync(string input)
        {
            ImageInfo Gif = await GetImageInfoAsync(input);

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

        public static async Task<ImageInfo> GetImageInfoAsync(string url)
        {
            ImageInfo info = new ImageInfo();

            using (SKCodec image = SKCodec.Create(await GetStreamFromUrlAsync(url)))
            {
                info.Height = image.Info.Height;
                info.Width = image.Info.Width;

                if (image.EncodedFormat == SKEncodedImageFormat.Gif)
                {
                    if (image.FrameCount > 1)
                    {
                        int frameCount = image.FrameCount;
                        decimal delay = 0;
                        decimal this_delay = 0;
                        decimal averageFrameDifference = 0;
                        decimal averageContrast = 0;
                        List<SKBitmap> ExtractedFrames = new List<SKBitmap>();
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
                                SKBitmap bitmap = new SKBitmap(image.Info);;
                                image.GetPixels(image.Info, bitmap.GetPixels(), new SKCodecOptions(e));
                                ExtractedFrames.Add(bitmap);
                            }
                        }

                        SKCodecFrameInfo[] frameInfo = image.FrameInfo;

                        //\\ ================ Beginning of compare loop ================ //\\
                        for (int f = 0; f < frameCount; f++)
                        {
                            this_delay = frameInfo[f].Duration;
                            delay += this_delay;

                            if (frameCount < maxComparableFrames)
                            {
                                int p = 0;
                                foreach (SKBitmap bmp1 in ExtractedFrames)
                                {
                                    bool compareFullFrame = false;
                                    if (p == f + 1)
                                    {
                                        compareFullFrame = true;
                                    }
                                    if (p != f && isUniqueList[f].Count == 0)
                                    {
                                        var comparisonData = Compare(bmp1, ExtractedFrames[f], compareFullFrame);
                                        if (comparisonData.Item1)
                                        {
                                            isUniqueList[p].Add(f);

                                        }
                                        if (comparisonData.Item2 != -1)
                                        {
                                            averageFrameDifference += comparisonData.Item2;
                                            averageContrast += comparisonData.Item3;
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
                        foreach (SKBitmap bmp in ExtractedFrames)
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
                            info.IsLooped = image.RepetitionCount != 0;
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
                            info.IsLooped = image.RepetitionCount != 0;
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
        public static (bool, decimal, decimal) Compare(SKBitmap bmp1, SKBitmap bmp2, bool compareFullFrame)
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
        private static SKColor GetAverageColour(SKBitmap bmp)
        {
            SKBitmap bmp1px = new SKBitmap(1, 1);
            using (SKCanvas c = new SKCanvas(bmp1px))
            {
                c.DrawBitmap(bmp, new SKRect(0, 0, 1, 1), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }
            return bmp1px.GetPixel(0, 0);
        }

        private static double GetLuminance(SKColor c)
        {
            byte[] colourArray = { c.Red, c.Green, c.Blue };
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

        private static double GetContrast(SKColor c1, SKColor c2)
        {
            var lum1 = GetLuminance(c1);
            var lum2 = GetLuminance(c2);
            var brightest = Math.Max(lum1, lum2);
            var darkest = Math.Min(lum1, lum2);
            return (brightest + 0.05)
                 / (darkest + 0.05);
        }

        // Nothing fancy, just gets the GIF.
        private static async Task<Stream> GetStreamFromUrlAsync(string url)
        {
            byte[] imageData = null;

            imageData = await Program.httpClient.GetByteArrayAsync(url);

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
