using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace cli_life
{
    public class Settings
    {
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 20;
        public int CellSize { get; set; } = 1;
        public double LiveDensity { get; set; } = 0.5;
    }

    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        private bool IsAliveNext;

        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Count(x => x.IsAlive);
            IsAliveNext = IsAlive ? liveNeighbors == 2 || liveNeighbors == 3 : liveNeighbors == 3;
        }

        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }

    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;

        public int Columns => Cells.GetLength(0);
        public int Rows => Cells.GetLength(1);
        public int Width => Columns * CellSize;
        public int Height => Rows * CellSize;

        public Board(int width, int height, int cellSize, double liveDensity = .1)
        {
            CellSize = cellSize;
            Cells = new Cell[width / cellSize, height / cellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();
            ConnectNeighbors();
            Randomize(liveDensity);
        }

        readonly Random rand = new Random();

        public void Randomize(double liveDensity)
        {
            foreach (var cell in Cells)
                cell.IsAlive = rand.NextDouble() < liveDensity;
        }

        public void Advance()
        {
            foreach (var cell in Cells)
                cell.DetermineNextLiveState();
            foreach (var cell in Cells)
                cell.Advance();
        }

        public int CountAlive() => Cells.Cast<Cell>().Count(c => c.IsAlive);

        public void Save(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Columns} {Rows}");
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                    sb.Append(Cells[x, y].IsAlive ? '*' : ' ');
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static Board Load(string path, int cellSize = 1)
        {
            string[] lines = File.ReadAllLines(path);
            string[] dims = lines[0].Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int cols = int.Parse(dims[0]);
            int rows = int.Parse(dims[1]);
            var board = new Board(cols * cellSize, rows * cellSize, cellSize, 0);

            bool isCsv = lines.Length > 1 && lines[1].Contains(',');

            for (int y = 0; y < rows && y + 1 < lines.Length; y++)
            {
                string line = lines[y + 1];
                if (isCsv)
                {
                    string[] parts = line.Split(',');
                    for (int x = 0; x < cols && x < parts.Length; x++)
                        board.Cells[x, y].IsAlive = parts[x].Trim() == "1";
                }
                else
                {
                    for (int x = 0; x < cols && x < line.Length; x++)
                        board.Cells[x, y].IsAlive = line[x] == '*';
                }
            }
            return board;
        }

        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = x > 0 ? x - 1 : Columns - 1;
                    int xR = x < Columns - 1 ? x + 1 : 0;
                    int yT = y > 0 ? y - 1 : Rows - 1;
                    int yB = y < Rows - 1 ? y + 1 : 0;

                    Cells[x, y].neighbors.Add(Cells[xL, yT]);
                    Cells[x, y].neighbors.Add(Cells[x, yT]);
                    Cells[x, y].neighbors.Add(Cells[xR, yT]);
                    Cells[x, y].neighbors.Add(Cells[xL, y]);
                    Cells[x, y].neighbors.Add(Cells[xR, y]);
                    Cells[x, y].neighbors.Add(Cells[xL, yB]);
                    Cells[x, y].neighbors.Add(Cells[x, yB]);
                    Cells[x, y].neighbors.Add(Cells[xR, yB]);
                }
            }
        }
    }

    public static class FigureClassifier
    {
        private static readonly Dictionary<string, List<(int x, int y)>> KnownFigures = new()
        {
            ["Block"] = new() { (0, 0), (1, 0), (0, 1), (1, 1) },
            ["Beehive"] = new() { (1, 0), (2, 0), (0, 1), (3, 1), (1, 2), (2, 2) },
            ["Loaf"] = new() { (1, 0), (2, 0), (0, 1), (3, 1), (1, 2), (3, 2), (2, 3) },
            ["Boat"] = new() { (0, 0), (1, 0), (0, 1), (2, 1), (1, 2) },
            ["Tub"] = new() { (1, 0), (0, 1), (2, 1), (1, 2) },
            ["Blinker"] = new() { (0, 0), (1, 0), (2, 0) },
        };

        private static List<(int x, int y)> Normalize(IEnumerable<(int x, int y)> cells)
        {
            var list = cells.ToList();
            int minX = list.Min(c => c.x);
            int minY = list.Min(c => c.y);
            return list.Select(c => (c.x - minX, c.y - minY))
                       .OrderBy(c => c.Item1).ThenBy(c => c.Item2)
                       .ToList();
        }

        private static IEnumerable<List<(int x, int y)>> Transformations(List<(int x, int y)> cells)
        {
            Func<(int x, int y), (int x, int y)>[] transforms =
            {
                c => ( c.x,  c.y),
                c => (-c.y,  c.x),
                c => (-c.x, -c.y),
                c => ( c.y, -c.x),
                c => (-c.x,  c.y),
                c => ( c.y,  c.x),
                c => ( c.x, -c.y),
                c => (-c.y, -c.x),
            };
            foreach (var t in transforms)
                yield return Normalize(cells.Select(c => t(c)));
        }

        public static string Classify(List<(int x, int y)> cells)
        {
            foreach (var transformed in Transformations(cells))
            {
                foreach (var (name, pattern) in KnownFigures)
                {
                    var norm = Normalize(pattern);
                    if (transformed.Count == norm.Count && transformed.SequenceEqual(norm))
                        return name;
                }
            }
            return "Unknown";
        }
    }

    public static class BoardAnalyzer
    {
        public static List<List<(int x, int y)>> FindComponents(Board board)
        {
            var visited = new bool[board.Columns, board.Rows];
            var components = new List<List<(int x, int y)>>();

            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    if (!board.Cells[x, y].IsAlive || visited[x, y]) continue;

                    var component = new List<(int x, int y)>();
                    var queue = new Queue<(int, int)>();
                    queue.Enqueue((x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        component.Add((cx, cy));
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = (cx + dx + board.Columns) % board.Columns;
                                int ny = (cy + dy + board.Rows) % board.Rows;
                                if (board.Cells[nx, ny].IsAlive && !visited[nx, ny])
                                {
                                    visited[nx, ny] = true;
                                    queue.Enqueue((nx, ny));
                                }
                            }
                    }
                    components.Add(component);
                }
            }
            return components;
        }

        public static Dictionary<string, int> ClassifyAll(Board board)
        {
            var counts = new Dictionary<string, int>();
            foreach (var comp in FindComponents(board))
            {
                string name = FigureClassifier.Classify(comp);
                counts[name] = counts.GetValueOrDefault(name) + 1;
            }
            return counts;
        }

        public static int RunUntilStable(Board board, int stableWindow = 10, int maxGenerations = 500)
        {
            int stableCount = 0;
            int prevAlive = board.CountAlive();
            for (int gen = 1; gen <= maxGenerations; gen++)
            {
                board.Advance();
                int alive = board.CountAlive();
                if (alive == prevAlive)
                {
                    stableCount++;
                    if (stableCount >= stableWindow)
                        return gen;
                }
                else
                {
                    stableCount = 0;
                    prevAlive = alive;
                }
            }
            return maxGenerations;
        }
    }

    class Program
    {
        static Board board;
        static Settings settings;

        static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "..", "..", "..")); // подъём из bin\Debug\net8.0 до корня проекта

        static string DataDir => Path.Combine(ProjectRoot, "Data");

        static Settings LoadSettings()
        {
            string path = Path.Combine(ProjectRoot, "settings.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
            var s = new Settings();
            File.WriteAllText(path, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            return s;
        }

        static void Reset()
        {
            board = new Board(settings.Width, settings.Height, settings.CellSize, settings.LiveDensity);
        }

        static void Render()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                    Console.Write(board.Cells[col, row].IsAlive ? '*' : ' ');
                Console.WriteLine();
            }
        }

        static void PrintAnalysis()
        {
            var counts = BoardAnalyzer.ClassifyAll(board);
            Console.WriteLine($"Живых клеток: {board.CountAlive()}");
            Console.WriteLine($"Компонент всего: {counts.Values.Sum()}");
            foreach (var (name, count) in counts.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"  {name}: {count}");
        }

        static void CreatePresetFiles()
        {
            Directory.CreateDirectory(DataDir);
            WritePreset(Path.Combine(DataDir, "block.txt"), 20, 10, new[] { (9, 4), (10, 4), (9, 5), (10, 5) });
            WritePreset(Path.Combine(DataDir, "blinker.txt"), 20, 10, new[] { (8, 4), (9, 4), (10, 4) });
            WritePreset(Path.Combine(DataDir, "glider.txt"), 20, 10, new[] { (9, 3), (10, 4), (8, 5), (9, 5), (10, 5) });
            WritePreset(Path.Combine(DataDir, "beehive.txt"), 20, 10, new[] { (9, 3), (10, 3), (8, 4), (11, 4), (9, 5), (10, 5) });
            WritePreset(Path.Combine(DataDir, "toad.txt"), 20, 10, new[] { (9, 4), (10, 4), (11, 4), (8, 5), (9, 5), (10, 5) });
            Console.WriteLine($"Файлы пресетов созданы в {DataDir}");
        }

        static void WritePreset(string path, int cols, int rows, (int x, int y)[] cells)
        {
            var grid = new bool[cols, rows];
            foreach (var (x, y) in cells)
                grid[x, y] = true;

            var sb = new StringBuilder();
            sb.AppendLine($"{cols},{rows}");
            for (int y = 0; y < rows; y++)
            {
                sb.AppendLine(string.Join(",", Enumerable.Range(0, cols).Select(x => grid[x, y] ? "1" : "0")));
            }
            File.WriteAllText(path, sb.ToString());
        }

        static void RunSimulation()
        {
            string statesDir = Path.Combine(DataDir, "states");
            Directory.CreateDirectory(statesDir);

            int gen = 0;
            while (true)
            {
                Console.Clear();
                Render();
                Console.WriteLine($"Поколение: {gen}  |  Живых клеток: {board.CountAlive()}");
                Console.WriteLine("Нажмите Q для выхода или любую клавишу для паузы...");

                try
                {
                    board.Save(Path.Combine(statesDir, $"gen_{gen:D6}.txt"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка сохранения: {ex.Message}");
                }

                board.Advance();
                gen++;

                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Q) break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
            Console.WriteLine($"Симуляция остановлена. Сохранено {gen} поколений в {statesDir}");
        }

        static void RunResearch()
        {
            Console.WriteLine("Запуск исследования стабильности...");
            double[] densities = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
            int experiments = 10;
            var avgGens = new double[densities.Length];

            for (int i = 0; i < densities.Length; i++)
            {
                double d = densities[i];
                int total = 0;
                for (int e = 0; e < experiments; e++)
                {
                    var b = new Board(settings.Width, settings.Height, settings.CellSize, d);
                    total += BoardAnalyzer.RunUntilStable(b);
                }
                avgGens[i] = (double)total / experiments;
                Console.WriteLine($"  плотность={d:F1}: среднее {avgGens[i]:F1} поколений");
            }

            Directory.CreateDirectory(DataDir);
            string dataPath = Path.Combine(DataDir, "data.txt");
            using (var sw = new StreamWriter(dataPath))
            {
                sw.WriteLine("Плотность\tПоколений");
                for (int i = 0; i < densities.Length; i++)
                    sw.WriteLine($"{densities[i]:F1}\t{avgGens[i]:F2}");
            }

            var plt = new ScottPlot.Plot();
            plt.Add.Scatter(densities, avgGens);
            plt.Title("Игра «Жизнь»: исследование стабильности");
            plt.XLabel("Начальная плотность живых клеток");
            plt.YLabel("Среднее число поколений до стабилизации");
            string plotPath = Path.Combine(DataDir, "plot.png");
            plt.SavePng(plotPath, 800, 600);

            Console.WriteLine($"Данные сохранены в {dataPath}, график — в {plotPath}");
        }

        static void ShowMenu()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║          Игра 'Жизнь' - Клеточный автомат        ║");
            Console.WriteLine("╠══════════════════════════════════════════════════╣");
            Console.WriteLine("║  Доступные команды:                              ║");
            Console.WriteLine("║  run              Симуляция (из settings.json)   ║");
            Console.WriteLine("║  load <файл>      Загрузить поле из файла        ║");
            Console.WriteLine("║  analyze          Анализ случайного поля         ║");
            Console.WriteLine("║  research         Исследование стабильности      ║");
            Console.WriteLine("║  presets          Создать файлы фигур-пресетов   ║");
            Console.WriteLine("║  exit             Выход                          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.Write("\nВведите команду: ");
        }

        static void Main(string[] args)
        {
            settings = LoadSettings();

            ShowMenu();
            string input = Console.ReadLine()?.Trim() ?? string.Empty;
            string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string command = parts.Length > 0 ? parts[0].ToLower() : string.Empty;
            string argument = parts.Length > 1 ? parts[1] : string.Empty;

            Console.WriteLine();

            switch (command)
            {
                case "run":
                    Reset();
                    RunSimulation();
                    break;

                case "load":
                    if (string.IsNullOrWhiteSpace(argument))
                    {
                        Console.WriteLine("Укажите путь к файлу: load <файл>");
                        break;
                    }
                    if (!File.Exists(argument))
                    {
                        Console.WriteLine($"Файл не найден: {argument}");
                        break;
                    }
                    board = Board.Load(argument, settings.CellSize);
                    Console.WriteLine($"Загружено поле {board.Columns}×{board.Rows} из {argument}");
                    Thread.Sleep(1000);
                    RunSimulation();
                    break;

                case "analyze":
                    Reset();
                    PrintAnalysis();
                    break;

                case "research":
                    RunResearch();
                    break;

                case "presets":
                    CreatePresetFiles();
                    break;

                case "exit":
                case "quit":
                    break;

                default:
                    Console.WriteLine($"Неизвестная команда: \"{command}\"");
                    break;
            }
        }
    }
}