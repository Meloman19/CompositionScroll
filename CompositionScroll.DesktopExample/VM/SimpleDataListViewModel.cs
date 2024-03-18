using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CompositionScroll.DesktopExample.VM
{
    public class SimpleDataListViewModel : ObservableObject
    {
        public SimpleDataListViewModel()
        {
            for (int i = 0; i < 1000; i++)
            {
                var text = i.ToString();
                if (i % 2 == 0)
                {
                    text += "\n" + text;
                }

                var data = new SimpleDataViewModel()
                {
                    Text = text,
                };
                Items.Add(data);
            }
        }

        public ObservableCollection<SimpleDataViewModel> Items { get; } = new ObservableCollection<SimpleDataViewModel>();
    }

    public class SimpleDataViewModel : ObservableObject
    {
        public string Text { get; set; }
    }
}