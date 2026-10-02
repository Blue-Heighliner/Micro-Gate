namespace BlueHeighliner.MicroGate;

/// <summary>
/// A table of bytes, <see cref="Columns"/> cells wide (30 by default), with every cell's 0-based index above it. Cells show their ASCII character by default and can be switched, with multi-select, to their 0-255 value. When editable, typing fills the selected cell and moves to the next, and the context menu inserts or replaces control characters and deletes cells.
/// </summary>
internal sealed class ByteGrid : Control
{
    /// <summary>
    /// Identifies the <see cref="Cells"/> property.
    /// </summary>
    public static readonly StyledProperty<List<ByteCell>?> CellsProperty = AvaloniaProperty.Register<ByteGrid, List<ByteCell>?>(nameof(Cells));

    /// <summary>
    /// Identifies the <see cref="IsEditable"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsEditableProperty = AvaloniaProperty.Register<ByteGrid, bool>(nameof(IsEditable));

    /// <summary>
    /// Identifies the <see cref="RawInput"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> RawInputProperty = AvaloniaProperty.Register<ByteGrid, bool>(nameof(RawInput));

    /// <summary>
    /// Identifies the <see cref="MaxCells"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxCellsProperty = AvaloniaProperty.Register<ByteGrid, int>(nameof(MaxCells), int.MaxValue);

    /// <summary>
    /// Identifies the <see cref="Columns"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<ByteGrid, int>(nameof(Columns), 30, validate: value => value >= 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteGrid"/> class.
    /// </summary>
    public ByteGrid()
    {
        Focusable = true;
        typeface = new Typeface(new FontFamily("Cascadia Code,Consolas,Menlo,monospace"));
    }

    private readonly double cellWidth = 28;
    private readonly double cellHeight = 22;
    private readonly double indexHeight = 14;
    private readonly Typeface typeface;
    private readonly ControlCharacters characters = new();
    private readonly SortedSet<int> selection = [];
    private readonly IBrush cellBrush = new SolidColorBrush(Color.Parse("#333337"));
    private readonly IBrush selectedBrush = new SolidColorBrush(Color.Parse("#1177BB"));
    private readonly IBrush textBrush = new SolidColorBrush(Color.Parse("#CCCCCC"));
    private readonly IBrush valueBrush = new SolidColorBrush(Color.Parse("#B5CEA8"));
    private readonly IBrush controlBrush = new SolidColorBrush(Color.Parse("#569CD6"));
    private readonly IBrush headerBrush = new SolidColorBrush(Color.Parse("#9D9D9D"));
    private readonly Pen caretPen = new(new SolidColorBrush(Color.Parse("#007ACC")), 1.5);
    private string editBuffer = string.Empty;
    private int anchor;
    private int caret;
    private bool dragging;

    /// <summary>
    /// Occurs when the user presses Enter in an editable grid.
    /// </summary>
    public event EventHandler? SubmitRequested;

    /// <summary>
    /// Occurs after the user changes the cells, by typing, inserting, replacing, or deleting.
    /// </summary>
    public event EventHandler? Edited;

    /// <summary>
    /// Gets or sets the cells shown. An editable grid changes this list in place.
    /// </summary>
    public List<ByteCell>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    /// <summary>
    /// Gets or sets how many cells wide the table is, at least 1.
    /// </summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can edit the cells.
    /// </summary>
    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether typing enters 0-255 values (Tab moves to the next cell) instead of ASCII characters (each character moves to the next cell).
    /// </summary>
    public bool RawInput
    {
        get => GetValue(RawInputProperty);
        set => SetValue(RawInputProperty, value);
    }

    /// <summary>
    /// Gets or sets the most cells an editable grid may hold.
    /// </summary>
    public int MaxCells
    {
        get => GetValue(MaxCellsProperty);
        set => SetValue(MaxCellsProperty, value);
    }

    private int Count => Cells?.Count ?? 0;

    private int SlotCount => Count + (IsEditable && Count < MaxCells ? 1 : 0);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

        for (int slot = 0; slot < SlotCount; slot++)
        {
            Rect cell = CellRect(slot);
            DrawText(context, slot.ToString(CultureInfo.InvariantCulture), headerBrush, new Rect(cell.X, cell.Y - indexHeight, cell.Width, indexHeight), 9);
            Rect rect = cell.Deflate(1);
            context.DrawRectangle(selection.Contains(slot) ? selectedBrush : cellBrush, null, rect);
            if (slot < Count)
            {
                (string text, IBrush brush) = Describe(Cells![slot]);
                DrawText(context, text, brush, rect, 11);
            }
        }

