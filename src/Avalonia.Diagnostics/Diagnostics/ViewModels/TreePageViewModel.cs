using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class TreePageViewModel : ViewModelBase, IDisposable
    {
        private ControlDetailsViewModel? _details;
        private readonly ISet<string> _pinnedProperties;

        public TreePageViewModel(MainViewModel mainView, TreeNode[] nodes, ISet<string> pinnedProperties)
        {
            MainView = mainView;
            Nodes = nodes;
            NodesSource = new HierarchicalTreeDataGridSource<TreeNode>(nodes)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<TreeNode>(
                        new TemplateColumn<TreeNode>("Element", "TreeNodeDataTemplate"),
                        x => x.Children, null, x => x.IsExpanded)
                },
            };
            NodesSource.Selection = Selection = new TreeDataGridRowSelectionModel<TreeNode>(NodesSource);
            _pinnedProperties = pinnedProperties;
            PropertiesFilter = new FilterViewModel();
            PropertiesFilter.RefreshFilter += (s, e) => Details?.PropertiesView?.Refresh();

            SettersFilter = new FilterViewModel();
            SettersFilter.RefreshFilter += (s, e) => Details?.UpdateStyleFilters();

            Selection.SelectionChanged += (_, _) =>
            {
                Details = SelectedNode == null ? null :
                    new ControlDetailsViewModel(this, SelectedNode.Visual, _pinnedProperties);
                Details?.UpdatePropertiesView(MainView.ShowImplementedInterfaces);
                Details?.UpdateStyleFilters();
                RaisePropertyChanged(nameof(SelectedNode));
            };
        }

        public event EventHandler<string>? ClipboardCopyRequested;
        
        public MainViewModel MainView { get; }

        public FilterViewModel PropertiesFilter { get; }

        public FilterViewModel SettersFilter { get; }

        public TreeNode[] Nodes { get; protected set; }
        
        public HierarchicalTreeDataGridSource<TreeNode> NodesSource { get; }

        public ITreeDataGridRowSelectionModel<TreeNode> Selection { get; }
        
        public TreeNode? SelectedNode
        {
            get => Selection.SelectedItem;
            set
            {
                if (value == SelectedNode)
                    return;
                
                if (value == null)
                {
                    Selection.Clear();
                    Details = null;
                }
                else
                {
                    Selection.Select(GetIndexPath(value));
                }
            }
        }

        public ControlDetailsViewModel? Details
        {
            get => _details;
            private set
            {
                var oldValue = _details;

                if (RaiseAndSetIfChanged(ref _details, value))
                {
                    oldValue?.Dispose();
                }
            }
        }

        public void Dispose()
        {
            foreach (var node in Nodes)
            {
                node.Dispose();
            }

            _details?.Dispose();
        }

        public TreeNode? FindNode(Control control)
        {
            foreach (var node in Nodes)
            {
                var result = FindNode(node, control);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public void SelectControl(Control control)
        {
            var node = default(TreeNode);
            Control? c = control;

            while (node == null && c != null)
            {
                node = FindNode(c);

                if (node == null)
                {
                    c = c.GetVisualParent<Control>();
                }
            }

            if (node != null)
            {
                ExpandNode(node.Parent);
                SelectedNode = node;
            }
        }

        public void CopySelector()
        {
            var currentVisual = SelectedNode?.Visual as Visual;
            if (currentVisual is not null)
            {
                var selector = GetVisualSelector(currentVisual);
                
                ClipboardCopyRequested?.Invoke(this, selector);
            }
        }
        
        public void CopySelectorFromTemplateParent()
        {
            var parts = new List<string>();

            var currentVisual = SelectedNode?.Visual as Visual;
            while (currentVisual is not null)
            {
                parts.Add(GetVisualSelector(currentVisual));
                
                currentVisual = currentVisual.TemplatedParent as Visual;
            }

            if (parts.Any())
            {
                parts.Reverse();
                var selector = string.Join(" /template/ ", parts);

                ClipboardCopyRequested?.Invoke(this, selector);
            }
        }

        public void ExpandRecursively()
        {
            if (SelectedNode is { } selectedNode)
            {
                ExpandNode(selectedNode);
                
                var stack = new Stack<TreeNode>();
                stack.Push(selectedNode);

                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    item.IsExpanded = true;
                    foreach (var child in item.Children)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        public void CollapseChildren()
        {
            if (SelectedNode is { } selectedNode)
            {
                var stack = new Stack<TreeNode>();
                stack.Push(selectedNode);

                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    item.IsExpanded = false;
                    foreach (var child in item.Children)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        public void CaptureNodeScreenshot()
        {
            MainView.Shot(null);
        }

        public void BringIntoView()
        {
            (SelectedNode?.Visual as Control)?.BringIntoView();
        }
        
        
        public void Focus()
        {
            (SelectedNode?.Visual as Control)?.Focus();
        }

        private static string GetVisualSelector(Visual visual)
        {
            var name = string.IsNullOrEmpty(visual.Name) ? "" : $"#{visual.Name}";
            var classes = string.Concat(visual.Classes
                .Where(c => !c.StartsWith(":"))
                .Select(c => '.' + c));
            var typeName = StyledElement.GetStyleKey(visual);

            return $"{typeName}{name}{classes}";
        } 

        private void ExpandNode(TreeNode? node)
        {
            if (node != null)
            {
                node.IsExpanded = true;
                ExpandNode(node.Parent);
            }
        }

        private TreeNode? FindNode(TreeNode node, Control control)
        {
            if (node.Visual == control)
            {
                return node;
            }
            else
            {
                foreach (var child in node.Children)
                {
                    var result = FindNode(child, control);

                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        private IndexPath GetIndexPath(TreeNode node)
        {
            var pathToRoot = new List<int>();
            while (node.Parent != null)
            {
                var parent = node.Parent;
                var indexOfChild = ((IList)parent.Children).IndexOf(node);
                pathToRoot.Add(indexOfChild);
                node = parent;
            }
            pathToRoot.Add(0);
            pathToRoot.Reverse();
            return new IndexPath(pathToRoot);
        }

        internal void UpdatePropertiesView()
        {
            Details?.UpdatePropertiesView(MainView?.ShowImplementedInterfaces ?? true);
        }
    }
}
