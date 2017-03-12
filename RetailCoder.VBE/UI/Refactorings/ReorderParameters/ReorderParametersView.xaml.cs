﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Rubberduck.Refactorings.ReorderParameters;

namespace Rubberduck.UI.Refactorings.ReorderParameters
{
    public partial class ReorderParametersView
    {
        // borrowed the drag/drop from https://fxmax.wordpress.com/2010/10/05/wpf/
        private Point _startPoint;
        private DragAdorner _adorner;
        private AdornerLayer _layer;

        private ReorderParametersViewModel ViewModel => (ReorderParametersViewModel)DataContext;

        public ReorderParametersView()
        {
            InitializeComponent();
        }

        private void ParameterGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(ParameterGrid);
        }

        private void ParameterGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(null);

                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    BeginDrag(e);
                }
            }
        }

        private void BeginDrag(MouseEventArgs e)
        {
            var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);

            if (listViewItem == null) { return; }

            // get the data for the ListViewItem
            var parameter = (Parameter)ParameterGrid.ItemContainerGenerator.ItemFromContainer(listViewItem);

            //setup the drag adorner.
            InitialiseAdorner(listViewItem);

            //add handles to update the adorner.
            ParameterGrid.PreviewDragOver += ParameterGrid_DragOver;
            ParameterGrid.DragLeave += ParameterGrid_DragLeave;
            ParameterGrid.DragEnter += ParameterGrid_DragEnter;

            var data = new DataObject(typeof(Parameter), parameter);
            DragDrop.DoDragDrop(ParameterGrid, data, DragDropEffects.Move);

            //cleanup 
            ParameterGrid.PreviewDragOver -= ParameterGrid_DragOver;
            ParameterGrid.DragLeave -= ParameterGrid_DragLeave;
            ParameterGrid.DragEnter -= ParameterGrid_DragEnter;

            if (_adorner != null)
            {
                AdornerLayer.GetAdornerLayer(ParameterGrid).Remove(_adorner);
                _adorner = null;
            }
        }

        private void ParameterGrid_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(Parameter)) ||
                sender == e.Source)
            {
                e.Effects = DragDropEffects.None;
            }
        }


        private void ParameterGrid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Parameter)))
            {
                var parameter = e.Data.GetData(typeof(Parameter)) as Parameter;
                var listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);

                if (listViewItem != null)
                {
                    var parameterToReplace = (Parameter)ParameterGrid.ItemContainerGenerator.ItemFromContainer(listViewItem);
                    var index = ParameterGrid.Items.IndexOf(parameterToReplace);

                    if (index >= 0)
                    {
                        ViewModel.Parameters.Move(ViewModel.Parameters.IndexOf(parameter), index);
                    }
                }
                else
                {
                    ViewModel.Parameters.Move(ViewModel.Parameters.IndexOf(parameter), ViewModel.Parameters.Count - 1);
                }
            }
        }

        private void InitialiseAdorner(ListViewItem listViewItem)
        {
            var brush = new VisualBrush(listViewItem);
            _adorner = new DragAdorner(listViewItem, listViewItem.RenderSize, brush) {Opacity = 0.5};
            _layer = AdornerLayer.GetAdornerLayer(ParameterGrid);
            _layer.Add(_adorner);
        }

        void ParameterGrid_DragLeave(object sender, DragEventArgs e)
        {
            if (e.OriginalSource == ParameterGrid)
            {
                var p = e.GetPosition(ParameterGrid);
                var r = VisualTreeHelper.GetContentBounds(ParameterGrid);
                if (!r.Contains(p))
                {
                    e.Handled = true;
                }
            }
        }

        void ParameterGrid_DragOver(object sender, DragEventArgs args)
        {
            if (_adorner == null) { return; }

            _adorner.OffsetLeft = args.GetPosition(ParameterGrid).X;
            _adorner.OffsetTop = args.GetPosition(ParameterGrid).Y - _startPoint.Y;
        }

        // Helper to search up the VisualTree
        private static T FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }
}
