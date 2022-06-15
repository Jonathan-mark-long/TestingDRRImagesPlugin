using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using Testing_DRR_Images;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using System.IO;

// Allows the script to write. Necessary for creating a course and plans.
[assembly: ESAPIScript(IsWriteable = true)]


namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            Patient p = context.Patient;
            ExternalPlanSetup plan = context.ExternalPlanSetup;

            List<Beam> beams = plan.Beams.Where(b => !b.IsSetupField).OrderBy(b => b.BeamNumber).ToList();
            string Path = @"\\Tevirari002\va_data$\ProgramData\Vision\PublishedScripts\TemporaryImages\";

            p.BeginModifications();

            foreach (Beam beam in beams)
            {
                VMS.TPS.Common.Model.API.Image Drr = beam.ReferenceImage;
                WriteableBitmap drr = BuildDRRImage(beam, Drr);
                SourcetoPng(drr, Path);
                System.Drawing.Image png = System.Drawing.Image.FromFile(Path + "Drr.png");
                Bitmap drr_fieldLines = FieldLines(beam, png, Drr, plan);
                png.Dispose();
                drr_fieldLines.Save(Path + @"\Drr_Field" + beam.Id + ".png", ImageFormat.Png);
            }




        }

        public static void SourcetoPng(BitmapSource bmp, string Path)
        {
            using (var fileStream = new FileStream(Path + @"\Drr.png", FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                encoder.Save(fileStream);
                fileStream.Close();
            }
        }

        private static WriteableBitmap BuildDRRImage(Beam beam, VMS.TPS.Common.Model.API.Image Drr)
        {
            if (beam.ReferenceImage == null) { return null; }


            int[,] pixels = new int[Drr.YSize, Drr.XSize];
            Drr.GetVoxels(0, pixels);
            int[] flat_pixels = new int[Drr.YSize * Drr.XSize];

            for (int i = 0; i < Drr.YSize; i++)
            {
                for (int j = 0; j < Drr.XSize; j++)
                {
                    flat_pixels[i + Drr.XSize * j] = pixels[i, j];
                }
            }

            var Drr_max = flat_pixels.Max();
            var Drr_min = flat_pixels.Min();

            System.Windows.Media.PixelFormat format = PixelFormats.Gray8;
            int stride = (Drr.XSize * format.BitsPerPixel + 7) / 8;
            byte[] image_bytes = new byte[stride * Drr.YSize];

            for (int i = 0; i < flat_pixels.Length; i++)
            {
                double value = flat_pixels[i];
                image_bytes[i] = Convert.ToByte(255 * ((value - Drr_min) / (Drr_max - Drr_min)));
            }

            BitmapSource source = BitmapSource.Create(Drr.XSize, Drr.YSize, 25.4 / Drr.XRes, 25.4 / Drr.YRes, format, null, image_bytes, stride);

            WriteableBitmap writeableBitmap = new WriteableBitmap(source);

            return writeableBitmap;

        }


        private static Bitmap FieldLines(Beam beam, System.Drawing.Image source, VMS.TPS.Common.Model.API.Image Drr, PlanSetup plan)
        {
            Bitmap bmp = new Bitmap(source);

            Bitmap bitmap = new Bitmap(bmp.Width * 4, bmp.Height * 4, bmp.PixelFormat);
            bitmap.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);
            using (var gr = Graphics.FromImage(bitmap))
            {
                gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                gr.DrawImage(bmp, new Rectangle(0, 0, bmp.Width * 4, bmp.Height * 4));
            }

            // Gantry Angle
            double GantryAngle = (beam.ControlPoints.FirstOrDefault().GantryAngle) * (Math.PI / 180.00);

            //Isocentre = center of image.
            double Centre_X = bitmap.Width / 2;
            double Centre_Y = bitmap.Height / 2;

            // Shifts to user orgin.
            double X_offset = (Math.Cos(GantryAngle) * (plan.StructureSet.Image.UserOrigin.x - beam.IsocenterPosition.x) + Math.Sin(GantryAngle) * (plan.StructureSet.Image.UserOrigin.y - beam.IsocenterPosition.y)) * 4;
            double Y_offset = (plan.StructureSet.Image.UserOrigin.z - beam.IsocenterPosition.z) * 4;

            // User origin.
            double user_origin_X = Centre_X + X_offset;
            double user_origin_Y = Centre_Y - Y_offset;

            // Shifts Reference Point
            double ReferencePoint_X_Shift = (Math.Cos(GantryAngle) * (plan.PrimaryReferencePoint.GetReferencePointLocation(plan).x - beam.IsocenterPosition.x) + Math.Sin(GantryAngle) * (plan.PrimaryReferencePoint.GetReferencePointLocation(plan).y - beam.IsocenterPosition.y)) * 4;
            double ReferencePoint_Y_Shift = (plan.PrimaryReferencePoint.GetReferencePointLocation(plan).z - beam.IsocenterPosition.z) * 4;

            double ReferencePoint_X = Centre_X + ReferencePoint_X_Shift;
            double ReferencePoint_Y = Centre_Y - ReferencePoint_Y_Shift;

            // Shifts to 3D dose max 
            double DoseMax_Xoffset = (Math.Cos(GantryAngle) * (plan.Dose.DoseMax3DLocation.x - beam.IsocenterPosition.x) + Math.Sin(GantryAngle) * (plan.Dose.DoseMax3DLocation.y - beam.IsocenterPosition.y)) * 4;
            double DoseMax_Yoffset = (plan.Dose.DoseMax3DLocation.z - beam.IsocenterPosition.z) * 4;

            // Dose Max
            double DoseMax_X = Centre_X + DoseMax_Xoffset;
            double DoseMax_Y = Centre_Y - DoseMax_Yoffset;

            // MLC properties
            // Leaf offset
            List<double> LeafOffset = new List<double> { -195, -185, -175, -165, -155, -145, -135, -125, -115, -105, -97.5, -92.5, -87.5, -82.5, -77.5, -72.5, -67.5, -62.5, -57.5, -52.5, -47.5, -42.5, -37.5, -32.5, -27.5, -22.5, -17.5, -12.5, -7.5, -2.5, 2.5, 7.5, 12.5, 17.5, 22.5, 27.5, 32.5, 37.5, 42.5, 47.5, 52.5, 57.5, 62.5, 67.5, 72.5, 77.5, 82.5, 87.5, 92.5, 97.5, 105, 115, 125, 135, 145, 155, 165, 175, 185, 195 };
            // Leaf Widths
            List<int> LeafWidths = new List<int> { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };
            // Leaf positions
            List<double> Bank0Positions = new List<double>();
            List<double> Bank1Positions = new List<double>();

            if (beam.MLC != null)
            {
                var LeafPostions = beam.ControlPoints.ElementAtOrDefault(0).LeafPositions;

                for (var i = 0; i < 59; i++)
                {
                    Bank0Positions.Add(LeafPostions[0, i]);
                    Bank1Positions.Add(LeafPostions[1, i]);
                }
            }


            double collimatorAngle = (beam.ControlPoints.FirstOrDefault().CollimatorAngle) * (Math.PI / 180.00);

            VRect<double> jawPositions = beam.ControlPoints.FirstOrDefault().JawPositions;

            double Y2_CenterX = Centre_X - (jawPositions.Y2 * Math.Sin(collimatorAngle)) * 4;
            double Y2_CenterY = Centre_Y - (jawPositions.Y2 * Math.Cos(collimatorAngle)) * 4;

            double Y1_CenterX = Centre_X - (jawPositions.Y1 * Math.Sin(collimatorAngle)) * 4;
            double Y1_CenterY = Centre_Y - (jawPositions.Y1 * Math.Cos(collimatorAngle)) * 4;


            double Y2_UpperX = Y2_CenterX + jawPositions.X2 * Math.Cos(collimatorAngle) * 4;
            double Y2_UpperY = Y2_CenterY - jawPositions.X2 * Math.Sin(collimatorAngle) * 4;

            double Y2_LowerX = Y2_CenterX + jawPositions.X1 * Math.Cos(collimatorAngle) * 4;
            double Y2_LowerY = Y2_CenterY - jawPositions.X1 * Math.Sin(collimatorAngle) * 4;

            double Y1_UpperX = Y1_CenterX + jawPositions.X2 * Math.Cos(collimatorAngle) * 4;
            double Y1_UpperY = Y1_CenterY - jawPositions.X2 * Math.Sin(collimatorAngle) * 4;

            double Y1_LowerX = Y1_CenterX + jawPositions.X1 * Math.Cos(collimatorAngle) * 4;
            double Y1_LowerY = Y1_CenterY - jawPositions.X1 * Math.Sin(collimatorAngle) * 4;

            int Graticule_Length = (int)(Math.Round(Drr.XSize * 2 / 100d, 0) * 100) * 4;

            int Graticule_PositiveX = (int)(Graticule_Length * Math.Sin(collimatorAngle)) * 4;
            int Graticule_PositiveY = (int)(Graticule_Length * Math.Cos(collimatorAngle)) * 4;


            System.Drawing.Pen fieldPen = new System.Drawing.Pen(System.Drawing.Color.Yellow, 3);
            System.Drawing.Pen isoPen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);
            System.Drawing.Pen graticulePen = new System.Drawing.Pen(System.Drawing.Color.Yellow, (float)0.5);
            System.Drawing.Pen referncePointPen = new System.Drawing.Pen(System.Drawing.Color.Cyan, 3);


            Font FieldFont = new Font("Arial", 24, System.Drawing.FontStyle.Bold);
            Font TextFont = new Font("Arial", 16, System.Drawing.FontStyle.Bold);
            System.Drawing.SolidBrush FieldText = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
            System.Drawing.SolidBrush ReferencePointText = new System.Drawing.SolidBrush(System.Drawing.Color.Cyan);



            using (var graphics = Graphics.FromImage(bitmap))
            {

                graphics.DrawLine(isoPen, Convert.ToInt32(user_origin_X), Convert.ToInt32(user_origin_Y) - 10, Convert.ToInt32(user_origin_X), Convert.ToInt32(user_origin_Y) + 10);
                graphics.DrawLine(fieldPen, Convert.ToInt32(user_origin_X - 10), Convert.ToInt32(user_origin_Y), Convert.ToInt32(user_origin_X + 10), Convert.ToInt32(user_origin_Y));

                graphics.DrawLine(referncePointPen, Convert.ToInt32(ReferencePoint_X - 5), Convert.ToInt32(ReferencePoint_Y - 5), Convert.ToInt32(ReferencePoint_X + 5), Convert.ToInt32(ReferencePoint_Y + 5));
                graphics.DrawLine(referncePointPen, Convert.ToInt32(ReferencePoint_X - 5), Convert.ToInt32(ReferencePoint_Y + 5), Convert.ToInt32(ReferencePoint_X + 5), Convert.ToInt32(ReferencePoint_Y - 5));
                graphics.DrawString(plan.PrimaryReferencePoint.Id, TextFont, ReferencePointText, Convert.ToInt32(ReferencePoint_X - (plan.PrimaryReferencePoint.Id.Length * 10) / 2), Convert.ToInt32(ReferencePoint_Y - 35));


                graphics.DrawEllipse(isoPen, Convert.ToInt32(Centre_X - 5), Convert.ToInt32(Centre_Y - 5), 10, 10);

                graphics.DrawLine(graticulePen, Convert.ToInt32(Centre_X + Graticule_PositiveX), Convert.ToInt32(Centre_Y + Graticule_PositiveY), Convert.ToInt32(Centre_X - Graticule_PositiveX), Convert.ToInt32(Centre_Y - Graticule_PositiveY));
                graphics.DrawLine(graticulePen, Convert.ToInt32(Centre_X + Graticule_PositiveY), Convert.ToInt32(Centre_Y - Graticule_PositiveX), Convert.ToInt32(Centre_X - Graticule_PositiveY), Convert.ToInt32(Centre_Y + Graticule_PositiveX));

                for (int A = -Graticule_Length; A < Graticule_Length; A += 10)
                {
                    double Marker_CenterX1 = Centre_X + (A * Math.Sin(collimatorAngle)) * 4;
                    double Marker_CenterY1 = Centre_Y + (A * Math.Cos(collimatorAngle)) * 4;
                    double Marker_CenterX2 = Centre_X + (A * Math.Cos(collimatorAngle)) * 4;
                    double Marker_CenterY2 = Centre_Y - (A * Math.Sin(collimatorAngle)) * 4;


                    if (A % 50 == 0)
                    {
                        double Marker_Cosine = (10 * Math.Cos(collimatorAngle)) * 4;
                        double Marker_Sine = (10 * Math.Sin(collimatorAngle)) * 4;
                        graphics.DrawLine(graticulePen, Convert.ToInt32(Marker_CenterX1 + Marker_Cosine), Convert.ToInt32(Marker_CenterY1 - Marker_Sine), Convert.ToInt32(Marker_CenterX1 - Marker_Cosine), Convert.ToInt32(Marker_CenterY1 + Marker_Sine));
                        graphics.DrawLine(graticulePen, Convert.ToInt32(Marker_CenterX2 + Marker_Sine), Convert.ToInt32(Marker_CenterY2 + Marker_Cosine), Convert.ToInt32(Marker_CenterX2 - Marker_Sine), Convert.ToInt32(Marker_CenterY2 - Marker_Cosine));

                    }
                    else
                    {
                        double Marker_Cosine = (5 * Math.Cos(collimatorAngle)) * 4;
                        double Marker_Sine = (5 * Math.Sin(collimatorAngle)) * 4;
                        graphics.DrawLine(graticulePen, Convert.ToInt32(Marker_CenterX1 + Marker_Cosine), Convert.ToInt32(Marker_CenterY1 - Marker_Sine), Convert.ToInt32(Marker_CenterX1 - Marker_Cosine), Convert.ToInt32(Marker_CenterY1 + Marker_Sine));
                        graphics.DrawLine(graticulePen, Convert.ToInt32(Marker_CenterX2 + Marker_Sine), Convert.ToInt32(Marker_CenterY2 + Marker_Cosine), Convert.ToInt32(Marker_CenterX2 - Marker_Sine), Convert.ToInt32(Marker_CenterY2 - Marker_Cosine));

                    }


                }

                // MLCs
                if (beam.MLC != null)
                {
                    for (int l = 0; l < 59; l++)
                    {
                        if (LeafOffset.ElementAt(l) > jawPositions.Y1 && LeafOffset.ElementAt(l) < jawPositions.Y2)
                        {
                            double MLC_Edge1X_atCenter = Centre_X - ((LeafOffset.ElementAt(l) - LeafWidths.ElementAt(l) / 2) * Math.Sin(collimatorAngle)) * 4;
                            double MlC_Edge1Y_atCenter = Centre_Y - ((LeafOffset.ElementAt(l) - LeafWidths.ElementAt(l) / 2) * Math.Cos(collimatorAngle)) * 4;

                            double MLC_Edge2X_atCenter = Centre_X - ((LeafOffset.ElementAt(l) + LeafWidths.ElementAt(l) / 2) * Math.Sin(collimatorAngle)) * 4;
                            double MlC_Edge2Y_atCenter = Centre_Y - ((LeafOffset.ElementAt(l) + LeafWidths.ElementAt(l) / 2) * Math.Cos(collimatorAngle)) * 4;

                            if (Math.Abs(Bank0Positions.ElementAt(l)) < Math.Abs(jawPositions.X1))
                            {
                                double MLC0_Edge1X = MLC_Edge1X_atCenter + (Bank0Positions.ElementAt(l) * Math.Cos(collimatorAngle)) * 4;
                                double MLC0_Edge1Y = MlC_Edge1Y_atCenter - (Bank0Positions.ElementAt(l) * Math.Sin(collimatorAngle)) * 4;

                                if (Bank0Positions.ElementAt(l - 1) != Bank0Positions.ElementAt(l) && LeafOffset.ElementAt(l - 1) > jawPositions.Y1)
                                {
                                    if (Math.Abs(Bank0Positions.ElementAt(l - 1)) < Math.Abs(jawPositions.X1))
                                    {
                                        double MLC0_Side1X = MLC_Edge1X_atCenter + (Bank0Positions.ElementAt(l - 1) * Math.Cos(collimatorAngle)) * 4;
                                        double MLC0_Side1Y = MlC_Edge1Y_atCenter - (Bank0Positions.ElementAt(l - 1) * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC0_Edge1X), Convert.ToInt32(MLC0_Edge1Y), Convert.ToInt32(MLC0_Side1X), Convert.ToInt32(MLC0_Side1Y));
                                    }
                                    if (Math.Abs(Bank0Positions.ElementAt(l - 1)) > Math.Abs(jawPositions.X1))
                                    {
                                        double MLC0_Side1X = MLC_Edge1X_atCenter + (jawPositions.X1 * Math.Cos(collimatorAngle)) * 4;
                                        double MLC0_Side1Y = MlC_Edge1Y_atCenter - (jawPositions.X1 * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC0_Edge1X), Convert.ToInt32(MLC0_Edge1Y), Convert.ToInt32(MLC0_Side1X), Convert.ToInt32(MLC0_Side1Y));
                                    }

                                }

                                double MLC0_Edge2X = MLC_Edge2X_atCenter + (Bank0Positions.ElementAt(l) * Math.Cos(collimatorAngle)) * 4;
                                double MlC0_Edge2Y = MlC_Edge2Y_atCenter - (Bank0Positions.ElementAt(l) * Math.Sin(collimatorAngle)) * 4;

                                if (Bank0Positions.ElementAt(l + 1) != Bank0Positions.ElementAt(l) && LeafOffset.ElementAt(l) < jawPositions.Y2)
                                {
                                    if (Math.Abs(Bank0Positions.ElementAt(l + 1)) < Math.Abs(jawPositions.X1))
                                    {
                                        double MLC0_Side2X = MLC_Edge2X_atCenter + (Bank0Positions.ElementAt(l + 1) * Math.Cos(collimatorAngle)) * 4;
                                        double MLC0_Side2Y = MlC_Edge2Y_atCenter - (Bank0Positions.ElementAt(l + 1) * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC0_Edge2X), Convert.ToInt32(MlC0_Edge2Y), Convert.ToInt32(MLC0_Side2X), Convert.ToInt32(MLC0_Side2Y));
                                    }
                                    if (Math.Abs(Bank0Positions.ElementAt(l + 1)) > Math.Abs(jawPositions.X1))
                                    {
                                        double MLC0_Side2X = MLC_Edge2X_atCenter + (jawPositions.X1 * Math.Cos(collimatorAngle)) * 4;
                                        double MLC0_Side2Y = MlC_Edge2Y_atCenter - (jawPositions.X1 * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC0_Edge2X), Convert.ToInt32(MlC0_Edge2Y), Convert.ToInt32(MLC0_Side2X), Convert.ToInt32(MLC0_Side2Y));
                                    }

                                }

                                graphics.DrawLine(fieldPen, Convert.ToInt32(MLC0_Edge1X), Convert.ToInt32(MLC0_Edge1Y), Convert.ToInt32(MLC0_Edge2X), Convert.ToInt32(MlC0_Edge2Y));
                            }

                            if (Math.Abs(Bank1Positions.ElementAt(l)) < Math.Abs(jawPositions.X2))
                            {
                                double MLC1_Edge1X = MLC_Edge1X_atCenter + (Bank1Positions.ElementAt(l) * Math.Cos(collimatorAngle)) * 4;
                                double MLC1_Edge1Y = MlC_Edge1Y_atCenter - (Bank1Positions.ElementAt(l) * Math.Sin(collimatorAngle)) * 4;

                                if (Bank1Positions.ElementAt(l - 1) != Bank1Positions.ElementAt(l) && LeafOffset.ElementAt(l) > jawPositions.Y1)
                                {
                                    if (Math.Abs(Bank1Positions.ElementAt(l - 1)) < Math.Abs(jawPositions.X2))
                                    {
                                        double MLC1_Side1X = MLC_Edge1X_atCenter + (Bank1Positions.ElementAt(l - 1) * Math.Cos(collimatorAngle)) * 4;
                                        double MLC1_Side1Y = MlC_Edge1Y_atCenter - (Bank1Positions.ElementAt(l - 1) * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC1_Edge1X), Convert.ToInt32(MLC1_Edge1Y), Convert.ToInt32(MLC1_Side1X), Convert.ToInt32(MLC1_Side1Y));
                                    }
                                    if (Math.Abs(Bank1Positions.ElementAt(l - 1)) > Math.Abs(jawPositions.X2))
                                    {
                                        double MLC1_Side1X = MLC_Edge1X_atCenter + (jawPositions.X2 * Math.Cos(collimatorAngle)) * 4;
                                        double MLC1_Side1Y = MlC_Edge1Y_atCenter - (jawPositions.X2 * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC1_Edge1X), Convert.ToInt32(MLC1_Edge1Y), Convert.ToInt32(MLC1_Side1X), Convert.ToInt32(MLC1_Side1Y));
                                    }

                                }

                                double MLC1_Edge2X = MLC_Edge2X_atCenter + (Bank1Positions.ElementAt(l) * Math.Cos(collimatorAngle)) * 4;
                                double MlC1_Edge2Y = MlC_Edge2Y_atCenter - (Bank1Positions.ElementAt(l) * Math.Sin(collimatorAngle)) * 4;

                                if (Bank1Positions.ElementAt(l + 1) != Bank1Positions.ElementAt(l) && LeafOffset.ElementAt(l) < jawPositions.Y2)
                                {
                                    if (Math.Abs(Bank1Positions.ElementAt(l + 1)) < Math.Abs(jawPositions.X2))
                                    {
                                        double MLC1_Side2X = MLC_Edge2X_atCenter + (Bank1Positions.ElementAt(l + 1) * Math.Cos(collimatorAngle)) * 4;
                                        double MLC1_Side2Y = MlC_Edge2Y_atCenter - (Bank1Positions.ElementAt(l + 1) * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC1_Edge2X), Convert.ToInt32(MlC1_Edge2Y), Convert.ToInt32(MLC1_Side2X), Convert.ToInt32(MLC1_Side2Y));
                                    }
                                    if (Math.Abs(Bank1Positions.ElementAt(l + 1)) > Math.Abs(jawPositions.X2))
                                    {
                                        double MLC1_Side2X = MLC_Edge2X_atCenter + (jawPositions.X2 * Math.Cos(collimatorAngle)) * 4;
                                        double MLC1_Side2Y = MlC_Edge2Y_atCenter - (jawPositions.X2 * Math.Sin(collimatorAngle)) * 4;

                                        graphics.DrawLine(fieldPen, Convert.ToInt32(MLC1_Edge2X), Convert.ToInt32(MlC1_Edge2Y), Convert.ToInt32(MLC1_Side2X), Convert.ToInt32(MLC1_Side2Y));
                                    }

                                }

                                graphics.DrawLine(fieldPen, Convert.ToInt32(MLC1_Edge1X), Convert.ToInt32(MLC1_Edge1Y), Convert.ToInt32(MLC1_Edge2X), Convert.ToInt32(MlC1_Edge2Y));
                            }
                        }
                    }
                }


                StringFormat sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Center
                };


                graphics.DrawLine(fieldPen, Convert.ToInt32(Y2_LowerX), Convert.ToInt32(Y2_LowerY), Convert.ToInt32(Y2_UpperX), Convert.ToInt32(Y2_UpperY));
                graphics.DrawString("Y2", FieldFont, FieldText, new Rectangle(Math.Min(Convert.ToInt32(Y2_LowerX), Convert.ToInt32(Y2_UpperX)) - 20, Math.Min(Convert.ToInt32(Y2_LowerY), Convert.ToInt32(Y2_UpperY)) - 20, (Convert.ToInt32(Y2_UpperX) - Convert.ToInt32(Y2_LowerX)), (Convert.ToInt32(Y2_LowerY) - Convert.ToInt32(Y2_UpperY))), sf);

                graphics.DrawLine(fieldPen, Convert.ToInt32(Y1_LowerX), Convert.ToInt32(Y1_LowerY), Convert.ToInt32(Y1_UpperX), Convert.ToInt32(Y1_UpperY));
                graphics.DrawString("Y1", FieldFont, FieldText, new Rectangle(Math.Min(Convert.ToInt32(Y1_LowerX), Convert.ToInt32(Y1_UpperX)) + 20, Math.Min(Convert.ToInt32(Y1_LowerY), Convert.ToInt32(Y1_UpperY)) + 20, (Convert.ToInt32(Y1_UpperX) - Convert.ToInt32(Y1_LowerX)), (Convert.ToInt32(Y1_LowerY) - Convert.ToInt32(Y1_UpperY))), sf);

                graphics.DrawLine(fieldPen, Convert.ToInt32(Y2_LowerX), Convert.ToInt32(Y2_LowerY), Convert.ToInt32(Y1_LowerX), Convert.ToInt32(Y1_LowerY));
                graphics.DrawString("X1", FieldFont, FieldText, new Rectangle(Math.Min(Convert.ToInt32(Y1_LowerX), Convert.ToInt32(Y2_LowerX)) - 20, Math.Min(Convert.ToInt32(Y1_LowerY), Convert.ToInt32(Y2_LowerY)) + 20, (Convert.ToInt32(Y1_LowerX) - Convert.ToInt32(Y2_LowerX)), (Convert.ToInt32(Y1_LowerY) - Convert.ToInt32(Y2_LowerY))), sf);

                graphics.DrawLine(fieldPen, Convert.ToInt32(Y2_UpperX), Convert.ToInt32(Y2_UpperY), Convert.ToInt32(Y1_UpperX), Convert.ToInt32(Y1_UpperY));
                graphics.DrawString("X2", FieldFont, FieldText, new Rectangle(Math.Min(Convert.ToInt32(Y1_UpperX), Convert.ToInt32(Y2_UpperX)) + 20, Math.Min(Convert.ToInt32(Y1_UpperY), Convert.ToInt32(Y2_UpperY)) - 20, (Convert.ToInt32(Y1_UpperX) - Convert.ToInt32(Y2_UpperX)), (Convert.ToInt32(Y1_UpperY) - Convert.ToInt32(Y2_UpperY))), sf);

                plan.DoseValuePresentation = DoseValuePresentation.Relative;
                graphics.FillEllipse(FieldText, Convert.ToInt32(DoseMax_X), Convert.ToInt32(DoseMax_Y), 5, 5);
                graphics.DrawString(plan.Dose.DoseMax3D.ValueAsString + " %", TextFont, FieldText, Convert.ToInt32(DoseMax_X) + 10, Convert.ToInt32(DoseMax_Y) - 5);

            }




            return bitmap;

        }





    }
}

