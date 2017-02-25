﻿using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Janus.Filters;
using Janus.Properties;

namespace Janus
{
    public class Watcher
    {
        public SyncData Data { get; }
        public ISynchroniser Synchroniser { get; }

        /// <summary>
        /// List of files that have been deleted in the WatchPath directory.
        /// Used for manual synchronisation.
        /// Will be empty when it's automatic sync.
        /// </summary>
        private readonly List<string> _delete = new List<string>();

        /// <summary>
        /// List of files that have been added to or modified in the WatchPath directory.
        /// Used for manual synchronisation.
        /// Will be empty when it's automatic sync.
        /// </summary>
        private readonly List<string> _copy = new List<string>();

        /// <summary>
        /// This can only be set when instantiating.
        /// This will disable all file watching.
        /// Used for testing, should never be true in normal use.
        /// </summary>
        public readonly bool Observe;

        /// <summary>
        /// Watches for file additions + modifications in the WatchPath directory.
        /// This only triggers on "LastWrite" so as to help mitigate double events,
        ///  one for initial creation and one for when it's written to.
        /// </summary>
        private readonly FileSystemWatcher _writeWatcher;

        /// <summary>
        /// Watches for file deletions in the WatchPath directory.
        /// </summary>
        private readonly FileSystemWatcher _deleteWatcher;

        public Watcher(string watchPath, string endPath, bool addFiles, bool deleteFiles, List<IFilter> filters, bool recursive, bool observe = false)
        {
            Data = new SyncData
            {
                AddFiles = addFiles,
                DeleteFiles = deleteFiles,
                Filters = filters,
                Recursive = recursive,
                WatchDirectory = watchPath,
                SyncDirectory = endPath
            };

            Synchroniser = new MetaDataSynchroniser(Data);

            if (observe)
            {
                Observe = true;
                return;
            }

            _writeWatcher = new FileSystemWatcher
            {
                Path = watchPath,
                NotifyFilter = NotifyFilters.LastWrite
            };

            _deleteWatcher = new FileSystemWatcher
            {
                Path = watchPath
            };


            _writeWatcher.Changed += WriteWatcherChanged;
            _deleteWatcher.Deleted += WriteWatcherDeleted;
            EnableEvents();
        }

        public void AddFilter(IFilter filter)
        {
            Data.Filters.Add(filter);
        }

