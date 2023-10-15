
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Chanoler.Utility
{
    //A collection of search and sort algorithms which work with Udon at runtime
    public class ChanArray : UdonSharpBehaviour
    {
        public static void InsertionSort<T>(T[] arr) where T : System.IComparable<T> => InsertionSort(arr, 0, arr.Length - 1);
        public static void InsertionSort<T>(T[] arr, int left, int right) where T : System.IComparable<T>
        {
            for (int i = left + 1; i <= right; i++)
            {
                T temp = arr[i];
                int j = i - 1;
                while (j >= left && arr[j].CompareTo(temp) > 0)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = temp;
            }
        }

        public static void InsertionSortKeyValue<K, V>(K[] keys, V[] values) where K : System.IComparable<K> => InsertionSortKeyValue(keys, values, 0, keys.Length - 1);
        public static void InsertionSortKeyValue<K, V>(K[] keys, V[] values, int left, int right) where K : System.IComparable<K>
        {
            for (int i = left + 1; i <= right; i++)
            {
                K tempKey = keys[i];
                V tempValue = values[i];
                int j = i - 1;
                while (j >= left && keys[j].CompareTo(tempKey) > 0)
                {
                    keys[j + 1] = keys[j];
                    values[j + 1] = values[j];
                    j--;
                }
                keys[j + 1] = tempKey;
                values[j + 1] = tempValue;
            }
        }


        public static void HeapSort<T>(T[] arr) where T : System.IComparable<T> => HeapSort(arr, 0, arr.Length - 1);
        private static void HeapSort<T>(T[] arr, int left, int right) where T : System.IComparable<T>
        {
            int i = left, j = right;
            T pivot = arr[(left + right) / 2];

            while (i <= j)
            {
                while (arr[i].CompareTo(pivot) < 0)
                    i++;
                while (arr[j].CompareTo(pivot) > 0)
                    j--;
                if (i <= j)
                {
                    //Swap
                    T tmp = arr[i];
                    arr[i] = arr[j];
                    arr[j] = tmp;
                    i++;
                    j--;
                }
            }

            //Recursive calls
            if (left < j)
                HeapSort(arr, left, j);
            if (i < right)
                HeapSort(arr, i, right);
        }



        public static void HeapSortKeyValue<K, V>(K[] arr, V[] arr2) where K : System.IComparable<K> => HeapSortKeyValue(arr, arr2, 0, arr.Length - 1);
        private static void HeapSortKeyValue<K, V>(K[] arr, V[] arr2, int left, int right) where K : System.IComparable<K>
        {
            int i = left, j = right;
            K pivot = arr[(left + right) / 2];

            while (i <= j)
            {
                while (arr[i].CompareTo(pivot) < 0)
                    i++;
                while (arr[j].CompareTo(pivot) > 0)
                    j--;
                if (i <= j)
                {
                    //Swap
                    K tmp = arr[i];
                    arr[i] = arr[j];
                    arr[j] = tmp;

                    V tmp2 = arr2[i];
                    arr2[i] = arr2[j];
                    arr2[j] = tmp2;

                    i++;
                    j--;
                }
            }

            //Recursive calls
            if (left < j)
                HeapSortKeyValue(arr, arr2, left, j);
            if (i < right)
                HeapSortKeyValue(arr, arr2, i, right);
        }

        public static void QuickSort<T>(T[] arr) where T : System.IComparable<T> => QuickSort(arr, 0, arr.Length - 1);
        private static void QuickSort<T>(T[] arr, int left, int right) where T : System.IComparable<T>
        {
            int i = left, j = right;
            T pivot = arr[(left + right) / 2];

            while (i <= j)
            {
                while (arr[i].CompareTo(pivot) < 0)
                    i++;
                while (arr[j].CompareTo(pivot) > 0)
                    j--;
                if (i <= j)
                {
                    //Swap
                    T tmp = arr[i];
                    arr[i] = arr[j];
                    arr[j] = tmp;

                    i++;
                    j--;
                }
            }

            //Recursive calls
            if (left < j)
                QuickSort(arr, left, j);
            if (i < right)
                QuickSort(arr, i, right);
        }

        public static void QuickSortKeyValue<K, V>(K[] arr, V[] arr2) where K : System.IComparable<K> => QuickSortKeyValue(arr, arr2, 0, arr.Length - 1);
        private static void QuickSortKeyValue<K, V>(K[] arr, V[] arr2, int left, int right) where K : System.IComparable<K>
        {
            int i = left, j = right;
            K pivot = arr[(left + right) / 2];

            while (i <= j)
            {
                while (arr[i].CompareTo(pivot) < 0)
                    i++;
                while (arr[j].CompareTo(pivot) > 0)
                    j--;
                if (i <= j)
                {
                    //Swap
                    K tmp = arr[i];
                    arr[i] = arr[j];
                    arr[j] = tmp;

                    V tmp2 = arr2[i];
                    arr2[i] = arr2[j];
                    arr2[j] = tmp2;

                    i++;
                    j--;
                }
            }

            //Recursive calls
            if (left < j)
                QuickSortKeyValue(arr, arr2, left, j);
            if (i < right)
                QuickSortKeyValue(arr, arr2, i, right);
        }

        //Introspective sort
        public static void IntroSort<T>(T[] arr) where T : System.IComparable<T> => IntroSort(arr, 0, arr.Length - 1, (int)(2 * System.Math.Log(arr.Length, 2)));
        private static void IntroSort<T>(T[] arr, int left, int right, int depthLimit = 0) where T : System.IComparable<T>
        {
            int partitionSize = right - left + 1;
            if (partitionSize <= 16)
            {
                InsertionSort(arr, left, right);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(arr, left, right);
                return;
            }

            int pivot = SelectPivot(arr, left, right);
            IntroSort(arr, left, pivot, depthLimit - 1);
            IntroSort(arr, pivot + 1, right, depthLimit - 1);
        }

        private static int SelectPivot<T>(T[] arr, int left, int right) where T : System.IComparable<T>
        {
            int mid = (left + right) / 2;
            if (arr[left].CompareTo(arr[mid]) > 0) {
                T tmp = arr[left];
                arr[left] = arr[mid];
                arr[mid] = tmp;
            }
            if (arr[left].CompareTo(arr[right]) > 0) {
                T tmp = arr[left];
                arr[left] = arr[right];
                arr[right] = tmp;
            }
            if (arr[mid].CompareTo(arr[right]) > 0) {
                T tmp = arr[mid];
                arr[mid] = arr[right];
                arr[right] = tmp;
            }
            return mid;
        }

        public static void IntroSortKeyValue<K, V>(K[] arr, V[] arr2) where K : System.IComparable<K> => IntroSortKeyValue(arr, arr2, 0, arr.Length - 1, (int)(2 * System.Math.Log(arr.Length, 2)));
        private static void IntroSortKeyValue<K, V>(K[] arr, V[] arr2, int left, int right, int depthLimit = 0) where K : System.IComparable<K>
        {
            int partitionSize = right - left + 1;
            if (partitionSize <= 16)
            {
                InsertionSortKeyValue(arr, arr2, left, right);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSortKeyValue(arr, arr2, left, right);
                return;
            }

            int pivot = SelectPivotKeyValue(arr, arr2, left, right);
            IntroSortKeyValue(arr, arr2, left, pivot, depthLimit - 1);
            IntroSortKeyValue(arr, arr2, pivot + 1, right, depthLimit - 1);
        }

        private static int SelectPivotKeyValue<K, V>(K[] arr, V[] arr2, int left, int right) where K : System.IComparable<K>
        {
            int mid = (left + right) / 2;
            if (arr[left].CompareTo(arr[mid]) > 0) {
                K tmp = arr[left];
                arr[left] = arr[mid];
                arr[mid] = tmp;

                V tmp2 = arr2[left];
                arr2[left] = arr2[mid];
                arr2[mid] = tmp2;
            }
            if (arr[left].CompareTo(arr[right]) > 0) {
                K tmp = arr[left];
                arr[left] = arr[right];
                arr[right] = tmp;

                V tmp2 = arr2[left];
                arr2[left] = arr2[right];
                arr2[right] = tmp2;
            }
            if (arr[mid].CompareTo(arr[right]) > 0) {
                K tmp = arr[mid];
                arr[mid] = arr[right];
                arr[right] = tmp;

                V tmp2 = arr2[mid];
                arr2[mid] = arr2[right];
                arr2[right] = tmp2;
            }
            return mid;
        }

        public static int BinarySearch<T>(T[] arr, T value) where T : System.IComparable<T>
        {
            int min = 0;
            int max = arr.Length - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                int comparison = arr[mid].CompareTo(value);
                if (comparison == 0)
                    return mid;
                else if (comparison < 0)
                    min = mid + 1;
                else
                    max = mid - 1;
            }
            return -1;
        }

        //Default sort
        public static void Sort<T>(T[] arr) where T : System.IComparable<T> => IntroSort(arr);
        public static void SortKeyValue<K, V>(K[] arr, V[] arr2) where K : System.IComparable<K> => IntroSortKeyValue(arr, arr2);
    }
}