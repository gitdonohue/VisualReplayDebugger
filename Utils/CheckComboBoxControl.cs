// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace VisualReplayDebugger
{
    public class CheckComboBoxControl : ComboBox
    {
        public IEnumerable<string> SelectedItems => CheckBoxItems.Where(x=> x.IsChecked.Value).Select(x=>x.Content as string);
        public IEnumerable<string> UnselectedItems => CheckBoxItems.Where(x=> !x.IsChecked.Value).Select(x=>x.Content as string);

        public event Action Changed;

        private IEnumerable<CheckBox> CheckBoxItems => this.Items.Cast<object>().Skip(1).Cast<CheckBox>();

        private bool DoDropdownKeepOpenHack => true;

        public CheckComboBoxControl(string label) 
        {
            this.Width = 160;
            this.StaysOpenOnEdit = true; // Does not work, so as a workaround I'm forcing IsDropDownOpen on clicks
            this.IsEditable = true;
            this.Text = label;
            this.IsReadOnly = true;
            
            this.DropDownClosed += (o,e) => { this.Text = label; }; // Forces label
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (DoDropdownKeepOpenHack)
            {
                // Disable popup animation
                var popup = (Popup)Template.FindName("PART_Popup", this);
                popup.PopupAnimation = PopupAnimation.None;
            }
        }

        public void SetItems(IEnumerable<string> items, bool selected)
        {
            ClearItems();

            CheckBox all = new() { Content = "All" };
            all.Click += All_Click;
            this.Items.Add(all);

            foreach (string item in items)
            {
                CheckBox cbi = new() { Content = item, IsChecked = selected };
                cbi.Click += Cbi_Click;
                this.Items.Add(cbi);
            }
        }

        public void ClearItems()
        {
            if (this.Items.Count > 0)
            {
                var all = this.Items[0] as CheckBox;
                all.Click -= All_Click;
            }

            foreach (var cbi in CheckBoxItems)
            {
                cbi.Click -= Cbi_Click;
            }
            this.Items.Clear();
        }

        private void All_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool allChecked = (sender as CheckBox).IsChecked.Value;

            foreach ( var cbi in CheckBoxItems)
            {
                cbi.IsChecked = allChecked;
            }
            Changed?.Invoke();
            if (DoDropdownKeepOpenHack) IsDropDownOpen = true; // Hack to keep the dropdown open
        }

        private void Cbi_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Changed?.Invoke();
            if (DoDropdownKeepOpenHack) IsDropDownOpen = true; // Hack to keep the dropdown open
        }
    }
}
