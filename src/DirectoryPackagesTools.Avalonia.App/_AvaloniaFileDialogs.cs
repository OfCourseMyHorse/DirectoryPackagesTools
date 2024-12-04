using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

using Avalonia.Platform.Storage;

namespace DirectoryPackagesTools
{
    internal static class _AvaloniaFileDialogs
    {
        public static async Task<IStorageFile?> TryOpenForRead(this Avalonia.Visual visual, string filterName, string filterExt)
        {
            return await TryOpenForRead(visual, (filterName, filterExt));
        }

        public static async Task<IStorageFile?> TryOpenForRead(this Avalonia.Visual visual, params (string filterName,string filterExt)[] fileFilters)
        {
            // Get the current top-level window

            if (visual == null) return null;            
            var topLevel = TopLevel.GetTopLevel(visual);
            if (topLevel == null) return null;            

            // Create OpenFilePickerOptions
            var options = new FilePickerOpenOptions
            {
                Title = "Open File",
                AllowMultiple = false
            };

            foreach(var (filterName, filterExt) in fileFilters) options.AddFilter(filterName, filterExt);

            // Open the OpenFileDialog
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

            if (files.Count <= 0) return null;

            return files[0];
        }

        private static FilePickerFileType? AddFilter(this FilePickerOpenOptions dst, string filterName, string filterExt)
        {
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (string.IsNullOrEmpty(filterName)) throw new ArgumentNullException(nameof(filterName));
            if (string.IsNullOrEmpty(filterExt)) throw new ArgumentNullException(nameof(filterExt));

            dst.FileTypeFilter ??= new List<FilePickerFileType>();

            if (dst.FileTypeFilter is not ICollection<FilePickerFileType> dstList)
            {
                return null;
            }

            var filter = new FilePickerFileType(filterName);
            filter.Patterns = new[] { filterExt };

            dstList.Add(filter);

            return filter;
        }
        
        public static async Task<TResult?> CastResultTo<TResult>(this Task<IStorageFile?> task)
        {
            return await task.ContinueWith(t => t.Result.CastTo<TResult>());
        }

        public static TResult? CastTo<TResult>(this IStorageFile? file)
        {
            if (file == null) return default;

            if (typeof(TResult) == typeof(IStorageFile)) return (TResult)file;

            if (typeof(TResult) == typeof(Uri)) return (TResult)(Object)file.Path;

            var path = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return default;

            if (typeof(TResult) == typeof(System.IO.FileInfo))
            {
                return (TResult)(Object)new System.IO.FileInfo(path);
            }

            if (typeof(TResult) == typeof(string))
            {
                return (TResult)(Object)path;
            }

            throw new NotImplementedException();
        }



        public static async Task<IStorageFolder?> OpenFolderPicker(this Avalonia.Visual visual)
        {
            // Get the current top-level window
            var topLevel = TopLevel.GetTopLevel(visual);

            if (topLevel == null) return null;

            // Create FolderPickerOpenOptions
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false
            };

            // Open the FolderPicker
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);

            if (folders.Count <= 0) return null;
            
            return folders[0];
        }

        public static async Task<TResult?> CastResultTo<TResult>(this Task<IStorageFolder?> task)
        {
            return await task.ContinueWith(t => t.Result.CastTo<TResult>());
        }

        public static TResult? CastTo<TResult>(this IStorageFolder? file)
        {
            if (file == null) return default;

            if (typeof(TResult) == typeof(IStorageFile)) return (TResult)file;

            if (typeof(TResult) == typeof(Uri)) return (TResult)(Object)file.Path;

            var path = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return default;

            if (typeof(TResult) == typeof(System.IO.DirectoryInfo))
            {
                return (TResult)(Object)new System.IO.DirectoryInfo(path);
            }

            if (typeof(TResult) == typeof(string))
            {
                return (TResult)(Object)path;
            }

            throw new NotImplementedException();
        }
    }
}
