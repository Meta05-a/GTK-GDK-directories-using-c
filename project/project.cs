//ctrl z undo move
using Gtk;
using Gdk;
using System;
using System.Linq;
using System.Collections.Generic;

public enum Player { None, Human, Computer }

public class ConnectFourGame : Gtk.Window
{
    private const int GridSize = 9; // 9x9 grid
    private const int CellSize = 50; // Size of each cell
    private const int WinLength = 4; // Length required to win
    private Player[,] board; // Tracks the state of the grid
    private Player currentPlayer; // Tracks the current player
    private bool gameOver = false;

    private List<(int row, int col, Player player)> moveHistory; // Tracks move history
    private GridWidget gridWidget;

    public ConnectFourGame() : base("Connect Four - 9x9 Grid with Undo")
    {
        // Enable key press events
        AddEvents((int)Gdk.EventMask.KeyPressMask);

        // Initialize the board
        board = new Player[GridSize, GridSize];
        moveHistory = new List<(int row, int col, Player player)>();
        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
                board[i, j] = Player.None;

        // Set up the window
        SetDefaultSize(GridSize * CellSize, GridSize * CellSize + 50);
        BorderWidth = 10;
        DeleteEvent += (sender, args) => Application.Quit();

        // Set up the grid widget
        gridWidget = new GridWidget(GridSize, GridSize, CellSize, new Cairo.Color(1, 1, 1), new Cairo.Color(0, 0, 0));
        Add(gridWidget);

        // Randomly choose the starting player
        currentPlayer = new Random().Next(0, 2) == 0 ? Player.Human : Player.Computer;
        Console.WriteLine($"{currentPlayer} will start the game.");

        // If the computer starts, make the first move
        if (currentPlayer == Player.Computer)
            MakeComputerMove();

        KeyPressEvent += OnKeyPress;

        ShowAll();
    }

    private void OnKeyPress(object sender, KeyPressEventArgs args)
    {
        if (gameOver)
            return;

        if (args.Event.Key == Gdk.Key.Escape)
        {
            Application.Quit();
            return;
        }

        // Undo functionality (CTRL+Z)
        if (args.Event.State.HasFlag(ModifierType.ControlMask) && args.Event.Key == Gdk.Key.z)
        {
            UndoLastMove();
            return;
        }

        // Handle moves if the current player is Human
        if (currentPlayer == Player.Human)
        {
            if (args.Event.Key >= Gdk.Key.Key_1 && args.Event.Key <= Gdk.Key.Key_9)
            {
                int column = (int)args.Event.Key - (int)Gdk.Key.Key_1;
                MakeMove(column, currentPlayer);
            }
            else
            {
                Console.WriteLine("Invalid key. Please press a number between 1 and 9.");
            }
        }
    }

    private void MakeMove(int column, Player player)
    {
        if (gameOver)
            return;

        int row = GetFirstEmptyRow(column);
        if (row == -1)
        {
            Console.WriteLine($"Column {column + 1} is full.");
            return;
        }

        board[row, column] = player;
        gridWidget.DrawCircle(row, column, player == Player.Human ? new Cairo.Color(0, 0, 1) : new Cairo.Color(1, 0, 0));
        moveHistory.Add((row, column, player)); // Save the move to history

        if (CheckWin(row, column, player))
        {
            gameOver = true;
            Console.WriteLine($"{player} wins!");
            Application.Quit(); // Close the application
            return;
        }

        if (IsBoardFull())
        {
            gameOver = true;
            Console.WriteLine("The game is a draw!");
            Application.Quit(); // Close the application
            return;
        }

        currentPlayer = player == Player.Human ? Player.Computer : Player.Human;

        if (currentPlayer == Player.Computer)
            MakeComputerMove();
    }

    private void UndoLastMove()
    {
        if (moveHistory.Count == 0)
        {
            Console.WriteLine("No moves to undo.");
            return;
        }

        // Get the last move
        var lastMove = moveHistory.Last();
        moveHistory.RemoveAt(moveHistory.Count - 1); // Remove it from history

        // Clear the move from the board and grid
        board[lastMove.row, lastMove.col] = Player.None;
        gridWidget.ClearCell(lastMove.row, lastMove.col); // Implement ClearCell in GridWidget

        // Switch the current player back
        currentPlayer = lastMove.player;
        Console.WriteLine("Undo successful.");
    }

