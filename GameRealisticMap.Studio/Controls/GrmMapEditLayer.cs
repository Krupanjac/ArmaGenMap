using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameRealisticMap.Geometries;
using GameRealisticMap.Studio.Behaviors;
using Gemini.Framework;

namespace GameRealisticMap.Studio.Controls
{
    public sealed class GrmMapEditLayer : GrmMapLayerGroup
    {
        public static readonly DependencyProperty ClearSelectionProperty =
            DependencyProperty.Register(nameof(ClearSelection), typeof(ICommand), typeof(GrmMapEditLayer), new PropertyMetadata(null));

        public static readonly DependencyProperty InsertPointCommandProperty =
            DependencyProperty.Register(nameof(InsertPointCommand), typeof(ICommand), typeof(GrmMapEditLayer), new PropertyMetadata(null));

        public static readonly DependencyProperty DeleteSelectionCommandProperty =
            DependencyProperty.Register(nameof(DeleteSelectionCommand), typeof(ICommand), typeof(GrmMapEditLayer), new PropertyMetadata(null));

        public static readonly DependencyProperty EditPointsProperty =
            DependencyProperty.Register(nameof(EditPoints), typeof(IEditablePointCollection), typeof(GrmMapEditLayer), new PropertyMetadata(null, EditPoints_Changed));

        public static readonly DependencyProperty OutlineProperty =
            DependencyProperty.Register(nameof(Outline), typeof(IEnumerable<IReadOnlyCollection<TerrainPoint>>), typeof(GrmMapEditLayer), new PropertyMetadata(null, Outline_Changed));

        public static readonly DependencyProperty EditModeProperty =
            DependencyProperty.Register(nameof(EditMode), typeof(GrmMapEditMode), typeof(GrmMapEditLayer), new PropertyMetadata(GrmMapEditMode.None, EditMode_Changed));

        private readonly GrmMapEditLayerOverlay overlay;
        private IEditablePointCollection? editPoints;
        private bool isPreviewEnd;
        private Point? selectionStart;
        private Rect selectionRect;

        public GrmMapEditLayer()
        {
            overlay = new GrmMapEditLayerOverlay(this);
            Focusable = true;
        }

        public Rect SelectionRect => selectionRect;

        public GrmMapEditMode EditMode
        {
            get { return (GrmMapEditMode)GetValue(EditModeProperty); }
            set { SetValue(EditModeProperty, value); }
        }

        public IEditablePointCollection? EditPoints
        {
            get { return (IEditablePointCollection?)GetValue(EditPointsProperty); }
            set { SetValue(EditPointsProperty, value); }
        }

        public IEnumerable<IReadOnlyCollection<TerrainPoint>>? Outline
        {
            get { return (IEnumerable<IReadOnlyCollection<TerrainPoint>>?)GetValue(OutlineProperty); }
            set { SetValue(OutlineProperty, value); }
        }

        public ICommand? ClearSelection
        {
            get { return (ICommand?)GetValue(ClearSelectionProperty); }
            set { SetValue(ClearSelectionProperty, value); }
        }

        public ICommand? InsertPointCommand
        {
            get { return (ICommand?)GetValue(InsertPointCommandProperty); }
            set { SetValue(InsertPointCommandProperty, value); }
        }

        public ICommand? DeleteSelectionCommand
        {
            get { return (ICommand?)GetValue(DeleteSelectionCommandProperty); }
            set { SetValue(DeleteSelectionCommandProperty, value); }
        }

        public ContextMenu? SelectionContextMenu { get; set; }

        public bool IsPreviewEnd => isPreviewEnd;

        public static readonly DependencyProperty MultiEditPointsProperty =
            DependencyProperty.Register(nameof(MultiEditPoints), typeof(IEnumerable<IEditablePointCollection>), typeof(GrmMapEditLayer), new PropertyMetadata(null, MultiEditPoints_Changed));

        public IEnumerable<IEditablePointCollection>? MultiEditPoints
        {
            get { return (IEnumerable<IEditablePointCollection>?)GetValue(MultiEditPointsProperty); }
            set { SetValue(MultiEditPointsProperty, value); }
        }

