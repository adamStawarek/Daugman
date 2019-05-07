﻿using ImageEditor.Filters.Interfaces;
using ImageEditor.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ImageEditor.Filters
{
    public class Daugman : IFilter
    {
        public string Name => "daugman";
        private readonly int _maxRadius = 80;
        private readonly int _minRadius = 30;
        private Bitmap _processedBitmap;

        public Bitmap Filter(Bitmap bitmap)
        {

            _processedBitmap = bitmap;
            var pn = GetProbabilities();
            ApplyHistogramEqualization(pn);
            Point startPoint = FindFloodFillStaringPoint();
            ApplyFloodFill(startPoint);
            var thresholdPixels = ApplyThersholding();
            var localMinPixels = FindLocalMinimums(ref thresholdPixels);
            var pointMaxIntensityDifferences = CalculateCircularPixelsIntensities(localMinPixels);
            MarkObtainedPoints(pointMaxIntensityDifferences);
            _processedBitmap.SetPixel(startPoint.X, startPoint.Y, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X - 1, startPoint.Y, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X + 1, startPoint.Y, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X - 1, startPoint.Y + 1, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X, startPoint.Y + 1, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X + 1, startPoint.Y + 1, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X - 1, startPoint.Y - 1, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X, startPoint.Y - 1, Color.Pink);
            _processedBitmap.SetPixel(startPoint.X + 1, startPoint.Y - 1, Color.Pink);
            return _processedBitmap;
        }
      
        private double[] GetProbabilities()
        {
            var intensityCount = new Dictionary<byte,int>();
            for (int i = 0; i <= 255; i++)
            {
                intensityCount.Add((byte)i,0);
            }
            for (int y = 0; y < _processedBitmap.Height; y++)
            {
                for (int x = 0; x < _processedBitmap.Width; x++)
                {
                    var pixel = _processedBitmap.GetPixel(x, y);
                    byte blue = pixel.R;
                    byte green = pixel.G;
                    byte red = pixel.B;
                    var grayScale = GetGreyscale(red, green, blue);
                    intensityCount[grayScale]++;
                }
            }

            var totalPixelCount = _processedBitmap.Width * _processedBitmap.Height;
            var pn = new Double[256];
            foreach (var i in intensityCount)
            {
                pn[i.Key] = i.Value / (double)totalPixelCount;
            }

            return pn;
        }

        private void ApplyHistogramEqualization(double[] pn)
        {
            for (int y = 0; y < _processedBitmap.Height; y++)
            {
                for (int x = 0; x < _processedBitmap.Width; x++)
                {
                    var pixel = _processedBitmap.GetPixel(x, y);
                    byte blue = pixel.R;
                    byte green = pixel.G;
                    byte red = pixel.B;
                    var grayScale = GetGreyscale(red, green, blue);
                    var newIntensity = (byte)Math.Floor(255 * pn.Take(grayScale).Sum());
                    _processedBitmap.SetPixel(x,y,Color.FromArgb(newIntensity,newIntensity,newIntensity));
                }
            }
        }

        private Point FindFloodFillStaringPoint()
        {
            var offsetWidth = _processedBitmap.Width / 3;
            var offsetHeight = _processedBitmap.Height / 3;
            (Point? point, byte intensity) point= (null, 0);
            for (int y = offsetHeight; y < _processedBitmap.Height-offsetHeight; y++)
            {
                for (int x = offsetWidth; x < _processedBitmap.Width-offsetWidth; x++)
                {
                    var pixel = _processedBitmap.GetPixel(x, y);
                    byte blue = pixel.R;
                    byte green = pixel.G;
                    byte red = pixel.B;
                    var grayScale = GetGreyscale(red, green, blue);
                    if (point.point == null || point.intensity < grayScale)
                        point = (new Point(x, y), grayScale);
                }
            }

            return (Point)point.point;
        }

        private void ApplyFloodFill(Point startPoint)
        {
            Stack<Point> pixels = new Stack<Point>();
            var targetColor = _processedBitmap.GetPixel(startPoint.X, startPoint.Y);
            var targetIntensity = GetGreyscale(targetColor.R, targetColor.G, targetColor.B);
            pixels.Push(startPoint);
            var offset = 40;
            while (pixels.Count > 0)
            {
                Point a = pixels.Pop();
                if (a.X < _processedBitmap.Width && a.X > -1 && a.Y < _processedBitmap.Height && a.Y > -1)
                {
                    var pixelColor = _processedBitmap.GetPixel(a.X, a.Y);
                    var pixelIntensity = GetGreyscale(pixelColor.R, pixelColor.G, pixelColor.B);
                    if (pixelIntensity >= targetIntensity-offset&& pixelIntensity <= targetIntensity + offset)
                    {
                        _processedBitmap.SetPixel(a.X, a.Y, Color.Black);
                        pixels.Push(new Point(a.X - 1, a.Y));
                        pixels.Push(new Point(a.X + 1, a.Y));
                        pixels.Push(new Point(a.X, a.Y - 1));
                        pixels.Push(new Point(a.X, a.Y + 1));
                    }
                }
            }
        }

        private List<Point> ApplyThersholding()
        {
            LogService.Write("Thersholding started...");

            var thresholdPixels = new List<Point>();
            var thresholdValue = byte.MaxValue / 2;
            for (int y = 0; y < _processedBitmap.Height; y++)
            {

                for (int x = 0; x < _processedBitmap.Width; x++)
                {
                    var pixel = _processedBitmap.GetPixel(x, y);
                    byte blue = pixel.R;
                    byte green = pixel.G;
                    byte red = pixel.B;
                    var grayScale = GetGreyscale(red, green, blue);
                    if (grayScale < thresholdValue)
                        thresholdPixels.Add(new Point(x, y));
                }
            }

            return thresholdPixels;
        }

        private List<Point> FindLocalMinimums(ref List<Point> thresholdPixels)
        {
            LogService.Write("Identifying local minimum point in 3x3 neighborhood");
          
            var localMinPixels = new List<Point>();
            //remove edge points and point out of the image
            thresholdPixels = thresholdPixels.Where(p =>
                p.X > 1 && p.Y > 1 && p.X < _processedBitmap.Width - 2 && p.Y < _processedBitmap.Height - 2).ToList();

            foreach (var p1 in thresholdPixels)
            {
                var pointsInNeighborhood = p1.GetPointListAroundTheCenter(1);

                var isLocalMin = true;
                var pixel1 = _processedBitmap.GetPixel(p1.X, p1.Y);
                var val1 = GetGreyscale(pixel1.R, pixel1.G, pixel1.B);
                foreach (var p2 in pointsInNeighborhood)
                {
                    var pixel2 = _processedBitmap.GetPixel(p2.X, p2.Y);
                    var val2 = GetGreyscale(pixel2.R, pixel2.G, pixel2.B);
                    if (val1 > val2) isLocalMin = false;
                }

                if (isLocalMin)
                    localMinPixels.Add(p1);
            }
            return localMinPixels;
        }       

        private Dictionary<Point, CircleIntensity> CalculateCircularPixelsIntensities(List<Point> localMinPixels)
        {
            LogService.Write("calculating circular pixels intensities");
            var pointMaxIntensityDifferences = new Dictionary<Point, CircleIntensity>();

            foreach (var point in localMinPixels)
            {
                var previousIntensities = 0;
                pointMaxIntensityDifferences.Add(point, new CircleIntensity());

                var distancesToBitmapEdges=new List<int>
                {
                    _processedBitmap.Height - point.Y,
                    point.Y,
                    point.X,
                    _processedBitmap.Width-point.X
                };
                var maxPointRadius = distancesToBitmapEdges.Min();
                if(maxPointRadius<_minRadius) continue;


                for (int i = _minRadius; i <  maxPointRadius; i+=3)
                {
                    var circularPoints = point.GetCircularPoints(i, Math.PI / 6.0f);

                    var intensitiesSum = (int)circularPoints.Sum(p =>
                    {
                        var pixel = _processedBitmap.GetPixel(p.X, p.Y);
                        return GetGreyscale(pixel.R, pixel.G, pixel.B);
                    });
                    LogService.Write($"Intensity at radius {i} at point {point.X} , {point.Y} : {intensitiesSum}");
                    if (previousIntensities != 0)
                    {
                        var difference = Math.Abs(intensitiesSum - previousIntensities);
                        if (difference > pointMaxIntensityDifferences[point].DiffIntensity)
                        {
                            pointMaxIntensityDifferences[point] = new CircleIntensity() { DiffIntensity = difference, Radius = i };
                        }

                    }

                    previousIntensities = intensitiesSum;
                }
            }

            return pointMaxIntensityDifferences;
        }

        private void MarkObtainedPoints(Dictionary<Point, CircleIntensity> pointMaxIntensityDifferences)
        {

            var point2 = pointMaxIntensityDifferences.OrderByDescending(p => p.Value.DiffIntensity).First();
            LogService.Write($"Selected center: {point2.Key.X} , {point2.Key.Y} ,radius: {point2.Value.Radius} , " +
                             $"DiffIntensity: {point2.Value.DiffIntensity}");


            _processedBitmap.SetPixel(point2.Key.X, point2.Key.Y, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X - 1, point2.Key.Y, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X + 1, point2.Key.Y, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X - 1, point2.Key.Y + 1, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X, point2.Key.Y + 1, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X + 1, point2.Key.Y + 1, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X - 1, point2.Key.Y - 1, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X, point2.Key.Y - 1, Color.Yellow);
            _processedBitmap.SetPixel(point2.Key.X + 1, point2.Key.Y - 1, Color.Yellow);
            foreach (var p in point2.Key.GetCircularPoints(point2.Value.Radius, Math.PI / 6.0f))
            {
                if(p.Y+1>=_processedBitmap.Height||p.Y-1<0||p.X-1<0||p.X+1>=_processedBitmap.Width)
                    continue;
                _processedBitmap.SetPixel(p.X, p.Y, Color.Red);
                _processedBitmap.SetPixel(p.X - 1, p.Y, Color.Red);
                _processedBitmap.SetPixel(p.X + 1, p.Y, Color.Red);
                _processedBitmap.SetPixel(p.X - 1, p.Y + 1, Color.Red);
                _processedBitmap.SetPixel(p.X, p.Y + 1, Color.Red);
                _processedBitmap.SetPixel(p.X + 1, p.Y + 1, Color.Red);
                _processedBitmap.SetPixel(p.X - 1, p.Y - 1, Color.Red);
                _processedBitmap.SetPixel(p.X, p.Y - 1, Color.Red);
                _processedBitmap.SetPixel(p.X + 1, p.Y - 1, Color.Red);
            }

            //var points = pointMaxIntensityDifferences.OrderByDescending(p => p.Value.DiffIntensity).Take(5).ToList();
            ////LogService.Write($"Selected center: {point2.Key.X} , {point2.Key.Y} ,radius: {point2.Value.Radius} , " +
            ////                 $"diffIntensity: {point2.Value.DiffIntensity}");


            //foreach (var point2 in points)
            //{
            //    _processedBitmap.SetPixel(point2.Key.X, point2.Key.Y, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X - 1, point2.Key.Y, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X + 1, point2.Key.Y, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X - 1, point2.Key.Y + 1, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X, point2.Key.Y + 1, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X + 1, point2.Key.Y + 1, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X - 1, point2.Key.Y - 1, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X, point2.Key.Y - 1, Color.Yellow);
            //    _processedBitmap.SetPixel(point2.Key.X + 1, point2.Key.Y - 1, Color.Yellow);
            //}
            //foreach (var p in points[0].Key.GetCircularPoints(points[0].Value.Radius, Math.PI / 6.0f))
            //{
            //    _processedBitmap.SetPixel(p.X, p.Y, Color.Red);
            //    _processedBitmap.SetPixel(p.X - 1, p.Y, Color.Red);
            //    _processedBitmap.SetPixel(p.X + 1, p.Y, Color.Red);
            //    _processedBitmap.SetPixel(p.X - 1, p.Y + 1, Color.Red);
            //    _processedBitmap.SetPixel(p.X, p.Y + 1, Color.Red);
            //    _processedBitmap.SetPixel(p.X + 1, p.Y + 1, Color.Red);
            //    _processedBitmap.SetPixel(p.X - 1, p.Y - 1, Color.Red);
            //    _processedBitmap.SetPixel(p.X, p.Y - 1, Color.Red);
            //    _processedBitmap.SetPixel(p.X + 1, p.Y - 1, Color.Red);
            //}
        }

        private byte GetGreyscale(byte r, byte g, byte b)
        {
            return (byte)((r + b + g) / 3);
        }
    }
}