    private void MakeComputerMove()
    {
        if (gameOver)
            return;

        GLib.Timeout.Add(500, () =>
        {
            var validColumns = Enumerable.Range(0, GridSize).Where(col => GetFirstEmptyRow(col) != -1).ToList();
            if (!validColumns.Any())
                return false;

            int column = validColumns[new Random().Next(validColumns.Count)];
            MakeMove(column, Player.Computer);
            return false; // Timeout runs only once
        });
    }

    private int GetFirstEmptyRow(int column)
    {
        for (int row = GridSize - 1; row >= 0; row--)
            if (board[row, column] == Player.None)
                return row;

        return -1;
    }

    private bool IsBoardFull()
    {
        for (int col = 0; col < GridSize; col++)
            if (GetFirstEmptyRow(col) != -1)
                return false;

        return true;
    }

    private bool CheckWin(int row, int col, Player player)
    {
        return CheckDirection(row, col, 1, 0, player) + CheckDirection(row, col, -1, 0, player) >= WinLength - 1 || // Horizontal
               CheckDirection(row, col, 0, 1, player) + CheckDirection(row, col, 0, -1, player) >= WinLength - 1 || // Vertical
               CheckDirection(row, col, 1, 1, player) + CheckDirection(row, col, -1, -1, player) >= WinLength - 1 || // Diagonal /
               CheckDirection(row, col, 1, -1, player) + CheckDirection(row, col, -1, 1, player) >= WinLength - 1;  // Diagonal \
    }

    private int CheckDirection(int row, int col, int dRow, int dCol, Player player)
    {
        int count = 0;
        while (true)
        {
            row += dRow;
            col += dCol;

            if (row < 0 || row >= GridSize || col < 0 || col >= GridSize || board[row, col] != player)
                break;

            count++;
        }
        return count;
    }

    public static void Main()
    {
        Application.Init();
        new ConnectFourGame();
        Application.Run();
    }
}

public class GridWidget : DrawingArea
{
    private int rows;
    private int columns;
    private int cellSize;
    private Cairo.Color backgroundColor;
    private Cairo.Color lineColor;
    private Dictionary<(int, int), Cairo.Color> cellCircles;

    public GridWidget(int rows, int columns, int cellSize, Cairo.Color backgroundColor, Cairo.Color lineColor)
    {
        this.rows = rows;
        this.columns = columns;
        this.cellSize = cellSize;
        this.backgroundColor = backgroundColor;
        this.lineColor = lineColor;
        this.cellCircles = new Dictionary<(int, int), Cairo.Color>();

        this.Drawn += OnDraw;
    }

    public void DrawCircle(int row, int col, Cairo.Color color)
    {
        cellCircles[(row, col)] = color;
        QueueDraw();
    }

    public void ClearCell(int row, int col) // Clear a specific cell
    {
        if (cellCircles.ContainsKey((row, col)))
        {
            cellCircles.Remove((row, col));
            QueueDraw();
        }
    }

    private void OnDraw(object sender, DrawnArgs args)
    {
        var cr = args.Cr;

        // Draw background
        cr.SetSourceRGB(backgroundColor.R, backgroundColor.G, backgroundColor.B);
        cr.Paint();

        // Draw grid lines
        cr.SetSourceRGB(lineColor.R, lineColor.G, lineColor.B);

        for (int i = 0; i <= rows; i++)
        {
            cr.MoveTo(0, i * cellSize);
            cr.LineTo(columns * cellSize, i * cellSize);
            cr.Stroke();
        }

        for (int j = 0; j <= columns; j++)
        {
            cr.MoveTo(j * cellSize, 0);
            cr.LineTo(j * cellSize, rows * cellSize);
            cr.Stroke();
        }

        // Draw circles
        foreach (var ((row, column), color) in cellCircles)
        {
            cr.SetSourceRGB(color.R, color.G, color.B);
            double centerX = column * cellSize + cellSize / 2.0;
            double centerY = row * cellSize + cellSize / 2.0;
            double radius = cellSize / 2.5;
            cr.Arc(centerX, centerY, radius, 0, 2 * Math.PI);
            cr.Fill();
        }
    }
}
