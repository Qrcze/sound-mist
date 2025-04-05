using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.Linq;

namespace SoundMist.Controls
{
    internal class WaveSlider : TemplatedControl
    {
        /// <summary>
        /// PointerPosition DirectProperty definition
        /// </summary>
        private static readonly DirectProperty<WaveSlider, double> PointerPositionProperty =
            AvaloniaProperty.RegisterDirect<WaveSlider, double>(nameof(PointerPosition),
                o => o.PointerPosition,
                (o, v) => o.PointerPosition = v);

        /// <summary>
        /// Samples DirectProperty definition
        /// </summary>
        public static readonly DirectProperty<WaveSlider, int[]> SamplesProperty =
            AvaloniaProperty.RegisterDirect<WaveSlider, int[]>(nameof(Samples),
                o => o.Samples,
                (o, v) => o.Samples = v);

        /// <summary>
        /// Maximum DirectProperty definition
        /// </summary>
        public static readonly DirectProperty<WaveSlider, double> MaximumProperty =
            AvaloniaProperty.RegisterDirect<WaveSlider, double>(nameof(Maximum),
                o => o.Maximum,
                (o, v) => o.Maximum = v);

        /// <summary>
        /// Value DirectProperty definition
        /// </summary>
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<WaveSlider, double>(nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        private double _maximum = 1;
        private int[] _samples = Enumerable.Repeat(70, 1800).ToArray();
        private double? _width;
        private Rect[]? _sampleBars;
        private bool _isPressed;
        private double _controlEnd;
        private double _pointerPosition = double.NaN;

        private const int BarSpace = 3;
        private const double FadedOpacity = 0.85;
        private const int HeightDivider = 2;

        /// <summary>
        /// Gets or sets the Maximum property. This DirectProperty
        /// indicates the maximum value of the <see cref="Value"/>.
        /// </summary>
        public double Maximum
        {
            get => _maximum;
            set => SetAndRaise(MaximumProperty, ref _maximum, value);
        }

        /// <summary>
        /// Gets or sets the Value property. This DirectProperty
        /// indicates the current position.
        /// </summary>
        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Gets or sets the Samples property. This DirectProperty
        /// indicates the samples lol
        /// TODO: make it get a link instead, then handle downloading it async+lazy(only download when in view) and by the individual control, instead of having to store it in view/model
        /// </summary>
        public int[] Samples
        {
            get => _samples;
            set
            {
                SetAndRaise(SamplesProperty, ref _samples, value);
                CalculateWidth(true);
            }
        }

        int SamplesWidth => Samples.Length;
        int SamplesHeight { get; set; } = 140 / HeightDivider; //max height of the sample

        /// <summary>
        /// Gets or sets the PointerPosition property. This DirectProperty
        /// indicates the X position of the hovering pointer.
        /// </summary>
        double PointerPosition
        {
            get => _pointerPosition;
            set => SetAndRaise(PointerPositionProperty, ref _pointerPosition, value);
        }

        static WaveSlider()
        {
            AffectsRender<Visual>(ValueProperty, MaximumProperty, SamplesProperty, PointerPositionProperty, IsEnabledProperty);
        }

        public WaveSlider()
        {
            Opacity = FadedOpacity;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            finalSize = base.ArrangeOverride(finalSize);

            //Debug.Print($"final size is: {finalerSize}");
            _width = finalSize.Width;

            //_height = finalSize.Height;

            CalculateWidth(false);

            return finalSize;
        }

        private void CalculateWidth(bool recalculateBars)
        {
            if (SamplesWidth <= 0 || !_width.HasValue)
            {
                _sampleBars = null;
                return;
            }

            int samplesPerBar = 1;
            if (SamplesWidth >= _width.Value / BarSpace)
            {
                int samplesArrayLength = (int)Math.Round(_width.Value / BarSpace);
                samplesPerBar = (int)Math.Ceiling((double)SamplesWidth / samplesArrayLength);
            }

            int barsCount = SamplesWidth / samplesPerBar;

            if (!recalculateBars && (_sampleBars != null && barsCount == _sampleBars.Length))
                return;

            Debug.Print("recalculating sample bars");

            _sampleBars = new Rect[barsCount];

            int barCount = 0;
            int currentSample = 0;
            double avg = 0;
            for (int i = 0; i < SamplesWidth; i++)
            {
                avg += Samples[i];

                if (++barCount < samplesPerBar)
                    continue;
                barCount = 0;

                avg = avg / samplesPerBar / HeightDivider;

                _sampleBars[currentSample] = new(currentSample * BarSpace, SamplesHeight - avg, 2, avg);

                currentSample++;

                avg = 0;
            }

            var last = _sampleBars.Last();
            _controlEnd = last.X + last.Width;
            MinHeight = SamplesHeight;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            PointerPosition = GetValueFromPixelPosition(e.GetPosition(this).X);
            if (_isPressed)
                Value = PointerPosition;
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            Opacity = 1;
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            Opacity = FadedOpacity;
            PointerPosition = double.NaN;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_sampleBars is null)
                return;

            Value = GetValueFromPixelPosition(e.GetPosition(this).X);

            _isPressed = true;
        }

        double GetValueFromPixelPosition(double pos)
        {
            double percent = pos * 100 / _controlEnd;
            return Maximum * percent / 100;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            _isPressed = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (!_width.HasValue || _sampleBars is null)
                return;

            var before = Brushes.OrangeRed;
            var between = Brushes.Orange;
            var after = Brushes.LightGray;

            double percent = Value * 100 / Maximum;
            int currentSample = (int)(_sampleBars.Length * percent / 100);

            //Debug.Print($"rendering waveform, percent: {percent}, currentSample: {currentSample}, _sampleBars.Length: {_sampleBars.Length}");
            //Debug.Print($"control size: {_controlEnd}");

            context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(new Rect(0, 0, _controlEnd, SamplesHeight)));

            Func<int, IBrush> getColorForIndex;
            if (!IsEnabled)
                getColorForIndex = (i) => Brushes.LightGray;
            else if (double.IsNaN(PointerPosition))
                getColorForIndex = (i) => { return i < currentSample ? before : after; };
            else
            {
                int hoveredSample = 0;
                if (!double.IsNaN(PointerPosition))
                {
                    double pointerPercent = PointerPosition * 100 / Maximum;
                    hoveredSample = (int)(_sampleBars.Length * pointerPercent / 100);
                }
                getColorForIndex = (i) =>
                {
                    if (hoveredSample < currentSample)
                    {
                        if (i < hoveredSample)
                            return before;
                        if (i < currentSample)
                            return between;
                        return after;
                    }
                    if (i < currentSample)
                        return before;
                    if (i < hoveredSample)
                        return between;
                    return after;
                };
            }

            for (int i = 0; i < _sampleBars.Length; i++)
            {
                var color = getColorForIndex(i);
                context.DrawRectangle(color, null, _sampleBars[i]);
            }
        }
    }
}