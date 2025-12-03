using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GameRealisticMap.Geometries;

namespace GameRealisticMap.Studio.Controls
{
    internal sealed class GrmMapDraggableSquare : FrameworkElement
    {
        private Point start;
        private Vector initialOffset;
        private TerrainPoint initialPoint;
        private readonly GrmMapEditLayer map;

        public GrmMapDraggableSquare(GrmMapEditLayer map, IEditablePointCollection collection, TerrainPoint terrainPoint, int index)
        {
            this.map = map;
            this.Collection = collection;
            Width = 12;
            Height = 12;
            TerrainPoint = initialPoint = terrainPoint;
            Focusable = true;
            Index = index;
        }

        public IEditablePointCollection Collection { get; }

        public SolidColorBrush Fill { get; set; } = new SolidColorBrush(Colors.White);

        public SolidColorBrush FillFocus { get; set; } = new SolidColorBrush(Colors.Black);

        public Pen Pen { get; set; } = new Pen(new SolidColorBrush(Colors.Black), 1);

        public TerrainPoint TerrainPoint { get; set; }

        public int Index {  get; set; }

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(GrmMapDraggableSquare), new PropertyMetadata(false, (d, e) => ((GrmMapDraggableSquare)d).InvalidateVisual()));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Fill, Pen, new Rect(RenderSize));
            if ( IsFocused || IsSelected )
            {
                drawingContext.DrawRectangle(FillFocus, null, new Rect(new Point(2, 2), new Size(RenderSize.Width-4, RenderSize.Height-4)));
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (map.EditMode != GrmMapEditMode.None)
            {
                if (IsFocused)
                {
                    map.EditMode = GrmMapEditMode.None;
                    e.Handled = true;
                }
                return;
            }

            Focus();
            IsSelected = true;

            start = e.GetPosition(map.ParentMap!);
            initialOffset = VisualTreeHelper.GetOffset(this);
            initialPoint = TerrainPoint;

            e.Handled = true;
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            InvalidateVisual();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (IsMouseCaptured)
            {
                var pos = e.GetPosition(map.ParentMap!);
                var delta = start - pos;
                var s = DesiredSize;
                var p = initialOffset - delta;
                Arrange(new Rect(new Point(p.X , p.Y), s));
                TerrainPoint = map.ParentMap!.ViewportCoordinatesCenter(new Point(p.X, p.Y), RenderSize);
                map.OnPointPositionPreviewChange(this);
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (IsFocused)
                {
                    Cursor = Cursors.SizeAll;
                    CaptureMouse();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (IsFocused && IsMouseCaptured)
            {
                if (!initialPoint.Vector.Equals(TerrainPoint.Vector))
                {
                    map.OnPointPositionChanged(this, initialPoint);
                }
            }

            ReleaseMouseCapture();
            Cursor = null;

            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if ( e.Key == Key.Delete)
            {
                e.Handled = true;
                map.PointPositionDelete(this);
            }

            base.OnKeyUp(e);
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            e.Handled = true;
            base.OnMouseRightButtonDown(e);
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            e.Handled = true;
            map.OpenItemContextMenu(this, e);
            base.OnMouseRightButtonUp(e);
        }
    }
}
