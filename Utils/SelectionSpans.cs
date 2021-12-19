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
        private List<(int start, int stop)> spans = new();
        public IEnumerable<(int start, int stop)> Spans => spans;

        public IEnumerable<int> AllIndexes => spans.SelectMany(x=>Enumerable.Range(x.start,x.stop-x.start+1));
        
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
            if (spans.Any(s => s.start <= index && s.stop >= index))
            {
                // Already included
            }
            else
            {
                int indexBefore = spans.FindLastIndex(x => x.stop < index);
                int indexAfter = spans.FindIndex(x => x.start > index);
                if (indexBefore == -1)
                {
                    // Before first span
                    var s = spans.ElementAt(indexAfter);
                    if ( s.start == (index+1) )
                    {
                        // Add to start of first span
                        spans[indexAfter] = (index,s.stop);
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
                    if (s.stop == (index - 1))
                    {
                        // Add to end of last span
                        spans[indexBefore] = (s.start, index);
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
                    if (s_pre.stop == (index - 1)) // Just after pre
                    {
                        if (s_post.start == (index + 1)) // Touches post
                        {
                            // Merge contiguous spans
                            spans[indexBefore] = (s_pre.start, s_post.stop);
                            spans.RemoveAt(indexAfter);
                        }    
                        else
                        {
                            // Add to previous span
                            spans[indexBefore] = (s_pre.start, index);
                        }
                    }
                    else if (s_post.start == (index + 1)) // Just before post
                    {
                        spans[indexAfter] = (index, s_post.stop);
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
            int spanIndex = spans.FindIndex(x => x.start <= index && x.stop >= index);
            if (spanIndex != -1)
            {
                var s = spans.ElementAt(spanIndex);
                int spanLen = s.stop - s.start + 1;
                if (spanLen == 1)
                {
                    spans.RemoveAt(spanIndex);
                }
                else
                {
                    if (s.start == index)
                    {
                        spans[spanIndex] = (index+1,s.stop);
                    }
                    else if (s.stop == index)
                    {
                        spans[spanIndex] = (s.start, index-1);
                    }
                    else
                    {
                        spans[spanIndex] = (s.start, index-1);
                        spans.Insert(spanIndex + 1, (index+1, s.stop));
                    }
                }
                Changed?.Invoke();
            }
        }

        public bool Contains(int index) => Spans.Any(x => x.start <= index && x.stop >= index);

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
    }
}
