using Avalonia;
using Avalonia.Controls.Primitives;
using System;

namespace SoundMist.Controls
{
    public class UniformGridWithMaxColumns : UniformGrid
    {
        /// <summary>
        /// MaxColumns StyledProperty definition
        /// </summary>
        public static readonly StyledProperty<int> MaxColumnsProperty =
            AvaloniaProperty.Register<UniformGridWithMaxColumns, int>(nameof(MaxColumns));

        /// <summary>
        /// Expected size of the child items
        /// </summary>
        public static readonly StyledProperty<double> ChildWidthProperty =
            AvaloniaProperty.Register<UniformGridWithMaxColumns, double>(nameof(ChildWidth), 1);

        public int MaxColumns
        {
            get => GetValue(MaxColumnsProperty);
            set => SetValue(MaxColumnsProperty, value);
        }

        public double ChildWidth
        {
            get => GetValue(ChildWidthProperty);
            set => SetValue(ChildWidthProperty, value);
        }

        private double _lastWidth = 0;
        private Visual? _parent;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ParentProperty)
            {
                var oldParent = change.OldValue as AvaloniaObject;
                if (oldParent is not null)
                    oldParent.PropertyChanged -= ParentPropertyChanged;
                if (Parent is not null)
                {
                    Parent.PropertyChanged += ParentPropertyChanged;
                    _parent = (Visual)Parent;
                    UpdateColumns();
                }
            }
            else if (change.Property == MaxColumnsProperty || change.Property == ChildWidthProperty)
            {
                UpdateColumns(true);
            }
        }

        private void ParentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty)
                UpdateColumns();
        }

        private void UpdateColumns(bool force = false)
        {
            if (_parent is null || !force && (MaxColumns == 0 || _parent.Bounds.Width == _lastWidth))
                return;

            _lastWidth = _parent.Bounds.Width;

            Columns = (int)Math.Clamp(_parent.Bounds.Width / ChildWidth, 1, MaxColumns);
        }
    }
}