using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Lab_rab_2_KutlubaevAD_БПИ_23_02.Model
{
    public struct SortResult
    {
        public int[] SortedArray;
        public long Comparisons;
        public double ElapsedMs;
        public bool Cancelled;
    }

    public class ArraySorter
    {
        private long _totalComparisons;
        private readonly object _locker = new object();

        public long TotalComparisons => Interlocked.Read(ref _totalComparisons);

        public void ResetTotalComparisons()
        {
            Interlocked.Exchange(ref _totalComparisons, 0L);
        }

        public delegate void SortCompletedHandler(
            string sortName, int[] result, long comparisons,
            double elapsedMs, int threadIndex, bool cancelled);
        public event SortCompletedHandler SortCompleted;

        public delegate void SortProgressHandler(string sortName, double progress, int threadIndex);
        public event SortProgressHandler ProgressChanged;

        public int[] GenerateRandomArray(int size)
        {
            var rand = new Random();
            var arr = new int[size];
            for (int i = 0; i < size; i++)
                arr[i] = rand.Next(10000);
            return arr;
        }

        private static int[] CopyArray(int[] src)
        {
            var dst = new int[src.Length];
            Array.Copy(src, dst, src.Length);
            return dst;
        }

        public void BubbleSort(int[] original, bool useShared, CancellationToken ct, int threadIndex = 0)
        {
            int[] arr = useShared ? original : CopyArray(original);
            long cmp = 0;
            int n = arr.Length;
            int reportStep = Math.Max(1, n / 100);
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < n - 1; i++)
            {
                if (ct.IsCancellationRequested) break;

                for (int j = 0; j < n - 1 - i; j++)
                {
                    if (ct.IsCancellationRequested) break;

                    if (useShared)
                    {
                        lock (_locker)
                        {
                            cmp++;
                            if (arr[j] > arr[j + 1])
                            { int t = arr[j]; arr[j] = arr[j + 1]; arr[j + 1] = t; }
                        }
                    }
                    else
                    {
                        cmp++;
                        if (arr[j] > arr[j + 1])
                        { int t = arr[j]; arr[j] = arr[j + 1]; arr[j + 1] = t; }
                    }
                }

                if (i % reportStep == 0)
                    ProgressChanged?.Invoke("Bubble", (double)(i + 1) / (n - 1) * 100.0, threadIndex);
            }

            sw.Stop();
            lock (_locker) { _totalComparisons += cmp; }
            ProgressChanged?.Invoke("Bubble", 100.0, threadIndex);
            SortCompleted?.Invoke("Bubble", arr, cmp, sw.Elapsed.TotalMilliseconds, threadIndex, ct.IsCancellationRequested);
        }

        public void QuickSort(int[] original, bool useShared, CancellationToken ct, int threadIndex = 0)
        {
            int[] arr = useShared ? original : CopyArray(original);
            long cmp = 0;
            int n = arr.Length;
            double expected = n * Math.Log(n + 2, 2);
            var sw = Stopwatch.StartNew();

            QuickSortRecursive(arr, 0, n - 1, ref cmp, expected, ct, useShared, threadIndex);

            sw.Stop();
            lock (_locker) { _totalComparisons += cmp; }
            ProgressChanged?.Invoke("Quick", 100.0, threadIndex);
            SortCompleted?.Invoke("Quick", arr, cmp, sw.Elapsed.TotalMilliseconds, threadIndex, ct.IsCancellationRequested);
        }

        private void QuickSortRecursive(int[] arr, int left, int right,
            ref long cmp, double expected, CancellationToken ct, bool useShared, int threadIndex)
        {
            if (left >= right || ct.IsCancellationRequested) return;
            int pivot = Partition(arr, left, right, ref cmp, useShared);
            ProgressChanged?.Invoke("Quick", Math.Min(99.0, cmp / expected * 100.0), threadIndex);
            QuickSortRecursive(arr, left, pivot - 1, ref cmp, expected, ct, useShared, threadIndex);
            QuickSortRecursive(arr, pivot + 1, right, ref cmp, expected, ct, useShared, threadIndex);
        }

        private int Partition(int[] arr, int left, int right, ref long cmp, bool useShared)
        {
            int pivot = arr[right];
            int i = left - 1;
            for (int j = left; j < right; j++)
            {
                if (useShared)
                {
                    lock (_locker)
                    {
                        cmp++;
                        if (arr[j] < pivot)
                        { i++; int t = arr[i]; arr[i] = arr[j]; arr[j] = t; }
                    }
                }
                else
                {
                    cmp++;
                    if (arr[j] < pivot)
                    { i++; int t = arr[i]; arr[i] = arr[j]; arr[j] = t; }
                }
            }
            int tmp = arr[i + 1]; arr[i + 1] = arr[right]; arr[right] = tmp;
            return i + 1;
        }

        public void InsertionSort(int[] original, bool useShared, CancellationToken ct, int threadIndex = 0)
        {
            int[] arr = useShared ? original : CopyArray(original);
            long cmp = 0;
            int n = arr.Length;
            int reportStep = Math.Max(1, n / 100);
            var sw = Stopwatch.StartNew();

            for (int i = 1; i < n; i++)
            {
                if (ct.IsCancellationRequested) break;

                if (useShared)
                {
                    int key;
                    lock (_locker) { key = arr[i]; }
                    int j = i - 1;
                    while (j >= 0)
                    {
                        bool shift;
                        lock (_locker) { cmp++; shift = arr[j] > key; }
                        if (!shift) break;
                        lock (_locker) { arr[j + 1] = arr[j]; }
                        j--;
                    }
                    lock (_locker) { arr[j + 1] = key; }
                }
                else
                {
                    int key = arr[i];
                    int j = i - 1;
                    while (j >= 0 && arr[j] > key)
                    { cmp++; arr[j + 1] = arr[j]; j--; }
                    cmp++;
                    arr[j + 1] = key;
                }

                if (i % reportStep == 0)
                    ProgressChanged?.Invoke("Insertion", (double)i / (n - 1) * 100.0, threadIndex);
            }

            sw.Stop();
            lock (_locker) { _totalComparisons += cmp; }
            ProgressChanged?.Invoke("Insertion", 100.0, threadIndex);
            SortCompleted?.Invoke("Insertion", arr, cmp, sw.Elapsed.TotalMilliseconds, threadIndex, ct.IsCancellationRequested);
        }

        public void ShakerSort(int[] original, bool useShared, CancellationToken ct, int threadIndex = 0)
        {
            int[] arr = useShared ? original : CopyArray(original);
            long cmp = 0;
            int n = arr.Length;
            var sw = Stopwatch.StartNew();

            int left = 0, right = n - 1;
            int pass = 0;
            int maxPasses = Math.Max(1, n / 2);

            while (left < right && !ct.IsCancellationRequested)
            {
                for (int i = left; i < right && !ct.IsCancellationRequested; i++)
                {
                    if (useShared)
                    {
                        lock (_locker)
                        {
                            cmp++;
                            if (arr[i] > arr[i + 1])
                            { int t = arr[i]; arr[i] = arr[i + 1]; arr[i + 1] = t; }
                        }
                    }
                    else
                    {
                        cmp++;
                        if (arr[i] > arr[i + 1])
                        { int t = arr[i]; arr[i] = arr[i + 1]; arr[i + 1] = t; }
                    }
                }
                right--;

                for (int i = right; i > left && !ct.IsCancellationRequested; i--)
                {
                    if (useShared)
                    {
                        lock (_locker)
                        {
                            cmp++;
                            if (arr[i] < arr[i - 1])
                            { int t = arr[i]; arr[i] = arr[i - 1]; arr[i - 1] = t; }
                        }
                    }
                    else
                    {
                        cmp++;
                        if (arr[i] < arr[i - 1])
                        { int t = arr[i]; arr[i] = arr[i - 1]; arr[i - 1] = t; }
                    }
                }
                left++;
                pass++;

                ProgressChanged?.Invoke("Shaker",
                    Math.Min(99.0, (double)pass / maxPasses * 100.0), threadIndex);
            }

            sw.Stop();
            lock (_locker) { _totalComparisons += cmp; }
            ProgressChanged?.Invoke("Shaker", 100.0, threadIndex);
            SortCompleted?.Invoke("Shaker", arr, cmp, sw.Elapsed.TotalMilliseconds, threadIndex, ct.IsCancellationRequested);
        }


        public Task<SortResult> BubbleSortAsync(int[] original, CancellationToken ct, Action<double> onProgress = null)
        {
            return Task.Run(() =>
            {
                int[] arr = CopyArray(original);
                long cmp = 0;
                int n = arr.Length;
                int reportStep = Math.Max(1, n / 100);
                var sw = Stopwatch.StartNew();

                for (int i = 0; i < n - 1; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    for (int j = 0; j < n - 1 - i; j++)
                    {
                        if (ct.IsCancellationRequested) break;
                        cmp++;
                        if (arr[j] > arr[j + 1])
                        { int t = arr[j]; arr[j] = arr[j + 1]; arr[j + 1] = t; }
                    }
                    if (i % reportStep == 0)
                        onProgress?.Invoke((double)(i + 1) / (n - 1) * 100.0);
                }

                sw.Stop();
                lock (_locker) { _totalComparisons += cmp; }
                onProgress?.Invoke(100.0);
                return new SortResult
                {
                    SortedArray = arr,
                    Comparisons = cmp,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    Cancelled = ct.IsCancellationRequested
                };
            }, ct);
        }

        public Task<SortResult> QuickSortAsync(int[] original, CancellationToken ct, Action<double> onProgress = null)
        {
            return Task.Run(() =>
            {
                int[] arr = CopyArray(original);
                long cmp = 0;
                int n = arr.Length;
                double expected = n * Math.Log(n + 2, 2);
                var sw = Stopwatch.StartNew();

                QuickSortRecursiveTask(arr, 0, n - 1, ref cmp, expected, ct, onProgress);

                sw.Stop();
                lock (_locker) { _totalComparisons += cmp; }
                onProgress?.Invoke(100.0);
                return new SortResult
                {
                    SortedArray = arr,
                    Comparisons = cmp,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    Cancelled = ct.IsCancellationRequested
                };
            }, ct);
        }

        private void QuickSortRecursiveTask(int[] arr, int left, int right,
            ref long cmp, double expected, CancellationToken ct, Action<double> onProgress)
        {
            if (left >= right || ct.IsCancellationRequested) return;
            int pivot = PartitionTask(arr, left, right, ref cmp);
            onProgress?.Invoke(Math.Min(99.0, cmp / expected * 100.0));
            QuickSortRecursiveTask(arr, left, pivot - 1, ref cmp, expected, ct, onProgress);
            QuickSortRecursiveTask(arr, pivot + 1, right, ref cmp, expected, ct, onProgress);
        }

        private int PartitionTask(int[] arr, int left, int right, ref long cmp)
        {
            int pivot = arr[right];
            int i = left - 1;
            for (int j = left; j < right; j++)
            {
                cmp++;
                if (arr[j] < pivot)
                { i++; int t = arr[i]; arr[i] = arr[j]; arr[j] = t; }
            }
            int tmp = arr[i + 1]; arr[i + 1] = arr[right]; arr[right] = tmp;
            return i + 1;
        }

        public Task<SortResult> InsertionSortAsync(int[] original, CancellationToken ct, Action<double> onProgress = null)
        {
            return Task.Run(() =>
            {
                int[] arr = CopyArray(original);
                long cmp = 0;
                int n = arr.Length;
                int reportStep = Math.Max(1, n / 100);
                var sw = Stopwatch.StartNew();

                for (int i = 1; i < n; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    int key = arr[i];
                    int j = i - 1;
                    while (j >= 0 && arr[j] > key)
                    { cmp++; arr[j + 1] = arr[j]; j--; }
                    cmp++;
                    arr[j + 1] = key;

                    if (i % reportStep == 0)
                        onProgress?.Invoke((double)i / (n - 1) * 100.0);
                }

                sw.Stop();
                lock (_locker) { _totalComparisons += cmp; }
                onProgress?.Invoke(100.0);
                return new SortResult
                {
                    SortedArray = arr,
                    Comparisons = cmp,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    Cancelled = ct.IsCancellationRequested
                };
            }, ct);
        }

        public Task<SortResult> ShakerSortAsync(int[] original, CancellationToken ct, Action<double> onProgress = null)
        {
            return Task.Run(() =>
            {
                int[] arr = CopyArray(original);
                long cmp = 0;
                int n = arr.Length;
                var sw = Stopwatch.StartNew();

                int left = 0, right = n - 1;
                int pass = 0;
                int maxPasses = Math.Max(1, n / 2);

                while (left < right && !ct.IsCancellationRequested)
                {
                    for (int i = left; i < right && !ct.IsCancellationRequested; i++)
                    {
                        cmp++;
                        if (arr[i] > arr[i + 1])
                        { int t = arr[i]; arr[i] = arr[i + 1]; arr[i + 1] = t; }
                    }
                    right--;

                    for (int i = right; i > left && !ct.IsCancellationRequested; i--)
                    {
                        cmp++;
                        if (arr[i] < arr[i - 1])
                        { int t = arr[i]; arr[i] = arr[i - 1]; arr[i - 1] = t; }
                    }
                    left++;
                    pass++;
                    onProgress?.Invoke(Math.Min(99.0, (double)pass / maxPasses * 100.0));
                }

                sw.Stop();
                lock (_locker) { _totalComparisons += cmp; }
                onProgress?.Invoke(100.0);
                return new SortResult
                {
                    SortedArray = arr,
                    Comparisons = cmp,
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    Cancelled = ct.IsCancellationRequested
                };
            }, ct);
        }
    }
}
