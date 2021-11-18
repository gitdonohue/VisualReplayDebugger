using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VisualReplayDebugger
{
    public class SearchContext
    {
        string SearchString;
        bool CaseInsensitive;
        Regex Rx;
        bool EmptySearchMatch;
        bool EmptyInputMatch;

        public bool Empty => Rx == null && string.IsNullOrEmpty(SearchString);

        public SearchContext(string searchString, bool caseInsensitive = true, bool emptySearchMatches = false, bool emptyInputMatches = false)
        {
            EmptySearchMatch = emptySearchMatches;
            EmptyInputMatch = emptyInputMatches;
            if (!string.IsNullOrEmpty(searchString))
            {
                RegexOptions opts = RegexOptions.Compiled;
                if (caseInsensitive) { opts |= RegexOptions.IgnoreCase; }
                try
                {
                    Rx = new Regex(searchString, opts);
                }
                catch (RegexParseException)
                {
                    // Use plain text search
                }

                SearchString = searchString;
                CaseInsensitive = caseInsensitive;
                if (CaseInsensitive)
                {
                    SearchString = SearchString?.ToLower();
                }
            }
        }

        public bool Match(string text)
        {
            if (string.IsNullOrEmpty(text)) return EmptyInputMatch;
            if (string.IsNullOrEmpty(SearchString)) return EmptySearchMatch;

            if (Rx != null)
            {
                return Rx.IsMatch(text);
            }
            else
            {
                if (CaseInsensitive)
                {
                    text = text.ToLowerInvariant();
                }
                return text.Contains(SearchString);
            }
        }
    }
}
