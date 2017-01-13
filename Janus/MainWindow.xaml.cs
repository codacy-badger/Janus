﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Janus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DataStore MainStore = new DataStore();
        public static bool Exit = false;
        public static DataProvider Data;
        public static ObservableCollection<Watcher> Watchers;
        private static readonly string Startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static readonly string Shortcut = Path.Combine(Startup, "Janus.url");

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnWatcher_Click(object sender, RoutedEventArgs e)
        {
            var createWindow = new CreateWindow();
            createWindow.Init(this);
            createWindow.Show();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Hide();
            MainStore.Initialise();
            var d = MainStore.Load();
            Data = d.DataProvider;
            Watchers = d.Watchers;

            if (Watchers.Count == 0)
            {
                Show();
            }
            else
            {
                NotificationSystem.Default.Push(NotifcationType.Info, "Janus", "Started in minimised mode. Double click 'Ja' icon to interact.");
            }
            ListBox.ItemsSource = Watchers;
            Watchers.CollectionChanged += Watchers_CollectionChanged;

            CbStartup.IsChecked = File.Exists(Shortcut);
        }

        private void Watchers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateStore();
        }

        public static void UpdateStore()
        {
            MainStore.Store(new JanusData(Watchers, Data));
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var watcher = btn.DataContext as Watcher;

            if (watcher == null) return;
            watcher.Stop();
            Watchers.Remove(watcher);
            NotificationSystem.Default.Push(NotifcationType.Info, "Removed Watcher", "Removed watcher successfully.");
        }

        private void btnSync_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var watcher = btn.DataContext as Watcher;

            watcher?.Synchronise();
        }

        private void CbStartup_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CbStartup.IsChecked.HasValue) return;
            if (CbStartup.IsChecked.Value)
            {
                AddToStartup();
            }
            else
            {
                RemoveFromStartup();
            }
        }

        private void RemoveFromStartup()
        {
            if (File.Exists(Shortcut))
            {
                File.Delete(Shortcut);
            }
        }

        private void AddToStartup()
        {
            using (var writer = new StreamWriter(Shortcut))
            {
                var app = System.Reflection.Assembly.GetExecutingAssembly().Location;
                writer.WriteLine("[InternetShortcut]");
                writer.WriteLine("URL=file:///" + app);
                writer.WriteLine("IconIndex=0");
                var icon = app.Replace('\\', '/');
                writer.WriteLine("IconFile=" + icon);
                writer.Flush();
            }
        }

        private void TrayIcon_OnTrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (Exit) return;
            e.Cancel = true;
            Hide();
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            Show();
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            Exit = true;
            Close();
        }
    }
}