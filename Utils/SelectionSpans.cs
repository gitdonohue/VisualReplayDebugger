// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualReplayDebugger
{
    public class SelectionSpans
    {
        private List<(int, int)> spans = new();
        public IEnumerable<(int, int)> Spans => spans;

        public IEnumerable<int> AllIndexes => spans.SelectMany(x=>Enumerable.Range(x.Item1,x.Item2-x.Item1+1));
        
        public event Action Changed;
        
        public void Clear() 
        { 
            if (spans.Count > 0)
            {
                spans.Clear(); 
                Changed?.Invoke(); 
            }
        }

        public bool Empty => !spans.Any();

        public void SetSelection(int index)
        {
            spans.Clear();
            AddSelection(index);
        }

        public void AddSelection(int index) 
        {
            if (!spans.Any())
            {
                // First add
                spans.Add((index,index));
                Changed?.Invoke();
            }
            if (spans.Any(s => s.Item1 <= index && s.Item2 >= index))
            {
                // Already included
            }
            else
            {
                int indexBefore = spans.FindLastIndex(x => x.Item2 < index);
                int indexAfter = spans.FindIndex(x => x.Item1 > index);
                if (indexBefore == -1)
                {
                    // Before first span
                    var s = spans.ElementAt(indexAfter);
                    if ( s.Item1 == (index+1) )
                    {
                        // Add to start of first span
                        spans[indexAfter] = (index,s.Item2);
                    }
                    else
                    {
                        // Add before first span
                        spans.Insert(0, (index, index));
                    }
                }
                else if (indexAfter == -1)
                {
                    // After last span
                    var s = spans.ElementAt(indexBefore);
                    if (s.Item2 == (index - 1))
                    {
                        // Add to end of last span
                        spans[indexBefore] = (s.Item1, index);
                    }
                    else
                    {
                        // Add after last span
                        spans.Add((index, index));
                    }
                }
                else
                {
                    // Between spans
                    var s_pre = spans.ElementAt(indexBefore);
                    var s_post = spans.ElementAt(indexAfter);
                    if (s_pre.Item2 == (index - 1)) // Just after pre
                    {
                        if (s_post.Item1 == (index + 1)) // Touches post
                        {
                            // Merge contiguous spans
                            spans[indexBefore] = (s_pre.Item1, s_post.Item2);
                            spans.RemoveAt(indexAfter);
                        }    
                        else
                        {
                            // Add to previous span
                            spans[indexBefore] = (s_pre.Item1, index);
                        }
                    }
                    else if (s_post.Item1 == (index + 1)) // Just before post
                    {
                        spans[indexAfter] = (index, s_post.Item2);
                    }
                    else
                    {
                        // Not adjascent to any range
                        spans.Insert(indexBefore, (index, index));
                    }
                }
                Changed?.Invoke();
            }
        }

        public void RemoveSelectionAtIndex(int index) 
        {
            int spanIndex = spans.FindIndex(x => x.Item1 <= index && x.Item2 >= index);
            if (spanIndex != -1)
            {
                var s = spans.ElementAt(spanIndex);
                int spanLen = s.Item2 - s.Item1 + 1;
                if (spanLen == 1)
                {
                    spans.RemoveAt(spanIndex);
                }
                else
                {
                    if (s.Item1 == index)
                    {
                        spans[spanIndex] = (index+1,s.Item2);
                    }
                    else if (s.Item2 == index)
                    {
                        spans[spanIndex] = (s.Item1, index-1);
                    }
                    else
                    {
                        spans[spanIndex] = (s.Item1, index-1);
                        spans.Insert(spanIndex + 1, (index+1, s.Item2));
                    }
                }
                Changed?.Invoke();
            }
        }

        public bool Contains(int index) => Spans.Any(x => x.Item1 <= index && x.Item2 >= index);

        public void ToggleSelection(int index)
        {
            if (Contains(index))
            {
                RemoveSelectionAtIndex(index);
            }
            else
            {
                AddSelection(index);
            }
        }

        public void SetSelection(int start, int end)
        {
            spans.Clear();
            AddSelection(start,end);
        }

        public void AddSelection(int start, int end)
        {
            foreach (int index in Enumerable.Range(Math.Min(start, end), Math.Abs(end - start) + 1))
            {
                AddSelection(index);
            }
        }

        public void RemoveSelection(int start, int end)
        {
            foreach (int index in Enumerable.Range(Math.Min(start, end), Math.Abs(end - start) + 1))
            {
                RemoveSelectionAtIndex(index);
            }
        }

        public void ToggleSelection(int start, int end) 
        {
            foreach (int index in Enumerable.Range(Math.Min(start,end), Math.Abs(end - start) + 1))
            {
                ToggleSelection(index);
            }
        }

        //public bool GetNextSpanIndexFrom(int index, out int nextIndex)
        //{
        //    nextIndex = index;
        //    if (!Empty)
        //    {
        //        int firstIndex = spans.First().Item1;
        //        int lastIndex = spans.Last().Item2;
        //        if (index >= lastIndex) { return false; }
        //        if (index < firstIndex) { nextIndex = firstIndex; return true; }
        //        nextIndex = AllIndexes.FirstOrDefault(x => x > index);
        //        return true;
        //    }
        //    return false;
        //}

        //public bool GetPreviousSpanIndexFrom(int index, out int previousIndex)
        //{
        //    previousIndex = index;
        //    if (!Empty)
        //    {
        //        int firstIndex = spans.First().Item1;
        //        int lastIndex = spans.Last().Item2;
        //        if (index <= firstIndex) { return false; }
        //        if (index > lastIndex) { previousIndex = lastIndex; return true; }
        //        previousIndex = AllIndexes.LastOrDefault(x => x < index);
        //        return true;
        //    }
        //    return false;
        //}
    }
}
