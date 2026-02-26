using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Lab_rab_2_KutlubaevAD_БПИ_23_02.Model;

namespace Lab_rab_2_KutlubaevAD_БПИ_23_02.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropChanged(name);
            return true;
        }

        private readonly ArraySorter _sorter;
        private readonly SynchronizationContext _uiContext;

        private int[] _originalArray;

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private volatile int _bubbleRunning;
        private volatile int _quickRunning;
        private volatile int _insertionRunning;
        private volatile int _shakerRunning;

        private long _bubbleCmpSum;
        private long _quickCmpSum;
        private long _insertionCmpSum;
        private long _shakerCmpSum;

        private Stopwatch _bubbleSw;
        private Stopwatch _quickSw;
        private Stopwatch _insertionSw;
        private Stopwatch _shakerSw;

        private double[] _bubbleProgresses;
        private double[] _quickProgresses;
        private double[] _insertionProgresses;
        private double[] _shakerProgresses;


        private int _arraySize = 5000;
        public int ArraySize
        {
            get => _arraySize;
            set { if (SetField(ref _arraySize, value)) GenerateArrayCommand.RaiseCanExecuteChanged(); }
        }

        private string _originalArrayString = "Массив не сгенерирован";
        public string OriginalArrayString
        {
            get => _originalArrayString;
            set => SetField(ref _originalArrayString, value);
        }
        private string _bubbleSortResult = "—";
        public string BubbleSortResult { get => _bubbleSortResult; set => SetField(ref _bubbleSortResult, value); }

        private string _quickSortResult = "—";
        public string QuickSortResult { get => _quickSortResult; set => SetField(ref _quickSortResult, value); }

        private string _insertionSortResult = "—";
        public string InsertionSortResult { get => _insertionSortResult; set => SetField(ref _insertionSortResult, value); }

        private string _shakerSortResult = "—";
        public string ShakerSortResult { get => _shakerSortResult; set => SetField(ref _shakerSortResult, value); }
        private double _bubbleProgress;
        public double BubbleProgress { get => _bubbleProgress; set => SetField(ref _bubbleProgress, value); }

        private double _quickProgress;
        public double QuickProgress { get => _quickProgress; set => SetField(ref _quickProgress, value); }

        private double _insertionProgress;
        public double InsertionProgress { get => _insertionProgress; set => SetField(ref _insertionProgress, value); }

        private double _shakerProgress;
        public double ShakerProgress { get => _shakerProgress; set => SetField(ref _shakerProgress, value); }

        private string _totalComparisons = "Общее число сравнений: 0";
        public string TotalComparisons { get => _totalComparisons; set => SetField(ref _totalComparisons, value); }

        public int[] ThreadCounts { get; } = { 1, 2, 3, 4 };

        private int _threadCount = 1;
        public int ThreadCount
        {
            get => _threadCount;
            set => SetField(ref _threadCount, value);
        }

        private bool _useSharedArray;
        public bool UseSharedArray
        {
            get => _useSharedArray;
            set
            {
                if (SetField(ref _useSharedArray, value))
                    UpdateSharedArrayNote();
            }
        }

        private string _sharedArrayNote = "Режим: каждый поток сортирует свою копию массива.";
        public string SharedArrayNote { get => _sharedArrayNote; set => SetField(ref _sharedArrayNote, value); }

        public RelayCommand GenerateArrayCommand { get; }
        public RelayCommand BubbleSortCommand { get; }
        public RelayCommand QuickSortCommand { get; }
        public RelayCommand InsertionSortCommand { get; }
        public RelayCommand ShakerSortCommand { get; }
        public RelayCommand StopAllCommand { get; }

        public MainViewModel()
        {
            _sorter = new ArraySorter();
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();

            _sorter.SortCompleted += OnSortCompleted;
            _sorter.ProgressChanged += OnProgressChanged;

            GenerateArrayCommand  = new RelayCommand(GenerateArray,  () => ArraySize > 0);
            BubbleSortCommand = new RelayCommand(StartBubble, CanStartBubble);
            QuickSortCommand = new RelayCommand(StartQuick, CanStartQuick);
            InsertionSortCommand = new RelayCommand(StartInsertion, CanStartInsertion);
            ShakerSortCommand = new RelayCommand(StartShaker, CanStartShaker);
            StopAllCommand = new RelayCommand(StopAll);
        }

        private void GenerateArray()
        {
            StopAll();

            _cts = new CancellationTokenSource();

            _sorter.ResetTotalComparisons();
            _originalArray = _sorter.GenerateRandomArray(ArraySize);

            int preview = Math.Min(20, _originalArray.Length);
            string dots = ArraySize > 20 ? "..." : "";
            OriginalArrayString = "Массив: " + string.Join(", ", _originalArray, 0, preview) + dots;

            BubbleSortResult = QuickSortResult = InsertionSortResult = ShakerSortResult = "—";
            BubbleProgress = QuickProgress = InsertionProgress = ShakerProgress = 0;
            TotalComparisons = "Общее число сравнений: 0";

            RefreshAllCommands();
        }

        private bool CanStartBubble()    => _originalArray != null && _bubbleRunning == 0;
        private bool CanStartQuick()     => _originalArray != null && _quickRunning == 0;
        private bool CanStartInsertion() => _originalArray != null && _insertionRunning == 0;
        private bool CanStartShaker()    => _originalArray != null && _shakerRunning == 0;

        private void StartBubble()    => LaunchSort("Bubble");
        private void StartQuick()     => LaunchSort("Quick");
        private void StartInsertion() => LaunchSort("Insertion");
        private void StartShaker()    => LaunchSort("Shaker");

        private void LaunchSort(string name)
        {
            int n = ThreadCount;
            var ct = _cts.Token;

            switch (name)
            {
                case "Bubble":
                    _bubbleRunning = n;
                    _bubbleCmpSum = 0;
                    _bubbleProgresses = new double[n];
                    _bubbleSw = Stopwatch.StartNew();
                    BubbleSortResult = BuildRunningLabel(name, n);
                    BubbleProgress = 0;
                    BubbleSortCommand.RaiseCanExecuteChanged();
                    for (int i = 0; i < n; i++)
                    { int idx = i; new Thread(() => _sorter.BubbleSort(_originalArray, UseSharedArray, ct, idx)) { IsBackground = true }.Start(); }
                    break;

                case "Quick":
                    _quickRunning = n;
                    _quickCmpSum = 0;
                    _quickProgresses = new double[n];
                    _quickSw = Stopwatch.StartNew();
                    QuickSortResult = BuildRunningLabel(name, n);
                    QuickProgress = 0;
                    QuickSortCommand.RaiseCanExecuteChanged();
                    for (int i = 0; i < n; i++)
                    { int idx = i; new Thread(() => _sorter.QuickSort(_originalArray, UseSharedArray, ct, idx)) { IsBackground = true }.Start(); }
                    break;

                case "Insertion":
                    _insertionRunning = n;
                    _insertionCmpSum = 0;
                    _insertionProgresses = new double[n];
                    _insertionSw = Stopwatch.StartNew();
                    InsertionSortResult = BuildRunningLabel(name, n);
                    InsertionProgress = 0;
                    InsertionSortCommand.RaiseCanExecuteChanged();
                    for (int i = 0; i < n; i++)
                    { int idx = i; new Thread(() => _sorter.InsertionSort(_originalArray, UseSharedArray, ct, idx)) { IsBackground = true }.Start(); }
                    break;

                case "Shaker":
                    _shakerRunning = n;
                    _shakerCmpSum = 0;
                    _shakerProgresses = new double[n];
                    _shakerSw = Stopwatch.StartNew();
                    ShakerSortResult = BuildRunningLabel(name, n);
                    ShakerProgress = 0;
                    ShakerSortCommand.RaiseCanExecuteChanged();
                    for (int i = 0; i < n; i++)
                    { int idx = i; new Thread(() => _sorter.ShakerSort(_originalArray, UseSharedArray, ct, idx)) { IsBackground = true }.Start(); }
                    break;
            }
        }

        private string BuildRunningLabel(string name, int n)
        {
            string modeTag = UseSharedArray ? "[общий массив]" : $"[{n} поток(ов)]";
            return $"{LocalizeName(name)} {modeTag}: сортируется...";
        }

        private void StopAll()
        {
            _cts.Cancel();
        }

        private void OnProgressChanged(string sortName, double progress, int threadIndex)
        {
            double avg = progress;

            switch (sortName)
            {
                case "Bubble":
                    if (_bubbleProgresses != null && threadIndex < _bubbleProgresses.Length)
                    {
                        _bubbleProgresses[threadIndex] = progress;
                        avg = Average(_bubbleProgresses);
                    }
                    double bubbleAvg = avg;
                    _uiContext.Post(_ => BubbleProgress = bubbleAvg, null);
                    break;

                case "Quick":
                    if (_quickProgresses != null && threadIndex < _quickProgresses.Length)
                    {
                        _quickProgresses[threadIndex] = progress;
                        avg = Average(_quickProgresses);
                    }
                    double quickAvg = avg;
                    _uiContext.Post(_ => QuickProgress = quickAvg, null);
                    break;

                case "Insertion":
                    if (_insertionProgresses != null && threadIndex < _insertionProgresses.Length)
                    {
                        _insertionProgresses[threadIndex] = progress;
                        avg = Average(_insertionProgresses);
                    }
                    double insAvg = avg;
                    _uiContext.Post(_ => InsertionProgress = insAvg, null);
                    break;

                case "Shaker":
                    if (_shakerProgresses != null && threadIndex < _shakerProgresses.Length)
                    {
                        _shakerProgresses[threadIndex] = progress;
                        avg = Average(_shakerProgresses);
                    }
                    double shakAvg = avg;
                    _uiContext.Post(_ => ShakerProgress = shakAvg, null);
                    break;
            }
        }

        private void OnSortCompleted(string sortName, int[] result, long comparisons,
            double elapsedMs, int threadIndex, bool cancelled)
        {
            int remaining;
            switch (sortName)
            {
                case "Bubble":
                    Interlocked.Add(ref _bubbleCmpSum, comparisons);
                    remaining = Interlocked.Decrement(ref _bubbleRunning);
                    if (remaining == 0)
                    {
                        _bubbleSw.Stop();
                        long ms = _bubbleSw.ElapsedMilliseconds;
                        long cmp = Interlocked.Read(ref _bubbleCmpSum);
                        bool c = cancelled;
                        int n = ThreadCount;
                        _uiContext.Post(_ =>
                        {
                            BubbleProgress = 100;
                            BubbleSortResult = BuildResult("Пузырьковая", result, cmp, ms, n, c, UseSharedArray);
                            UpdateTotalComparisons();
                            BubbleSortCommand.RaiseCanExecuteChanged();
                        }, null);
                    }
                    break;

                case "Quick":
                    Interlocked.Add(ref _quickCmpSum, comparisons);
                    remaining = Interlocked.Decrement(ref _quickRunning);
                    if (remaining == 0)
                    {
                        _quickSw.Stop();
                        long ms = _quickSw.ElapsedMilliseconds;
                        long cmp = Interlocked.Read(ref _quickCmpSum);
                        bool c = cancelled;
                        int n = ThreadCount;
                        _uiContext.Post(_ =>
                        {
                            QuickProgress = 100;
                            QuickSortResult = BuildResult("Быстрая", result, cmp, ms, n, c, UseSharedArray);
                            UpdateTotalComparisons();
                            QuickSortCommand.RaiseCanExecuteChanged();
                        }, null);
                    }
                    break;

                case "Insertion":
                    Interlocked.Add(ref _insertionCmpSum, comparisons);
                    remaining = Interlocked.Decrement(ref _insertionRunning);
                    if (remaining == 0)
                    {
                        _insertionSw.Stop();
                        long ms = _insertionSw.ElapsedMilliseconds;
                        long cmp = Interlocked.Read(ref _insertionCmpSum);
                        bool c = cancelled;
                        int n = ThreadCount;
                        _uiContext.Post(_ =>
                        {
                            InsertionProgress = 100;
                            InsertionSortResult = BuildResult("Вставками", result, cmp, ms, n, c, UseSharedArray);
                            UpdateTotalComparisons();
                            InsertionSortCommand.RaiseCanExecuteChanged();
                        }, null);
                    }
                    break;

                case "Shaker":
                    Interlocked.Add(ref _shakerCmpSum, comparisons);
                    remaining = Interlocked.Decrement(ref _shakerRunning);
                    if (remaining == 0)
                    {
                        _shakerSw.Stop();
                        long ms = _shakerSw.ElapsedMilliseconds;
                        long cmp = Interlocked.Read(ref _shakerCmpSum);
                        bool c = cancelled;
                        int n = ThreadCount;
                        _uiContext.Post(_ =>
                        {
                            ShakerProgress = 100;
                            ShakerSortResult = BuildResult("Шейкерная", result, cmp, ms, n, c, UseSharedArray);
                            UpdateTotalComparisons();
                            ShakerSortCommand.RaiseCanExecuteChanged();
                        }, null);
                    }
                    break;
            }
        }


        private string BuildResult(string label, int[] arr, long cmp, long ms,
            int threadCount, bool cancelled, bool shared)
        {
            if (cancelled)
                return $"{label}: Отменено (прошло {ms} мс)";

            string modeInfo = shared
                ? "[общий массив — результат может быть некорректным!]"
                : (threadCount > 1 ? $"[{threadCount} потока параллельно]" : "");

            return $"{label} {modeInfo}: {FormatArray(arr)} | " +
                   $"время: {ms} мс | сравнений (сумма): {cmp:N0}";
        }

        private string FormatArray(int[] arr)
        {
            if (arr == null) return "—";
            if (arr.Length <= 8) return "[" + string.Join(", ", arr) + "]";
            return "[" + string.Join(", ", arr, 0, 5) + " ... " +
                   string.Join(", ", arr, arr.Length - 3, 3) + "]";
        }

        private void UpdateTotalComparisons()
        {
            TotalComparisons = $"Общее число сравнений (все потоки): {_sorter.TotalComparisons:N0}";
        }

        private void UpdateSharedArrayNote()
        {
            SharedArrayNote = UseSharedArray
                ? "Режим общего массива: все потоки сортируют один и тот же массив. " +
                  "Каждая операция защищена lock — результат корректен, но производительность " +
                  "резко падает из-за конкуренции потоков за одну блокировку."
                : "Режим копий (по умолчанию): каждый поток получает независимую копию массива.";
        }

        private void RefreshAllCommands()
        {
            GenerateArrayCommand.RaiseCanExecuteChanged();
            BubbleSortCommand.RaiseCanExecuteChanged();
            QuickSortCommand.RaiseCanExecuteChanged();
            InsertionSortCommand.RaiseCanExecuteChanged();
            ShakerSortCommand.RaiseCanExecuteChanged();
        }

        private static double Average(double[] arr)
        {
            if (arr == null || arr.Length == 0) return 0;
            double sum = 0;
            for (int i = 0; i < arr.Length; i++) sum += arr[i];
            return sum / arr.Length;
        }

        private static string LocalizeName(string name)
        {
            switch (name)
            {
                case "Bubble": return "Пузырьковая";
                case "Quick": return "Быстрая";
                case "Insertion": return "Вставками";
                case "Shaker": return "Шейкерная";
                default: return name;
            }
        }
    }
}