        /// <summary>
        /// Enables events from both FileSystemWatcher classes
        /// (copy + delete)
        /// </summary>
        public void EnableEvents()
        {
            _writeWatcher.EnableRaisingEvents = true;
            _deleteWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Asynchronously does a full synchronisation, making sure the
        /// WatchPath matches up with the EndPath.
        /// </summary>
        /// <returns>Async Task</returns>
        public Task DoInitialSynchronise() => Synchroniser.TryFullSynchroniseAsync();

        /// <summary>
        /// Called when the user prompts for a manual synchronisation.
        /// It will copy all the files that have been modified + deleted
        /// since we started tracking this session.
        /// </summary>
        public void Synchronise()
        {
            var copyCount = _copy.Count;
            var deleteCount = _delete.Count;
            if (copyCount + deleteCount == 0)
            {
                NotificationSystem.Default.Push(NotifcationType.Info, "Sync Completed.", "No files were changed.");
                return;
            }

            foreach (var file in _copy)
            {
                Logging.WriteLine(Resources.Manual_Copying_Target, file);
                Synchroniser.AddAsync(file);
            }
             
            foreach (var file in _delete)
            {
                Logging.WriteLine(Resources.Manual_Deleting_Target, file);
                Synchroniser.DeleteAsync(file);
            }

            _copy.Clear();
            _delete.Clear();

            NotificationSystem.Default.Push(NotifcationType.Info, "Sync Completed.",
                $"Finished copying {copyCount} files, and deleting {deleteCount} files.");
        }

        public async Task SynchroniseAsync()
        {
            await Task.Run(() => Synchronise());
        }

        /// <summary>
        /// Event recieved when a file in WatchPath is deleted
        /// </summary>
        /// <param name="sender">FileSystemWatcher</param>
        /// <param name="e">Event Parameters (contains file path)</param>
        private void WriteWatcherDeleted(object sender, FileSystemEventArgs e)
        {
            foreach (var filter in Data.Filters)
            {
                if (filter.ShouldExcludeFile(e.FullPath))
                {
                    return;
                }
            }

            if(_copy.Contains(e.FullPath))
            {
                Logging.WriteLine(Resources.Auto_Removing_Target, e.FullPath);
                var succ = _copy.Remove(e.FullPath);
                Logging.WriteLine(Resources.Auto_Removed_Target, succ);
            }
            if (Data.DeleteFiles)
            {
                Logging.WriteLine(Resources.Auto_Deleting_Target, e.FullPath);
                Synchroniser.DeleteAsync(e.FullPath);
                Logging.WriteLine(Resources.Auto_Deleted_Target, e.FullPath);
            }
            else
            {
                Logging.WriteLine(Resources.Auto_Mark_Delete_Target, e.FullPath);
                _delete.Add(e.FullPath);
            }
        }

        /// <summary>
        /// Stores the last path that was seen.
        /// Used for filtering out "double" events.
        /// TODO: Make this a lot better, very hacky!
        /// </summary>
        private string _lastPath = "";

        /// <summary>
        /// Event recieved when a file in WatchPath is modified / created
        /// </summary>
        /// <param name="sender">FileSystemWatcher</param>
        /// <param name="e">Event Parameters (contains file path)</param>
        private void WriteWatcherChanged(object sender, FileSystemEventArgs e)
        {
            foreach (var filter in Data.Filters)
            {
                if (filter.ShouldExcludeFile(e.FullPath))
                {
                    return;
                }
            }

            if (_lastPath == e.FullPath)
            {
                return;
            }
            _lastPath = e.FullPath;
            Logging.WriteLine(e.ChangeType);
            if (_delete.Contains(e.FullPath))
            {
                Logging.WriteLine(Resources.Auto_Remove_Delete_Target, e.FullPath);
                var succ = _delete.Remove(e.FullPath);
                Logging.WriteLine(Resources.Auto_Remove_Delete_List, succ);
            }
            if (Data.AddFiles)
            {
                Logging.WriteLine(Resources.Auto_Copying_Target, e.FullPath);
                Synchroniser.AddAsync(e.FullPath);
                Logging.WriteLine(Resources.Auto_Copied_Target, e.FullPath);
            }
            else
            {
                Logging.WriteLine(Resources.Auto_Mark_Copy_Target, e.FullPath);
                _copy.Add(e.FullPath);
            }
        }

        /// <summary>
        /// Stops all events and cleans up the FileSystemWatcher classes.
        /// After Stop is called the class cannot start watching again.
        /// To disable events temporarily use DisableEvents.
        /// </summary>
        public void Stop()
        {
            Logging.WriteLine(Resources.Watcher_Stop_Target, Data.WatchDirectory);
            DisableEvents();
            _writeWatcher.Dispose();
            _writeWatcher.Dispose();
        }

        /// <summary>
        /// Stops any events from being raised.
        /// Use EnableEvents to turn events back on.
        /// </summary>
        public void DisableEvents()
        {
            _deleteWatcher.EnableRaisingEvents = false;
            _writeWatcher.EnableRaisingEvents = false;
        }

        /// <summary>
        /// Equality comparison check wrapper.
        /// Discards any objects that aren't Watchers.
        /// </summary>
        /// <param name="obj">Watcher to compare with</param>
        /// <returns>If object is a Watcher that has equal properties</returns>
        public override bool Equals(object obj)
        {
            var wobj = obj as Watcher;
            return wobj != null && Equals(wobj);
        }

        /// <summary>
        /// Compares properties with specified watcher to see if they are
        /// equal to each other.
        /// Used in tests.
        /// </summary>
        /// <param name="other">Watcher to check against</param>
        /// <returns>If Watcher is equal to this one</returns>
        private bool Equals(Watcher other)
        {
            return Observe == other.Observe &&
                   Data.Equals(other.Data);
        }

        /// <summary>
        /// Computes a "unique" hash code for this Watcher.
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Observe.GetHashCode();
                hashCode = (hashCode*397) ^ (Data.WatchDirectory?.GetHashCode() ?? 0);
                hashCode = (hashCode*397) ^ (Synchroniser?.GetHashCode() ?? 0);
                hashCode = (hashCode*397) ^ Data.Recursive.GetHashCode();
                return hashCode;
            }
        }
    }
}
