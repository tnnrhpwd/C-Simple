using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Add this namespace for Color and Colors
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CSimple.Services.AppModeService;
using CSimple.ViewModels;
using System.Diagnostics;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage, IDrawable
    {
        private OrientPageViewModel _viewModel;
        private NodeViewModel _draggedNode = null;
        private PointF _dragStartPoint;
        private bool _isDrawingConnection = false;
        private PointF _connectionEndPoint;

        // Property to bind GraphicsView.Drawable to
        public IDrawable NodeDrawable => this;

        public OrientPage(OrientPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Optional: Set alert/actionsheet implementations if needed by VM
            _viewModel.ShowAlert = DisplayAlert;
            _viewModel.ShowActionSheet = (title, cancel, destruction, buttons) => DisplayActionSheet(title, cancel, destruction, buttons);

            // Invalidate canvas when VM collections change (basic example)
            _viewModel.Nodes.CollectionChanged += (s, e) => NodeCanvas?.Invalidate();
            _viewModel.Connections.CollectionChanged += (s, e) => NodeCanvas?.Invalidate();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine($"OrientPage Appearing.");
            NodeCanvas?.Invalidate(); // Ensure canvas draws on appearing
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            Debug.WriteLine($"OrientPage NavigatedTo.");
        }

        // --- IDrawable Implementation ---
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // Clear canvas (optional, depends on how invalidation works)
            // canvas.FillColor = Colors.WhiteSmoke;
            // canvas.FillRectangle(dirtyRect);

            if (_viewModel == null) return;

            // 1. Draw Connections
            canvas.StrokeColor = Colors.Gray;
            canvas.StrokeSize = 2;
            foreach (var connection in _viewModel.Connections)
            {
                var sourceNode = _viewModel.Nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                var targetNode = _viewModel.Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);

                if (sourceNode != null && targetNode != null)
                {
                    // Simple line - could be enhanced with arrows, curves
                    PointF start = GetConnectionPoint(sourceNode, targetNode.Position);
                    PointF end = GetConnectionPoint(targetNode, sourceNode.Position);
                    canvas.DrawLine(start, end);
                }
            }

            // 2. Draw Temporary Connection Line (if drawing)
            if (_isDrawingConnection && _viewModel._temporaryConnectionState is NodeViewModel startNode)
            {
                canvas.StrokeColor = Colors.DodgerBlue;
                canvas.StrokeDashPattern = new float[] { 4, 4 };
                PointF tempStart = GetConnectionPoint(startNode, _connectionEndPoint);
                canvas.DrawLine(tempStart, _connectionEndPoint);
                canvas.StrokeDashPattern = null; // Reset dash pattern
            }


            // 3. Draw Nodes
            foreach (var node in _viewModel.Nodes)
            {
                RectF nodeRect = new RectF(node.Position, node.Size);

                // Node background and border
                canvas.FillColor = node.Type == NodeType.Input ? Colors.LightSkyBlue : Colors.LightGoldenrodYellow;
                if (node.IsSelected)
                {
                    canvas.StrokeColor = Colors.OrangeRed;
                    canvas.StrokeSize = 3;
                }
                else
                {
                    canvas.StrokeColor = Colors.DarkGray;
                    canvas.StrokeSize = 1;
                }

                canvas.FillRoundedRectangle(nodeRect, 5);
                canvas.DrawRoundedRectangle(nodeRect, 5);

                // Node text
                canvas.FontColor = Colors.Black;
                canvas.FontSize = 12;
                canvas.DrawString(node.Name, nodeRect, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }

        // Helper to get center point for connections (can be improved)
        private PointF GetConnectionPoint(NodeViewModel node, PointF targetPoint)
        {
            // Simple center point - could be improved to connect to nearest side
            return new PointF(node.Position.X + node.Size.Width / 2, node.Position.Y + node.Size.Height / 2);
        }


        // --- Interaction Handlers ---

        void OnCanvasStartInteraction(object sender, TouchEventArgs e)
        {
            PointF touchPoint = e.Touches[0];
            var tappedNode = _viewModel.GetNodeAtPoint(touchPoint);

            if (tappedNode != null)
            {
                // Check if tapping on a connection handle (if implemented)
                // For now, assume tap starts drag or connection

                // Simple check: Shift+Tap to start connection? Or dedicated handles?
                // Let's assume simple drag for now, connection logic needs refinement.

                _viewModel.SelectedNode = tappedNode; // Select the node
                _draggedNode = tappedNode;
                _dragStartPoint = touchPoint;
                _isDrawingConnection = false; // Reset connection drawing

                // Placeholder: How to initiate connection drawing?
                // Maybe a long press, or specific handles on the node?
                // For demo: Let's say tapping near the right edge starts connection
                if (touchPoint.X > tappedNode.Position.X + tappedNode.Size.Width - 15)
                {
                    _viewModel.StartConnection(tappedNode);
                    _isDrawingConnection = true;
                    _connectionEndPoint = touchPoint; // Initial end point
                    _draggedNode = null; // Don't drag if starting connection
                }
            }
            else
            {
                _viewModel.SelectedNode = null; // Deselect if tapping empty space
                _draggedNode = null;
                _viewModel.CancelConnection(); // Cancel any pending connection
                _isDrawingConnection = false;
            }
            NodeCanvas.Invalidate(); // Redraw for selection/connection feedback
        }

        void OnCanvasDragInteraction(object sender, TouchEventArgs e)
        {
            PointF currentPoint = e.Touches[0];

            if (_draggedNode != null)
            {
                // Calculate delta and update node position in ViewModel
                float deltaX = currentPoint.X - _dragStartPoint.X;
                float deltaY = currentPoint.Y - _dragStartPoint.Y;
                PointF newPos = new PointF(_draggedNode.Position.X + deltaX, _draggedNode.Position.Y + deltaY);

                _viewModel.UpdateNodePosition(_draggedNode, newPos);

                _dragStartPoint = currentPoint; // Update start point for next delta
                NodeCanvas.Invalidate(); // Request redraw
            }
            else if (_isDrawingConnection)
            {
                _connectionEndPoint = currentPoint;
                _viewModel.UpdatePotentialConnection(currentPoint); // Update VM state if needed
                NodeCanvas.Invalidate(); // Redraw temporary line
            }
        }

        void OnCanvasEndInteraction(object sender, TouchEventArgs e)
        {
            PointF endPoint = e.Touches[0]; // Use the first touch point

            if (_isDrawingConnection)
            {
                var targetNode = _viewModel.GetNodeAtPoint(endPoint);
                _viewModel.CompleteConnection(targetNode); // VM handles connection logic
                _isDrawingConnection = false;
            }

            _draggedNode = null; // Stop dragging
            NodeCanvas.Invalidate(); // Final redraw
        }

        void OnCanvasCancelInteraction(object sender, EventArgs e)
        {
            // Handle cancellation (e.g., touch moved off screen)
            _draggedNode = null;
            if (_isDrawingConnection)
            {
                _viewModel.CancelConnection();
                _isDrawingConnection = false;
                NodeCanvas.Invalidate();
            }
        }
    }
}
