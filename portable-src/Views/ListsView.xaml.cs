using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Takoboto.Database;

namespace TakobotoApp
{
    /// <summary>History, favourites, and the user's own lists.</summary>
    public partial class ListsView : UserControl
    {
        enum Mode { History, Favorites, Overview, UserList }

        Mode mode = Mode.History;
        UserList userList;
        readonly ObservableCollection<WordItem> items = new ObservableCollection<WordItem>();
        int loadToken;

        public ListsView()
        {
            InitializeComponent();
            List.ItemsSource = items;
            // the "remove" button inside every row
            List.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnRowButton));
            NewListName.TextChanged += (s, e) => NewListHint.Visibility = NewListName.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Store.ListsChanged += OnStoreChanged;
        }

        void OnStoreChanged()
        {
            // keep the open page in sync when something was added elsewhere (e.g. a favourite from the search page)
            if (Visibility == Visibility.Visible) Reload(keepSelection: true);
        }

        // ---- entry points ---------------------------------------------------------------------------------------
        public void ShowHistory() { mode = Mode.History; userList = null; Reload(); }
        public void ShowFavorites() { mode = Mode.Favorites; userList = null; Reload(); }
        public void ShowOverview() { mode = Mode.Overview; userList = null; Reload(); }
        public void ShowUserList(UserList l) { mode = Mode.UserList; userList = l; Reload(); }

        List<SavedEntry> CurrentEntries()
        {
            if (mode == Mode.History) return Store.Lists.History;
            if (mode == Mode.Favorites) return Store.Lists.Favorites;
            if (mode == Mode.UserList && userList != null) return userList.Items;
            return null;
        }

        void Reload(bool keepSelection = false)
        {
            bool overview = mode == Mode.Overview;
            Overview.Visibility = overview ? Visibility.Visible : Visibility.Collapsed;
            Entries.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            BackBtn.Visibility = mode == Mode.UserList ? Visibility.Visible : Visibility.Collapsed;
            RenameBtn.Visibility = mode == Mode.UserList ? Visibility.Visible : Visibility.Collapsed;
            DeleteBtn.Visibility = mode == Mode.UserList ? Visibility.Visible : Visibility.Collapsed;
            ClearBtn.Visibility = overview ? Visibility.Collapsed : Visibility.Visible;
            ClearBtn.Content = mode == Mode.UserList ? "Empty the list" : "Clear";
            StopRename();
            TitleText.Text = mode == Mode.History ? "History" : mode == Mode.Favorites ? "Favorites" : mode == Mode.Overview ? "My lists" : (userList != null ? userList.Name : "");

            if (overview) { BuildOverview(); return; }
            LoadEntries(keepSelection);
        }

        // ---- overview --------------------------------------------------------------------------------------------
        void BuildOverview()
        {
            ListRows.Children.Clear();
            OverviewEmpty.Visibility = Store.Lists.Lists.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var l in Store.Lists.Lists)
            {
                var list = l;
                var row = new Button { Style = (Style)FindResource("MenuRow"), Margin = new Thickness(0, 0, 0, 6), HorizontalContentAlignment = HorizontalAlignment.Stretch };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var nameText = Ui.Text(list.Name, 18, "Fg");
                nameText.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(nameText);
                string countText = list.Items.Count + (list.Items.Count == 1 ? " word" : " words");
                var count = Ui.Text(countText, 14, "FgMuted", null, new Thickness(16, 0, 0, 0));
                count.VerticalAlignment = VerticalAlignment.Center;
                System.Windows.Automation.AutomationProperties.SetName(row, list.Name + ", " + countText);
                Grid.SetColumn(count, 1);
                grid.Children.Add(count);
                row.Content = grid;
                row.Padding = new Thickness(18, 14, 18, 14);
                row.Click += (s, e) => MainWindow.Instance.OpenList(list);
                var card = Ui.Card(row, new Thickness(0, 0, 0, 10));
                card.Padding = new Thickness(2);
                ListRows.Children.Add(card);
            }
        }

        void NewList_Click(object sender, RoutedEventArgs e)
        {
            var l = Store.CreateList(NewListName.Text);
            if (l == null) { NewListName.Focus(); return; }
            NewListName.Text = "";
            MainWindow.Instance.OpenList(l);
        }

        void NewList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) NewList_Click(sender, e);
        }

        // ---- entries ---------------------------------------------------------------------------------------------
        async void LoadEntries(bool keepSelection)
        {
            int my = ++loadToken;
            var src = CurrentEntries();
            if (src == null) return;
            int selectedId = keepSelection && List.SelectedItem is WordItem ? ((WordItem)List.SelectedItem).Tag.WordId : -1;
            var snapshot = src.ToList();
            List<WordItem> loaded = null;
            try
            {
                loaded = await Task.Run(() =>
                {
                    var result = new List<WordItem>();
                    foreach (var e in snapshot)
                    {
                        var tag = Engine.TagById(e.Id);
                        if (tag == null) continue;
                        if (!string.IsNullOrEmpty(e.Conj)) { tag.Conjugated = true; tag.ConjugatedText = e.Conj; }
                        if (!string.IsNullOrEmpty(e.Mark)) tag.HighlightText = e.Mark;
                        result.Add(WordItem.From(tag, removable: true));
                    }
                    return result;
                });
            }
            catch (Exception ex) { App.Log(ex); }
            if (my != loadToken || loaded == null) return;

            items.Clear();
            foreach (var it in loaded) items.Add(it);
            EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (items.Count == 0) { Detail.Clear(); return; }
            var again = selectedId >= 0 ? items.FirstOrDefault(i => i.Tag.WordId == selectedId) : null;
            if (again != null) List.SelectedItem = again;
            else if (!keepSelection) { List.SelectedIndex = 0; }
        }

        void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var it = List.SelectedItem as WordItem;
            if (it != null && (Detail.Current == null || Detail.Current.WordId != it.Tag.WordId)) Detail.Show(it.Tag);
        }

        void OnRowButton(object sender, RoutedEventArgs e)
        {
            var b = e.OriginalSource as Button;
            var it = b == null ? null : b.Tag as WordItem;
            var src = CurrentEntries();
            if (it == null || src == null) return;
            e.Handled = true;
            Store.Remove(src, it.Tag.WordId);   // raises ListsChanged -> reload
            if (Detail.Current != null && Detail.Current.WordId == it.Tag.WordId) Detail.Clear();
        }

        // ---- header buttons --------------------------------------------------------------------------------------
        void Clear_Click(object sender, RoutedEventArgs e)
        {
            var src = CurrentEntries();
            if (src == null || src.Count == 0) return;
            if (MessageBox.Show(Window.GetWindow(this), "Remove all " + src.Count + " entries from this list?", "Takoboto", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Store.Clear(src);
            Detail.Clear();
        }

        void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (userList == null) return;
            if (MessageBox.Show(Window.GetWindow(this), "Delete the list \"" + userList.Name + "\"?", "Takoboto", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Store.DeleteList(userList);
            ShowOverview();
        }

        void Back_Click(object sender, RoutedEventArgs e) { ShowOverview(); }

        // ---- rename ----------------------------------------------------------------------------------------------
        void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (userList == null) return;
            RenameBox.Text = userList.Name;
            TitleText.Visibility = Visibility.Collapsed;
            RenameBox.Visibility = Visibility.Visible;
            RenameBtn.Visibility = Visibility.Collapsed;
            RenameBox.Focus();
            RenameBox.SelectAll();
        }

        void StopRename()
        {
            RenameBox.Visibility = Visibility.Collapsed;
            TitleText.Visibility = Visibility.Visible;
            if (mode == Mode.UserList) RenameBtn.Visibility = Visibility.Visible;
        }

        void Rename_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { StopRename(); e.Handled = true; }
        }

        void Rename_LostFocus(object sender, RoutedEventArgs e) { if (RenameBox.Visibility == Visibility.Visible) Commit(); }

        void Commit()
        {
            if (userList != null) { Store.RenameList(userList, RenameBox.Text); TitleText.Text = userList.Name; }
            StopRename();
        }
    }
}
