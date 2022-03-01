using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DetAct.Data.Services
{
    public class DirectoryWatcherService : DisposableService
    {
        private readonly ILogger _logger;

        private readonly FileSystemWatcher fileSystemWatcher = new();

        public event FileSystemEventHandler OnDirectoryHasChanged;

        public IList<string> FileList {
            get {
                var directoryInfo = new DirectoryInfo(DirectoryPath);
                return directoryInfo.GetFiles(FileFilter)
                    .OrderBy(file => file.Name)
                    .Select(file => file.Name.Replace(oldValue: FileFilter[FileFilter.IndexOf('.')..], newValue: ""))
                    .ToList();
            }

            private set { }
        }

        public string DirectoryPath { get; private set; } = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), @"InputFiles")).FullName;

        public string FileFilter { get; private set; } = "*.*";

        public DirectoryWatcherService(ILogger<DirectoryWatcherService> logger, IHostApplicationLifetime applicationLifetime)
            : base(applicationLifetime)
        {
            _logger = logger;

            fileSystemWatcher.NotifyFilter = NotifyFilters.Attributes
                | NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastAccess
                | NotifyFilters.LastWrite
                | NotifyFilters.Security
                | NotifyFilters.Size;
        }

        public void Initialize(string fileFiler)
        {
            FileFilter = fileFiler;

            fileSystemWatcher.Filter = FileFilter;
            fileSystemWatcher.Path = DirectoryPath;

            fileSystemWatcher.Created += OnChanged;
            fileSystemWatcher.Renamed += OnChanged;
            fileSystemWatcher.Deleted += OnChanged;

            fileSystemWatcher.EnableRaisingEvents = true;
        }

        public string CreatePathForFile(string fileName)
        {
            return $"{DirectoryPath}/{fileName}.btml";
        }

        public FileStream CreateNewFile(string fileName)
        {
            if(FileList.Contains(fileName))
                return null;

            return File.Create(CreatePathForFile(fileName));
        }

        public void SaveFile(string fileName, string contents)
        {
            if(string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contents))
                return;

            File.WriteAllText(path: CreatePathForFile(fileName), contents, Encoding.UTF8);
        }

        public FileStream GetFile(string fileName)
        {
            if(!FileList.Contains(fileName))
                return null;

            return File.OpenRead(CreatePathForFile(fileName));
        }

        public void RemoveFile(string fileName)
        {
            File.Delete(CreatePathForFile(fileName));
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            OnDirectoryHasChanged?.Invoke(sender, e);
        }

        public override void Dispose()
        {
            if(fileSystemWatcher.EnableRaisingEvents) {
                fileSystemWatcher.EnableRaisingEvents = false;

                fileSystemWatcher.Created -= OnChanged;
                fileSystemWatcher.Renamed -= OnChanged;
                fileSystemWatcher.Deleted -= OnChanged;
            }

            GC.SuppressFinalize(obj: this);
        }
    }
}
