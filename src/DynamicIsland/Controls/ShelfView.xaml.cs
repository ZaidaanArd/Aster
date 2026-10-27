using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Services;

namespace DynamicIsland.Controls;

public readonly record struct ShelfAddResult(int Added, int Rejected, bool Full);

public partial class ShelfView : UserControl
{
    private const int MaxItems = 8;
    private const double CardWidth = 106;
    private const double CardHeight = 65;

    private static readonly FontFamily UiFont = new("Segoe UI Variable Display, Segoe UI");
    private static readonly Brush MissingBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0x6B, 0x63));
    private static readonly Brush MutedBrush = CreateFrozenBrush(Color.FromRgb(0x5E, 0x5E, 0x66));

    private readonly List<ShelfItem> _items = new();
    private readonly DispatcherTimer _existenceTimer;

    private Point _dragStart;
    private ShelfItem? _dragItem;
    private FrameworkElement? _dragSource;

    public event Action? ItemsChanged;
    public event Action<bool>? DragOutChanged;

    public int Count => _items.Count;

    public ShelfView()
    {
        InitializeComponent();

        _existenceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _existenceTimer.Tick += (_, _) => RefreshExistence();

        IsVisibleChanged += OnVisibleChanged;
        Unloaded += OnUnloaded;

        Render();
    }

    public ShelfAddResult AddPaths(IReadOnlyList<string> paths)
    {
        var added = 0;
        var rejected = 0;
        var full = false;

        foreach (var raw in paths)
        {
            if (_items.Count >= MaxItems)
            {
                full = true;
                rejected++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                rejected++;
                continue;
            }

            string path;
            try
            {
                path = Path.GetFullPath(raw);
            }
            catch
            {
                rejected++;
                continue;
            }

            if (_items.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                rejected++;
                continue;
            }

            var isDirectory = Directory.Exists(path);
            var exists = isDirectory || File.Exists(path);
            var name = GetName(path);

            _items.Add(new ShelfItem(path, name, isDirectory, exists));
            added++;
        }

        if (added > 0)
        {
            Render();
            ItemsChanged?.Invoke();
        }

        return new ShelfAddResult(added, rejected, full);
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            RefreshExistence();
            _existenceTimer.Start();
        }
        else
        {
            _existenceTimer.Stop();
            _dragItem = null;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _existenceTimer.Stop();
        _dragItem = null;
    }

    private void RefreshExistence()
    {
        var changed = false;

        foreach (var item in _items)
        {
            var isDirectory = Directory.Exists(item.Path);
            var exists = isDirectory || File.Exists(item.Path);
            if (item.Exists != exists)
            {
                item.Exists = exists;
                changed = true;
            }
        }

        if (changed)
        {
            Render();
            ItemsChanged?.Invoke();
        }
    }

    private void Render()
    {
        ShelfGrid.Children.Clear();

        foreach (var item in _items)
        {
            ShelfGrid.Children.Add(CreateCard(item));
        }

        var hasItems = _items.Count > 0;
        ShelfGrid.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        ShelfCount.Text = $"{_items.Count}/{MaxItems}";
        ClearButton.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border CreateCard(ShelfItem item)
    {
        var badgeBackground = CreateFrozenBrush(Color.FromArgb(
            0x26,
            item.BadgeColor.R,
            item.BadgeColor.G,
            item.BadgeColor.B));
        var badgeForeground = CreateFrozenBrush(item.BadgeColor);

        var badge = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Background = badgeBackground,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = item.BadgeText,
                FontFamily = UiFont,
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = badgeForeground,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var name = new TextBlock
        {
            Text = item.Name,
            FontFamily = UiFont,
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            LineHeight = 12,
            MaxHeight = 26,
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var size = new TextBlock
        {
            Text = GetSizeLabel(item),
            FontFamily = UiFont,
            FontSize = 8,
            Foreground = item.Exists ? MutedBrush : MissingBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 1, 0, 0)
        };

        var content = new Grid { Margin = new Thickness(7) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRowSpan(badge, 2);
        Grid.SetColumn(name, 2);
        Grid.SetColumn(size, 2);
        Grid.SetRow(size, 1);

        content.Children.Add(badge);
        content.Children.Add(name);
        content.Children.Add(size);

        var remove = new Button
        {
            Style = (Style)FindResource("GlyphButton"),
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Remove from shelf",
            Content = new TextBlock
            {
                Text = "\u00D7",
                FontFamily = UiFont,
                FontSize = 11,
                Margin = new Thickness(0, -1, 0, 0)
            }
        };
        remove.Click += (_, e) =>
        {
            Remove(item);
            e.Handled = true;
        };

        var root = new Grid();
        root.Children.Add(content);
        root.Children.Add(remove);

        var card = new Border
        {
            Style = (Style)FindResource("ShelfCard"),
            Width = CardWidth,
            Height = CardHeight,
            Margin = new Thickness(3),
            Tag = item,
            ToolTip = item.Path,
            Cursor = item.Exists ? Cursors.Hand : Cursors.Arrow,
            Opacity = item.Exists ? 1 : 0.55,
            Child = root
        };

        card.PreviewMouseLeftButtonDown += OnCardMouseDown;
        card.PreviewMouseMove += OnCardMouseMove;
        card.MouseLeftButtonDown += OnCardMouseLeftButtonDown;

        return card;
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ShelfItem item } card)
        {
            return;
        }

        if (!item.Exists || IsInsideButton(e.OriginalSource as DependencyObject, card))
        {
            return;
        }

        _dragStart = e.GetPosition(this);
        _dragItem = item;
        _dragSource = card;
    }

    private void OnCardMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            _dragItem is null ||
            _dragSource is null ||
            !ReferenceEquals(sender, _dragSource))
        {
            return;
        }

        if (sender is not FrameworkElement card ||
            IsInsideButton(e.OriginalSource as DependencyObject, card))
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _dragItem;
        _dragItem = null;

        if (!item.Exists)
        {
            return;
        }

        var data = new DataObject(DataFormats.FileDrop, new[] { item.Path });

        DragOutChanged?.Invoke(true);
        try
        {
            DragDrop.DoDragDrop(card, data, DragDropEffects.Copy);
        }
        finally
        {
            DragOutChanged?.Invoke(false);
        }

        e.Handled = true;
    }

    private void OnCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: ShelfItem item } card)
        {
            return;
        }

        if (!item.Exists || IsInsideButton(e.OriginalSource as DependencyObject, card))
        {
            return;
        }

        _dragItem = null;

        try
        {
            Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugLog.Write($"shelf open failed: {ex}");
        }

        e.Handled = true;
    }

    private static bool IsInsideButton(DependencyObject? source, DependencyObject card)
    {
        while (source is not null && !ReferenceEquals(source, card))
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void Remove(ShelfItem item)
    {
        if (!_items.Remove(item))
        {
            return;
        }

        Render();
        ItemsChanged?.Invoke();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            return;
        }

        _items.Clear();
        Render();
        ItemsChanged?.Invoke();
    }

    private static string GetName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string GetSizeLabel(ShelfItem item)
    {
        if (item.IsDirectory)
        {
            return "Folder";
        }

        if (!item.Exists)
        {
            return "Missing";
        }

        try
        {
            var length = new FileInfo(item.Path).Length;
            if (length < 1024)
            {
                return $"{length} B";
            }

            if (length < 1024 * 1024)
            {
                return $"{length / 1024.0:0} KB";
            }

            if (length < 1024L * 1024 * 1024)
            {
                return $"{length / (1024.0 * 1024):0.0} MB";
            }

            return $"{length / (1024.0 * 1024 * 1024):0.0} GB";
        }
        catch
        {
            return "\u2014";
        }
    }

    private static (string Text, Color Color) GetBadge(string path, bool isDirectory)
    {
        if (isDirectory)
        {
            return ("DIR", Color.FromRgb(0xFF, 0xB8, 0x6B));
        }

        var extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ("FILE", Color.FromRgb(0x94, 0x94, 0x9C));
        }

        var badge = extension.Length > 4 ? extension[..4] : extension;
        var color = extension switch
        {
            "PNG" or "JPG" or "JPEG" or "GIF" or "WEBP" or "BMP" or "SVG" or "HEIC" =>
                Color.FromRgb(0x7F, 0xB8, 0xFF),
            "PDF" =>
                Color.FromRgb(0xFF, 0x7A, 0x7A),
            "DOC" or "DOCX" or "TXT" or "MD" or "RTF" or "ODT" =>
                Color.FromRgb(0xE6, 0xE6, 0xEB),
            "XLS" or "XLSX" or "CSV" or "ODS" =>
                Color.FromRgb(0x6F, 0xE3, 0xA5),
            "PPT" or "PPTX" or "ODP" =>
                Color.FromRgb(0xFF, 0xB8, 0x6B),
            "ZIP" or "RAR" or "7Z" or "TAR" or "GZ" =>
                Color.FromRgb(0xFF, 0xD3, 0x6B),
            "MP3" or "WAV" or "FLAC" or "AAC" or "M4A" =>
                Color.FromRgb(0xC0, 0x8C, 0xFF),
            "MP4" or "MOV" or "MKV" or "AVI" or "WEBM" =>
                Color.FromRgb(0xFF, 0x8A, 0xB8),
            _ => Color.FromRgb(0x94, 0x94, 0x9C)
        };

        return (badge, color);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed class ShelfItem
    {
        public ShelfItem(string path, string name, bool isDirectory, bool exists)
        {
            Path = path;
            Name = name;
            IsDirectory = isDirectory;
            Exists = exists;
            (BadgeText, BadgeColor) = GetBadge(path, isDirectory);
        }

        public string Path { get; }
        public string Name { get; }
        public bool IsDirectory { get; }
        public bool Exists { get; set; }
        public string BadgeText { get; }
        public Color BadgeColor { get; }
    }
}
