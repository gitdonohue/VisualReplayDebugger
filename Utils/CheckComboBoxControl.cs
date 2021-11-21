// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace VisualReplayDebugger
{
    public class CheckComboBoxControl : ComboBox
    {
        public IEnumerable<string> SelectedItems => this.Items.Cast<CheckBox>().Where(x=>x.IsChecked.Value).Select(x=>x.Content as string);
        public IEnumerable<string> UnselectedItems => this.Items.Cast<CheckBox>().Where(x=>!x.IsChecked.Value).Select(x=>x.Content as string);

        public event Action Changed;

        public CheckComboBoxControl(string label) 
        {
            this.Width = 160;
            this.StaysOpenOnEdit = true; // Does not work
            this.IsEditable = true;
            this.Text = label;
            this.IsReadOnly = true;

            this.DropDownClosed += (o,e) => { this.Text = label; }; // Forces label
        }

        public void SetItems(IEnumerable<string> items, bool selected)
        {
            ClearItems();
            foreach (string item in items)
            {
                CheckBox cbi = new() { Content = item, IsChecked = selected };
                cbi.Click += Cbi_Click;
                this.Items.Add(cbi);
            }
        }

        public void ClearItems()
        {
            foreach (var cbi in this.Items.Cast<CheckBox>())
            {
                cbi.Click -= Cbi_Click;
            }
            this.Items.Clear();
        }

        private void Cbi_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Changed?.Invoke();
        }
    }
}