        private static void MultiEditPoints_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GrmMapEditLayer)d).ChangeMultiEditPoints((IEnumerable<IEditablePointCollection>?)e.OldValue, (IEnumerable<IEditablePointCollection>?)e.NewValue);
        }

        private void ChangeMultiEditPoints(IEnumerable<IEditablePointCollection>? oldValue, IEnumerable<IEditablePointCollection>? newValue)
        {
            if (oldValue != null)
            {
                foreach (var item in oldValue)
                {
                    item.CollectionChanged -= SelectionPoints_CollectionChanged;
                    item.PropertyChanged -= SelectionPoints_PropertyChanged;
                }
            }
            if (newValue != null)
            {
                foreach (var item in newValue)
                {
                    item.CollectionChanged += SelectionPoints_CollectionChanged;
                    item.PropertyChanged += SelectionPoints_PropertyChanged;
                }
            }
            CreatePoints();
            overlay.InvalidateVisual();
        }

        private static void EditPoints_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GrmMapEditLayer)d).ChangeEditPoints((IEditablePointCollection?)e.OldValue, (IEditablePointCollection?)e.NewValue);
        }

        private static void EditMode_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GrmMapEditLayer)d).ChangeEditMode((GrmMapEditMode)e.OldValue, (GrmMapEditMode)e.NewValue);
        }

        private static void Outline_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GrmMapEditLayer)d).ChangeOutline((IEnumerable<IReadOnlyCollection<TerrainPoint>>?)e.OldValue, (IEnumerable<IReadOnlyCollection<TerrainPoint>>?)e.NewValue);
        }

        private void ChangeOutline(IEnumerable<IReadOnlyCollection<TerrainPoint>>? oldValue, IEnumerable<IReadOnlyCollection<TerrainPoint>>? newValue)
        {
            overlay.InvalidateVisual();
        }

        private void ChangeEditMode(GrmMapEditMode oldValue, GrmMapEditMode newValue)
        {
            if (newValue == GrmMapEditMode.None)
            {
                Cursor = null;
            }
            else
            {
                Cursor = Cursors.Cross;
                if (newValue == GrmMapEditMode.ContinuePath)
                {
                    if (!InternalChildren.OfType<GrmMapDraggableSquare>().Any(p => p.IsFocused))
                    {
                        isPreviewEnd = false;
                        InternalChildren.OfType<GrmMapDraggableSquare>().First(p => p.Index == 0).Focus();
                    }
                }
            }
            overlay.InvalidateVisual();
        }

        private void ChangeEditPoints(IEditablePointCollection? oldValue, IEditablePointCollection? newValue)
        {
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= SelectionPoints_CollectionChanged;
                oldValue.PropertyChanged -= SelectionPoints_PropertyChanged;
            }
            if (newValue != null)
            {
                newValue.CollectionChanged += SelectionPoints_CollectionChanged;
                newValue.PropertyChanged += SelectionPoints_PropertyChanged;
            }
            editPoints = newValue;
            CreatePoints();
            overlay.InvalidateVisual();
        }

        private void SelectionPoints_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnViewportChanged();
        }

        private void SelectionPoints_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            CreatePoints();
            OnViewportChanged();
        }

        private void CreatePoints()
        {
            if (!InternalChildren.Contains(overlay))
            {
                InternalChildren.Add(overlay);
            }
            var first = InternalChildren.OfType<GrmMapDraggableSquare>().FirstOrDefault();
            if (first != null)
            {
                var index = InternalChildren.IndexOf(first);
                InternalChildren.RemoveRange(index, InternalChildren.Count - index);
            }
            if (editPoints != null)
            {
                CreatePoints(editPoints);
            }
            else if (MultiEditPoints != null)
            {
                foreach(var collection in MultiEditPoints)
                {
                    CreatePoints(collection);
                }
            }
        }

        private void CreatePoints(IEnumerable<TerrainPoint> points)
        {
            var collection = points as IEditablePointCollection;
            if (collection != null)
            {
                var pos = 0;
                foreach (var point in points)
                {
                    InternalChildren.Add(new GrmMapDraggableSquare(this, collection, point, pos));
                    pos++;
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var parent = ParentMap;
            if (parent != null)
            {
                var mode = EditMode;
                if (mode == GrmMapEditMode.InsertPoint)
                {
                    InsertPointCommand?.Execute(parent.ViewportCoordinates(e.GetPosition(parent)));
                    e.Handled = true;
                    return;
                }
                if (mode == GrmMapEditMode.ContinuePath)
                {
                    ContinuePath(parent.ViewportCoordinates(e.GetPosition(parent)), e);
                    e.Handled = true;
                    return;
                }
                if (e.ClickCount == 2)
                {
                    OnDoubleClick(parent.ViewportCoordinates(e.GetPosition(parent)), e);
                    return;
                }
                if (mode == GrmMapEditMode.None && e.ClickCount == 1)
                {
                    selectionStart = e.GetPosition(this);
                    CaptureMouse();
                    Focus();
                }
            }
            if (Keyboard.Modifiers == ModifierKeys.None && selectionStart == null)
            {
                ClearSelection?.Execute(null);
            }
            base.OnMouseLeftButtonDown(e);
        }

        private void OnDoubleClick(TerrainPoint terrainPoint, MouseButtonEventArgs e)
        {
            var selectionPoints = editPoints;
            if (selectionPoints != null && selectionPoints.CanInsertBetween)
            {
                var path = new TerrainPath(selectionPoints.ToList());
                if (path.Distance(terrainPoint) < 2)
                {
                    var index = path.NearestSegmentIndex(terrainPoint) + 1;
                    selectionPoints.Insert(index, terrainPoint);
                    e.Handled = true;
                }
            }
        }

        private void ContinuePath(TerrainPoint terrainPoint, MouseButtonEventArgs e)
        {
            if (editPoints != null && CanInsert(editPoints, out var focused))
            {
                if (focused.Index == 0)
                {
                    editPoints.Insert(0, terrainPoint);
                }
                else
                {
                    editPoints.Add(terrainPoint);
                }
                e.Handled = true;
            }
        }

        internal void OnPointPositionPreviewChange(GrmMapDraggableSquare p)
        {
            p.Collection.PreviewSet(p.Index, p.TerrainPoint);
        }

        internal void OnPointPositionChanged(GrmMapDraggableSquare p, TerrainPoint oldValue)
        {
            p.Collection.Set(p.Index, oldValue, p.TerrainPoint);
        }

        internal void PointPositionDelete(GrmMapDraggableSquare p)
        {
            p.Collection.RemoveAt(p.Index);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var rect = new Rect(new Point(), RenderSize);
            var map = ParentMap;
            if (map != null)
            {
                foreach (UIElement internalChild in InternalChildren)
                {
                    if (internalChild is GrmMapDraggableSquare square)
                    {
                        var s = internalChild.DesiredSize;
                        var p = map.ProjectViewport(square.TerrainPoint) - new System.Windows.Vector(s.Width / 2, s.Height / 2);
                        internalChild.Arrange(new Rect(p, s));
                    }
                    else
                    {
                        internalChild.Arrange(rect);
                    }
                }
            }
            return arrangeSize;
        }

        internal void OpenItemContextMenu(GrmMapDraggableSquare square, MouseButtonEventArgs e)
        {
            var collection = square.Collection;
            if (collection != null)
            {
                var menu = new ContextMenu(); // To do on Xaml side ?
                if (collection.CanSplit)
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = "Split into two paths",
                        Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/GameRealisticMap.Studio;component/Resources/Tools/split.png")) },
                        IsEnabled = square.Index > 0 && square.Index < collection.Count - 1,
                        Command = new RelayCommand(_ => collection.SplitAt(square.Index))
                    });
                }
                if (collection.CanInsertAtEnds)
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = "Continue path",
                        Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/GameRealisticMap.Studio;component/Resources/Tools/path.png")) },
                        IsEnabled = square.Index == 0 || square.Index == collection.Count - 1,
                        Command = new RelayCommand(_ => ContinuePathFrom(square))
                    });
                }
                if (collection.CanDeletePoint)
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = "Delete point",
                        Icon = new Image() { Source = new BitmapImage(new Uri("pack://application:,,,/GameRealisticMap.Studio;component/Resources/Tools/bin.png")) },
                        Command = new RelayCommand(_ => PointPositionDelete(square))
                    });
                }
                if (menu.Items.Count > 0)
                {
                    ButtonBehaviors.ShowButtonContextMenu(square, menu);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            UpdatePreviewState(e.Key);
            if (e.Key == Key.Delete)
            {
                var selectedSquares = InternalChildren.OfType<GrmMapDraggableSquare>().Where(s => s.IsSelected || s.IsFocused).OrderByDescending(s => s.Index).ToList();
                if (selectedSquares.Any())
                {
                    foreach (var s in selectedSquares)
                    {
                        PointPositionDelete(s);
                    }
                }
                else
                {
                    DeleteSelectionCommand?.Execute(null);
                }
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            UpdatePreviewState(e.Key);
        }

        private void UpdatePreviewState(Key key)
        {
            if (key == Key.LeftCtrl || key == Key.RightCtrl)
            {
                var currentState = EditMode;
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (currentState != GrmMapEditMode.ContinuePath && editPoints != null && CanInsert(editPoints, out var focused))
                    {
                        ContinuePathFrom(focused);
                    }
                }
                else
                {
                    if (currentState != GrmMapEditMode.None)
                    {
                        EditMode = GrmMapEditMode.None;
                    }
                }
            }
        }

        private void ContinuePathFrom(GrmMapDraggableSquare focused)
        {
            isPreviewEnd = focused.Index != 0;
            EditMode = GrmMapEditMode.ContinuePath;
            editPoints = focused.Collection;
        }

        private bool CanInsert(IEditablePointCollection selectionPoints, [NotNullWhen(true)] out GrmMapDraggableSquare? focused)
        {
            if (selectionPoints.CanInsertAtEnds)
            {
                focused = InternalChildren.OfType<GrmMapDraggableSquare>().FirstOrDefault(p => p.IsFocused && p.Collection == selectionPoints);
                return focused != null && (focused.Index == 0 || focused.Index == selectionPoints.Count - 1);
            }
            focused = null;
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (EditMode == GrmMapEditMode.ContinuePath)
            {
                overlay.InvalidateVisual();
            }
            if (selectionStart != null)
            {
                var current = e.GetPosition(this);
                selectionRect = new Rect(selectionStart.Value, current);
                
                if (EditPoints != null || MultiEditPoints != null)
                {
                    var map = ParentMap;
                    if (map != null)
                    {
                        var squares = InternalChildren.OfType<GrmMapDraggableSquare>().ToList();
                        foreach (var square in squares)
                        {
                            var pos = map.ProjectViewport(square.TerrainPoint);
                            if (selectionRect.Contains(new Point(pos.X, pos.Y)))
                            {
                                square.IsSelected = true;
                            }
                            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.None)
                            {
                                square.IsSelected = false;
                            }
                        }
                    }
                }

                overlay.InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (selectionStart != null)
            {
                ReleaseMouseCapture();
                selectionStart = null;

                var map = ParentMap;
                if (map != null && selectionRect.Width > 5 && selectionRect.Height > 5)
                {
                    if (EditPoints != null || MultiEditPoints != null)
                    {
                        var focused = InternalChildren.OfType<GrmMapDraggableSquare>().FirstOrDefault(s => s.IsSelected);
                        focused?.Focus();
                    }
                    else
                    {
                        var p1 = map.ViewportCoordinates(selectionRect.TopLeft);
                        var p2 = map.ViewportCoordinates(selectionRect.TopRight);
                        var p3 = map.ViewportCoordinates(selectionRect.BottomRight);
                        var p4 = map.ViewportCoordinates(selectionRect.BottomLeft);
                        var poly = new TerrainPolygon(new List<TerrainPoint>() { p1, p2, p3, p4, p1 });

                        foreach (var child in InternalChildren.OfType<GrmMapArma3>().ToList())
                        {
                            child.SelectItemsIn(poly, Keyboard.Modifiers);
                        }
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    ClearSelection?.Execute(null);
                    foreach (var s in InternalChildren.OfType<GrmMapDraggableSquare>()) s.IsSelected = false;
                }
                selectionRect = Rect.Empty;
                overlay.InvalidateVisual();
            }
            base.OnMouseLeftButtonUp(e);
        }

        public override void OnViewportChanged()
        {
            base.OnViewportChanged();
            InvalidateArrange();
            overlay.InvalidateVisual();
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            var parent = ParentMap;
            if (parent != null)
            {
                var outline = Outline;
                if (outline != null)
                {
                    var point = parent.ViewportCoordinates(e.GetPosition(parent));
                    foreach (var segment in outline)
                    {
                        if (HitTestPath(point, segment))
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }
                else if ( editPoints != null)
                {
                    var point = parent.ViewportCoordinates(e.GetPosition(parent));
                    if (HitTestPath(point, editPoints))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
            base.OnMouseRightButtonDown(e);
        }

        private static bool HitTestPath(TerrainPoint point, IReadOnlyCollection<TerrainPoint> segment)
        {
            if (segment.Count == 0)
            {
                return false;
            }
            if (segment is IEditablePointCollection a && a.IsObjectSquare)
            {
                return new TerrainPolygon(segment.Take(4).ToList()).Contains(point);
            }
            if (segment.Count == 1)
            {
                return (segment.First().Vector - point.Vector).Length() < 2;
            }
            return new TerrainPath(segment.ToList()).Distance(point) < 2;
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            var context = SelectionContextMenu;
            if (context != null)
            {
                context.PlacementTarget = this;
                context.Placement = PlacementMode.MousePoint;
                context.IsOpen = true;
            }

            // TODO: Open context menu
            base.OnMouseRightButtonUp(e);
        }

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }
    }
}
