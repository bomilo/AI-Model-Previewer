using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
using System.Windows.Forms;
using System.Windows.Media;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace ChartingApp
{

    public partial class Form1 : Form
    {
        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbfont, uint cbfont, IntPtr pdv, [In] ref uint pcFonts);

        System.Drawing.FontFamily ff;
        System.Drawing.Font font;

        int predictionCounter = 0;
        int realMarketCounter = 0;
        private double previousCalculatedPrice = 0;
        private bool previousPredictedPriceIsAHit;
        private double previousRealPrice;
        private double previousPredictedPrice;

        public ChartValues<MeasureModel> PredictionValues { get; set; }
        public ChartValues<MeasureModel> RealMarketValues { get; set; }
        public Timer PredictionTimer { get; set; }
        public Timer RealMarketTimer { get; set; }

        public Form1()
        {
            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //Save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            //the ChartValues property will store our values array
            PredictionValues = new ChartValues<MeasureModel>();
            RealMarketValues = new ChartValues<MeasureModel>();

            Brush BrushStrokePrediction = Brushes.MediumVioletRed;
            Brush BrushFillPrediction = Brushes.Transparent;

            //Brush BrushFillReal = new SolidColorBrush(Color.FromArgb(100, 159, 223, 188));
            Brush BrushFillReal = Brushes.Transparent;
            Brush BrushStrokeReal = Brushes.DarkGreen;

            cartesianChart1.Series = new SeriesCollection
            {
                new LineSeries
                {
                    Values = PredictionValues,
                    LineSmoothness = 0,
                    PointGeometrySize = 6,
                    Stroke = BrushStrokePrediction,
                    StrokeThickness = 2,
                    Title = "Prediction",
                    Fill = BrushFillPrediction,

                },
                new LineSeries
                {
                    Values = RealMarketValues,
                    LineSmoothness = 0,
                    PointGeometrySize = 6,
                    StrokeThickness = 2,
                    Title = "RealMarket",
                    Stroke = BrushStrokeReal,
                    Fill = BrushFillReal
                },

            };

            FontFamily family = new FontFamily("Ubuntu Light");

            cartesianChart1.AxisX.Add(new Axis
            {
                Title = "Time",
                FontSize = 12,
                FontFamily = family,
                DisableAnimations = false,
                LabelFormatter = value => new DateTime((long)value).ToString("HH:mm:ss:fff"),
                LabelsRotation = -45,
                Visibility = System.Windows.Visibility.Hidden,
                MinValue = DataHolder.PredictionTimes[0].Ticks - new TimeSpan(0, 2, 0).Ticks,
                MaxValue = DataHolder.PredictionTimes[DataHolder.PredictionTimes.Length - 1].Ticks + new TimeSpan(0, 2, 0).Ticks,
                Separator = new Separator
                {
                    Step = TimeSpan.FromSeconds(30).Ticks,
                    StrokeThickness = 0.0,
                }
            });

            cartesianChart1.AxisY.Add(new Axis
            {
                Title = "Price",
                FontSize = 12,
                FontFamily = family,
                ShowLabels = true,
                IsMerged = true,
                MaxValue = 479.0000,
                MinValue = 477.0000,
                Separator = new Separator
                {
                    StrokeThickness = 0.1,
                    StrokeDashArray = new DoubleCollection(new double[] { 4 }),
                    Stroke = new SolidColorBrush(Color.FromRgb(64, 79, 86)),
                }
            });

            cartesianChart1.Zoom = ZoomingOptions.Xy;
        }

        #region Font Related Stuff

        private void LoadFont()
        {
            byte[] fontAray = Properties.Resources.Ubuntu_L;
            int dataLenght = Properties.Resources.Ubuntu_L.Length;
            IntPtr ptrData = Marshal.AllocCoTaskMem(dataLenght);

            Marshal.Copy(fontAray, 0, ptrData, dataLenght);

            uint cFonts = 0;

            AddFontMemResourceEx(ptrData, (uint)fontAray.Length, IntPtr.Zero, ref cFonts);

            PrivateFontCollection pfc = new PrivateFontCollection();
            pfc.AddMemoryFont(ptrData, dataLenght);

            Marshal.FreeCoTaskMem(ptrData);

            ff = pfc.Families[0];
            font = new System.Drawing.Font(ff, 15f, System.Drawing.FontStyle.Bold);

        }

        private void AllocFont(System.Drawing.Font f, Control c, float size, System.Drawing.FontStyle fontStyle = System.Drawing.FontStyle.Regular)
        {
            c.Font = new System.Drawing.Font(ff, size, fontStyle);
        }

        #endregion

        private void SetAxisLimits(DateTime time)
        {
            //cartesianChart1.AxisX[0].MaxValue = time.Ticks + TimeSpan.FromSeconds(1).Ticks; // lets force the axis to be 100ms ahead
            //cartesianChart1.AxisX[0].MinValue = time.Ticks - TimeSpan.FromSeconds(480).Ticks; //we only care about the last 8 seconds
        }

        public string GetInstrumentDecription(string instrument)
        {
            switch (instrument)
            {
                case "STMN":
                    {
                        return "Straumann Holding AG";
                    }
                default:
                    {
                        return "";
                    }
            }
        }

        private void PredictionTimerOnTickHandler(object sender, EventArgs eventArgs)
        {

            if (predictionCounter > DataHolder.PredictionPrices.Length - 1)
            {
                PredictionTimer.Stop();
                return;
            }

            AddNewPoint(DataHolder.PredictionTimes, DataHolder.PredictionPrices, PredictionValues, predictionCounter, true);
            predictionCounter++;
        }

        private void AddNewPoint(DateTime[] times, double[] prices, ChartValues<MeasureModel> destinationLSeriePointcollection, int counter, bool applyMathModel = false)
        {
            #region 

            double price = 0;

            if (applyMathModel)
            {
                // Calculate new predictedPrice by using mathematical model

                double predictedPrice = prices[counter];

                if (previousCalculatedPrice == 0)
                {
                    price = ReturnNormalizedPredictedPrice(predictedPrice);
                }
                else
                {
                    if (previousPredictedPriceIsAHit)
                    {
                        price = ReturnNormalizedPredictedPrice(previousRealPrice - previousPredictedPrice + predictedPrice);
                    }
                    else
                    {
                        price = ReturnNormalizedPredictedPrice(previousRealPrice - previousCalculatedPrice + predictedPrice);
                    }
                }

                previousCalculatedPrice = price;
                previousPredictedPrice = predictedPrice;
                previousPredictedPriceIsAHit = (price == DataHolder.RealPrices[counter]) ? true : false;
                previousRealPrice = DataHolder.RealPrices[counter];
            }
            else
            {
                price = prices[counter];
            }

            #endregion

            MeasureModel Point = new MeasureModel();
            Point.DateTime = times[counter];
            Point.Value = price;
            destinationLSeriePointcollection.Add(Point);
        }

        private void MarketSimulationExecutionTimerOnTickHandler(object sender, EventArgs e)
        {
            if (realMarketCounter > DataHolder.RealPrices.Length - 1)
            {
                RealMarketTimer.Stop();
                return;
            }

            AddNewPoint(DataHolder.RealTimes, DataHolder.RealPrices, RealMarketValues, realMarketCounter);
            realMarketCounter++;


            //lets only use the last 30 values
            //if (RealMarketValues.Count > 30) RealMarketValues.RemoveAt(0);
        }

        private double ReturnNormalizedPredictedPrice(double calculatedPredictedPrice)
        {
            double tickSize = DataHolder.TickTable[textBox1.Text];
            return Math.Round(Math.Floor(calculatedPredictedPrice / tickSize) * tickSize, 2);
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text != String.Empty)
            {
                label6.Text = textBox1.Text;
                label7.Text = "SWISS Exchange";

                #region Prediction related

                //if (PredictionTimer != null)
                //{
                //    predictionCounter = 0;
                //    PredictionTimer.Stop();
                //    PredictionTimer.Dispose();
                //    PredictionTimer = null;
                //    cartesianChart1.Series[0].Values.Clear();
                //}

                //PredictionTimer = new Timer
                //{
                //    Interval = 750
                //};
                //PredictionTimer.Tick += PredictionTimerOnTickHandler;
                //PredictionTimer.Start();


                #endregion

                #region Market Execution Simulation

                //if (RealMarketTimer != null)
                //{
                //    realMarketCounter = 0;
                //    RealMarketTimer.Stop();
                //    RealMarketTimer.Dispose();
                //    RealMarketTimer = null;
                //    cartesianChart1.Series[1].Values.Clear();
                //}

                //RealMarketTimer = new Timer
                //{
                //    Interval = 1000
                //};
                //RealMarketTimer.Tick += MarketSimulationExecutionTimerOnTickHandler;
                //RealMarketTimer.Start();

                #endregion

                #region Quick Testing Code

                cartesianChart1.Series[0].Values.Clear();
                cartesianChart1.Series[1].Values.Clear();

                System.Threading.Thread dedicatedThread;

                System.Threading.ThreadStart threadStart = () =>
                {
                    for (int i = 0; i < DataHolder.PredictionTimes.Length; i++)
                    {
                        AddNewPoint(DataHolder.PredictionTimes, DataHolder.PredictionPrices, PredictionValues, predictionCounter, true);
                        predictionCounter++;
                        System.Threading.Thread.Sleep(1000);
                        AddNewPoint(DataHolder.RealTimes, DataHolder.RealPrices, RealMarketValues, realMarketCounter);
                        realMarketCounter++;
                    }

                    predictionCounter = 0;
                    realMarketCounter = 0;
                };

                dedicatedThread = new System.Threading.Thread(threadStart);

                if (!dedicatedThread.IsAlive)
                {
                    dedicatedThread.Start();
                }
                else
                {
                    dedicatedThread.Abort();
                    predictionCounter = 0;
                    realMarketCounter = 0;
                    dedicatedThread.Start();
                }

                    //for (int i = 0; i < DataHolder.PredictionTimes.Length; i++)
                    //{
                    //    MeasureModel PredictionPoint = new MeasureModel();
                    //    MeasureModel RealMarketPoint;
                    //    PredictionPoint.DateTime = DataHolder.PredictionTimes[predictionCounter];
                    //    PredictionPoint.Value = DataHolder.PredictionPrices[predictionCounter];
                    //    PredictionValues.Add(PredictionPoint);
                    //    predictionCounter++;

                    //    while (true)
                    //    {
                    //        if (realMarketCounter < DataHolder.RealTimes.Length)
                    //        {
                    //            RealMarketPoint = new MeasureModel();
                    //            RealMarketPoint.DateTime = DataHolder.RealTimes[realMarketCounter];
                    //            RealMarketPoint.Value = DataHolder.RealPrices[realMarketCounter];
                    //            RealMarketValues.Add(RealMarketPoint);
                    //            realMarketCounter++;

                    //            //System.Threading.Thread.Sleep(100);

                    //            if (realMarketCounter % 5 == 0)
                    //            {
                    //                realMarketCounter++;
                    //                break;
                    //            }
                    //        }
                    //        else
                    //        {
                    //            break;
                    //        }
                    //    }
                    //}

                    //predictionCounter = 0;
                    //realMarketCounter = 0;

                #endregion
            }
        }

        private void TextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            label3.Text = GetInstrumentDecription(textBox1.Text);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadFont();
            AllocFont(font, this.label5, 12f, System.Drawing.FontStyle.Bold);
            AllocFont(font, this.label5, 8f, System.Drawing.FontStyle.Italic);
            AllocFont(font, this.label1, 10f);
            AllocFont(font, this.label2, 10f);
            AllocFont(font, this.label3, 8f);
            AllocFont(font, this.button1, 10f);
            AllocFont(font, this.comboBox1, 8f);
            AllocFont(font, this.textBox1, 8f);
            AllocFont(font, this.label6, 15f, System.Drawing.FontStyle.Bold);

        }
    }

    public class MeasureModel
    {
        public DateTime DateTime { get; set; }
        public double Value { get; set; }
    }
}