using ChatGptDesktop.Model;
using ChatGptDesktop.ViewModel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Runtime.InteropServices;

namespace ChatGptDesktop.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();
        Key LastKet { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            AllocConsole();
            Loaded += MainWindow_Loaded;
            using (var dbContext = new ChatDbContext())
            {
                // Применение всех миграций при запуске приложения
                dbContext.Database.EnsureCreated();  // Создаст базу данных и все таблицы, если их нет
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.MessageListBox = sender as ListBox;
                viewModel.ScrollToBottom();

            }
        }

        T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            T foundChild = default;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                {
                    foundChild = t;
                    break;
                }
                else
                {
                    foundChild = FindVisualChild<T>(child);
                    if (foundChild != null)
                        break;
                }
            }

            return foundChild;
        }

        private void MessagesListBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.MessageListBox = sender as ListBox;

                // Отложенный вызов ScrollToBottom
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(viewModel.MessageListBox);
                    scrollViewer?.ScrollToBottom();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }


        private async void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null)
                return;

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Если Shift + Enter, добавляем новую строку
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.UserInput += "\n";
                }

                // Отложенная прокрутка, чтобы текст был виден
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.ScrollToEnd();  // Прокрутка в конец
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                e.Handled = true; // Чтобы предотвратить стандартное поведение Enter
            }
            else if (e.Key == Key.Enter)
            {
                // Если просто Enter, вызываем команду отправки сообщения
                if (DataContext is MainViewModel viewModel2)
                {
                    viewModel2.MessageListBox = sender as ListBox;
                    await viewModel2.SendMessage();
                }

                // Отложенная прокрутка, чтобы текст был виден
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.ScrollToEnd();  // Прокрутка в конец
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }



    }
}