        if (IsEditable && IsFocused && caret < SlotCount)
        {
            context.DrawRectangle(null, caretPen, CellRect(caret).Deflate(1));
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Columns * cellWidth, RowCount() * (indexHeight + cellHeight));

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CellsProperty)
        {
            selection.Clear();
            editBuffer = string.Empty;
            anchor = 0;
            caret = 0;
        }

        if (change.Property == CellsProperty || change.Property == IsEditableProperty || change.Property == MaxCellsProperty || change.Property == ColumnsProperty)
        {
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (IsEditable && selection.Count == 0)
        {
            SelectOnly(Math.Min(caret, Math.Max(0, SlotCount - 1)));
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        Focus();

        PointerPoint point = e.GetCurrentPoint(this);
        int index = IndexAt(point.Position);
        if (index < 0)
        {
            return;
        }

        editBuffer = string.Empty;
        if (point.Properties.IsRightButtonPressed)
        {
            if (!selection.Contains(index))
            {
                SelectOnly(index);
            }

            ContextMenu = BuildMenu();
        }
        else if (point.Properties.IsLeftButtonPressed)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (!selection.Remove(index))
                {
                    selection.Add(index);
                    anchor = index;
                }

                caret = index;
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                SelectRange(anchor, index);
                caret = index;
            }
            else
            {
                SelectOnly(index);
            }

            dragging = true;
            e.Pointer.Capture(this);
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!dragging)
        {
            return;
        }

        int index = IndexAt(e.GetPosition(this));
        if (index >= 0)
        {
            SelectRange(anchor, index);
            caret = index;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        dragging = false;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        switch (e.Key)
        {
            case Key.Left:
                Move(caret - 1, shift);
                e.Handled = true;
                break;
            case Key.Right:
                Move(caret + 1, shift);
                e.Handled = true;
                break;
            case Key.Up:
                Move(caret - Columns, shift);
                e.Handled = true;
                break;
            case Key.Down:
                Move(caret + Columns, shift);
                e.Handled = true;
                break;
            case Key.Tab when IsEditable:
                int target = shift ? caret - 1 : caret + 1;
                if (target >= 0 && target < SlotCount)
                {
                    Move(target, false);
                    e.Handled = true;
                }

                break;
            case Key.Enter when IsEditable:
                SubmitRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case Key.Delete when IsEditable:
                DeleteSelected();
                e.Handled = true;
                break;
            case Key.Back when IsEditable:
                Backspace();
                e.Handled = true;
                break;
            case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                SelectRange(0, Count - 1);
                InvalidateVisual();
                e.Handled = true;
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!IsEditable || e.Text is null)
        {
            return;
        }

        foreach (char character in e.Text)
        {
            if (RawInput && char.IsAsciiDigit(character))
            {
                TypeDigit(character);
            }
            else if (!RawInput && character is >= ' ' and <= '~')
            {
                Put((byte)character, false);
                Move(caret + 1, false);
            }
        }

        e.Handled = true;
    }

    private int RowCount() => Math.Max(1, (SlotCount + Columns - 1) / Columns);

    private Rect CellRect(int index) => new(index % Columns * cellWidth, (index / Columns * (indexHeight + cellHeight)) + indexHeight, cellWidth, cellHeight);

    private int IndexAt(Point point)
    {
        double x = point.X;
        double y = point.Y;
        if (x < 0 || y < 0 || (int)(x / cellWidth) >= Columns)
        {
            return -1;
        }

        int index = ((int)(y / (indexHeight + cellHeight)) * Columns) + (int)(x / cellWidth);
        return index < SlotCount ? index : -1;
    }

    private (string Text, IBrush Brush) Describe(ByteCell cell)
    {
        if (cell.ShowRaw || cell.Value > 127)
        {
            return (cell.Value.ToString(CultureInfo.InvariantCulture), valueBrush);
        }

        if (cell.Value is >= 0x20 and < 0x7F)
        {
            return (((char)cell.Value).ToString(), textBrush);
        }

        return (characters.Find(cell.Value)?.Abbreviation ?? ".", controlBrush);
    }

    private void DrawText(DrawingContext context, string text, IBrush brush, Rect area, double size)
    {
        FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);
        context.DrawText(formatted, new Point(area.X + ((area.Width - formatted.Width) / 2), area.Y + ((area.Height - formatted.Height) / 2)));
    }

    private void SelectOnly(int index)
    {
        selection.Clear();
        selection.Add(index);
        anchor = index;
        caret = index;
    }

    private void SelectRange(int from, int to)
    {
        selection.Clear();
        for (int index = Math.Min(from, to); index <= Math.Max(from, to); index++)
        {
            selection.Add(index);
        }
    }

    private void Move(int target, bool extend)
    {
        editBuffer = string.Empty;
        if (SlotCount == 0)
        {
            return;
        }

        target = Math.Clamp(target, 0, SlotCount - 1);
        if (extend)
        {
            SelectRange(anchor, target);
            caret = target;
        }
        else
        {
            SelectOnly(target);
        }

        RaiseEvent(new RequestBringIntoViewEventArgs { RoutedEvent = RequestBringIntoViewEvent, TargetObject = this, TargetRect = CellRect(target) });
        InvalidateVisual();
    }

    private void Put(byte value, bool showRaw)
    {
        if (Cells is not { } cells)
        {
            return;
        }

        if (caret >= cells.Count)
        {
            if (cells.Count >= MaxCells)
            {
                return;
            }

            caret = cells.Count;
            cells.Add(new ByteCell { Value = value, ShowRaw = showRaw });
        }
        else
        {
            cells[caret].Value = value;
            cells[caret].ShowRaw = showRaw;
        }

        OnEdited();
    }

    private void TypeDigit(char digit)
    {
        string tentative = editBuffer + digit;
        if (int.Parse(tentative, CultureInfo.InvariantCulture) > 255)
        {
            Move(caret + 1, false);
            tentative = digit.ToString();
        }

        editBuffer = tentative;
        byte value = byte.Parse(editBuffer, CultureInfo.InvariantCulture);
        Put(value, true);
        if (editBuffer.Length == 3 || value * 10 > 255 || value == 0)
        {
            Move(caret + 1, false);
        }
    }

    private void Backspace()
    {
        if (Cells is not { } cells)
        {
            return;
        }

        int index = editBuffer.Length > 0 ? caret : caret - 1;
        if (index < 0 || index >= cells.Count)
        {
            return;
        }

        cells.RemoveAt(index);
        Move(index, false);
        OnEdited();
    }

    private void DeleteSelected()
    {
        if (Cells is not { } cells || selection.Count == 0)
        {
            return;
        }

        int first = selection.Min;
        foreach (int index in selection.Reverse().Where(index => index < cells.Count))
        {
            cells.RemoveAt(index);
        }

        Move(first, false);
        OnEdited();
    }

    private void InsertControlCharacter(byte value)
    {
        if (Cells is not { } cells)
        {
            return;
        }

        int first = selection.Count > 0 ? selection.Min : caret;
        foreach (int index in selection.Reverse())
        {
            if (cells.Count >= MaxCells)
            {
                break;
            }

            cells.Insert(Math.Min(index, cells.Count), new ByteCell { Value = value });
        }

        Move(first, false);
        OnEdited();
    }

    private void ReplaceWithControlCharacter(byte value)
    {
        if (Cells is not { } cells)
        {
            return;
        }

        foreach (int index in selection.ToList())
        {
            if (index < cells.Count)
            {
                cells[index].Value = value;
                cells[index].ShowRaw = false;
            }
            else if (cells.Count < MaxCells)
            {
                cells.Add(new ByteCell { Value = value });
            }
        }

        OnEdited();
    }

    private void SetRaw(bool raw)
    {
        foreach (int index in selection.Where(index => index < Count))
        {
            Cells![index].ShowRaw = raw;
        }

        InvalidateVisual();
    }

    private void OnEdited()
    {
        InvalidateMeasure();
        InvalidateVisual();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private ContextMenu BuildMenu()
    {
        List<Control> items =
        [
            Item("Show as ASCII", () => SetRaw(false)),
            Item("Show as value", () => SetRaw(true)),
        ];

        if (IsEditable)
        {
            items.Add(new Separator());
            items.Add(new MenuItem { Header = "Insert", ItemsSource = characters.All.Select(character => Item(Describe(character), () => InsertControlCharacter(character.Value))).ToList() });
            items.Add(new MenuItem { Header = "Replace with", ItemsSource = characters.All.Select(character => Item(Describe(character), () => ReplaceWithControlCharacter(character.Value))).ToList() });
            items.Add(new Separator());
            items.Add(Item("Delete", DeleteSelected));
        }

        return new ContextMenu { ItemsSource = items };
    }

    private string Describe(ControlCharacter character) => $"{character.Value,3}  {character.Abbreviation,-3}  {character.Name}";

    private MenuItem Item(string header, Action action)
    {
        MenuItem item = new() { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
