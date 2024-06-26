﻿using Newtonsoft.Json.Linq;
using NLog.Filters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Tasks
{
    public static class PathHelper
    {
        public static string GetFilePathDirectoryName(string filePath)
        {
            return Path.GetDirectoryName(filePath) ?? filePath;
        }

        public static IEnumerable<KeyValuePair<string, string>> CalculateRemoteFilePath(
            string localBaseDirectory,
            string remoteBaseDirectory,
            IEnumerable<string> filePathList)
        {
            if (filePathList == null || !filePathList.Any())
            {
                yield break;
            }
            var directoryGroups = filePathList.GroupBy(PathHelper.GetFilePathDirectoryName);
            foreach (var directoryGroup in directoryGroups)
            {
                var relativePath = Path.GetRelativePath(localBaseDirectory, directoryGroup.Key);
                foreach (var localFilePath in directoryGroup)
                {
                    var fileName = Path.GetFileName(localFilePath);
                    var remoteFilePath = Path.Combine(remoteBaseDirectory, relativePath, fileName);
                    remoteFilePath = remoteFilePath.Replace("\\", "/");
                    remoteFilePath = remoteFilePath.Replace("/./", "/");
                    yield return KeyValuePair.Create(localFilePath, remoteFilePath);
                }
            }

            yield break;
        }

        public static string CalcuateRemoteDirectory(string localDirectoryBase, string relativeTo, string remoteDirectoryBase)
        {
            var relativePath = Path.GetRelativePath(localDirectoryBase, relativeTo);
            var targetDirectory = Path.Combine(remoteDirectoryBase, relativePath);
            targetDirectory = targetDirectory.Replace("\\", "/");
            targetDirectory = targetDirectory.Replace("/./", "/");
            return targetDirectory;
        }

    }

    internal class NodeFileSystemEnumerator
    {
        FtpUploadConfigModel _ftpUploadConfig;
        EnumerationOptions _enumerationOptions;
        public NodeFileSystemEnumerator(FtpUploadConfigModel ftpUploadConfig)
        {
            _ftpUploadConfig = ftpUploadConfig;
            _enumerationOptions = new EnumerationOptions()
            {
                MatchCasing = ftpUploadConfig.MatchCasing,
                RecurseSubdirectories = ftpUploadConfig.IncludeSubDirectories,
                MaxRecursionDepth = ftpUploadConfig.MaxRecursionDepth,
                MatchType = ftpUploadConfig.MatchType,
            };
        }

        public IEnumerable<string> EnumerateAllFiles()
        {
            if (!_ftpUploadConfig.IncludeSubDirectories)
            {
                foreach (var filePath in EnumerateFiles(_ftpUploadConfig.Value.LocalDirectory))
                {
                    yield return filePath;
                }
            }
            else
            {
                foreach (var filePath in ExecuteFilters(Directory.EnumerateFiles(_ftpUploadConfig.Value.LocalDirectory, _ftpUploadConfig.Value.SearchPattern ?? "*")))
                {
                    yield return filePath;
                }
                foreach (var directory in EnumerateSubDirectories(_ftpUploadConfig.Value.LocalDirectory))
                {
                    foreach (var filePath in EnumerateFiles(directory))
                    {
                        yield return filePath;
                    }
                }
            }
            yield break;
        }

        public IEnumerable<string> EnumerateSubDirectories(string directory)
        {
            if (!_ftpUploadConfig.IncludeSubDirectories)
            {
                return [];
            }
            if (_ftpUploadConfig.DirectoryFilters == null)
            {
                _ftpUploadConfig.DirectoryFilters = [];
            }
            int skipCount = 0;
            IEnumerable<string> directoriesList = [];
            var firstDirectoryFilter = _ftpUploadConfig.DirectoryFilters?.FirstOrDefault();
            if (firstDirectoryFilter == null || !firstDirectoryFilter.IsSearchPatternFilter())
            {
                directoriesList = EnumerateDirectories(directory, "*");
            }
            else
            {
                directoriesList = EnumerateDirectories(directory, firstDirectoryFilter.Value);
                skipCount = skipCount + 1;
            }

            foreach (var directoryFilter in _ftpUploadConfig.DirectoryFilters.Skip(skipCount))
            {
                if (directoryFilter == null)
                {
                    continue;
                }
                directoriesList = ExecuteFilter(
                    directoriesList,
                    directoryFilter,
                    DirectorySearchPattern);
            }
            return directoriesList;
        }

        public IEnumerable<string> EnumerateFiles(string directory)
        {

            _ftpUploadConfig.Filters ??= [];
            if (!_ftpUploadConfig.Filters.Any(StringEntryExtensions.IsSearchPatternFilter))
            {
                _ftpUploadConfig.Filters.Insert(0, new StringEntry()
                {
                    Name = nameof(FilePathFilter.SearchPattern),
                    Value = _ftpUploadConfig.SearchPattern,
                });
            }

            IEnumerable<string> filePathList = [];
            var firstFilter = _ftpUploadConfig.Filters?.FirstOrDefault();
            if (firstFilter == null)
            {
                return filePathList = EnumerateFiles(directory, "*");
            }
            int skipCount = 0;
            if (firstFilter.IsSearchPatternFilter())
            {
                filePathList = EnumerateFiles(directory, firstFilter.Value);
                skipCount = skipCount + 1;
            }
            else
            {
                filePathList = EnumerateFiles(directory, "*");
            }
            filePathList = ExecuteFilters(filePathList, skipCount);

            return filePathList;
        }

        private IEnumerable<string> ExecuteFilters(IEnumerable<string> filePathList, int skipCount = 0)
        {
            foreach (var filter in _ftpUploadConfig.Filters.Skip(skipCount))
            {
                if (filter == null)
                {
                    continue;
                }
                filePathList = ExecuteFilter(
                    filePathList,
                    filter,
                    FilePathSearchPattern);
            }

            if (_ftpUploadConfig.DateTimeFilters != null && _ftpUploadConfig.DateTimeFilters.Count != 0)
            {
                filePathList = filePathList.Where(DateTimeFilter);
            }

            if (_ftpUploadConfig.LengthFilters != null && _ftpUploadConfig.LengthFilters.Count != 0)
            {
                filePathList = filePathList.Where(FileLengthFilter);
            }

            return filePathList;
        }

        IEnumerable<string> ExecuteFilter(
            IEnumerable<string> pathList,
            StringEntry filter,
            Func<IEnumerable<string>, StringEntry, IEnumerable<string>>? searchPatternHandler = null)
        {
            if (filter.Name == null || string.IsNullOrEmpty(filter.Value))
            {
                return pathList;
            }

            if (!Enum.TryParse(filter.Name, true, out FilePathFilter pathFilter))
            {
                return pathList;
            }
            switch (pathFilter)
            {
                case FilePathFilter.Contains:
                    pathList = from item in pathList
                               where item.Contains(filter.Value)
                               select item;
                    break;
                case FilePathFilter.NotContains:
                    pathList = from item in pathList
                               where !item.Contains(filter.Value)
                               select item;
                    break;
                case FilePathFilter.StartWith:
                    pathList = from item in pathList
                               where item.StartsWith(filter.Value)
                               select item;
                    break;
                case FilePathFilter.NotStartWith:
                    pathList = from item in pathList
                               where !item.StartsWith(filter.Value)
                               select item;
                    break;
                case FilePathFilter.EndsWith:
                    pathList = from item in pathList
                               where item.EndsWith(filter.Value)
                               select item;
                    break;
                case FilePathFilter.NotEndsWith:
                    pathList = from item in pathList
                               where !item.EndsWith(filter.Value)
                               select item;
                    break;
                case FilePathFilter.RegExp:
                    {
                        var regex = new Regex(filter.Value);
                        pathList = from item in pathList
                                   where regex.IsMatch(item)
                                   select item;
                    }

                    break;
                case FilePathFilter.RegExpNotMatch:
                    {
                        var regex = new Regex(filter.Value);
                        pathList = from item in pathList
                                   where !regex.IsMatch(item)
                                   select item;
                    }
                    break;
                case FilePathFilter.SearchPattern:
                    pathList = searchPatternHandler == null ? pathList : searchPatternHandler(pathList, filter);
                    break;
                default:
                    break;
            }

            return pathList;
        }

        IEnumerable<string> FilePathSearchPattern(IEnumerable<string> pathList, StringEntry filter)
        {
            var newPathList = pathList.GroupBy(PathHelper.GetFilePathDirectoryName).SelectMany(x => EnumerateFiles(x.Key, filter.Value)).Distinct();
            return newPathList;
        }

        IEnumerable<string> DirectorySearchPattern(IEnumerable<string> pathList, StringEntry filter)
        {
            var newPathList = pathList.SelectMany(x => EnumerateDirectories(x, filter.Value)).Distinct();
            return newPathList;
        }

        IEnumerable<string> EnumerateFiles(
            string directory,
            string? searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern))
            {
                searchPattern = "*";
            }
            return Directory.EnumerateFiles(directory,
                    searchPattern,
                    _enumerationOptions
                    );
        }

        IEnumerable<string> EnumerateDirectories(
                string directory,
                string? searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern))
            {
                searchPattern = "*";
            }
            return Directory.EnumerateDirectories(
                    directory,
                    searchPattern,
                    _enumerationOptions
                    );
        }

        bool FileLengthFilter(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var matchedCount = 0;
            foreach (var fileLengthFilter in _ftpUploadConfig.LengthFilters)
            {
                var value0 = CalcuateLength(fileLengthFilter.LengthUnit, fileLengthFilter.Values[0]);
                var value1 = CalcuateLength(fileLengthFilter.LengthUnit, fileLengthFilter.Values[1]);
                switch (fileLengthFilter.Operator)
                {
                    case CompareOperator.LessThan:
                        if (fileInfo.Length < value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.GreatThan:
                        if (fileInfo.Length > value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.LessThanEqual:
                        if (fileInfo.Length <= value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.GreatThanEqual:
                        if (fileInfo.Length >= value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.Equals:
                        if (fileInfo.Length == value0)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.WithinRange:
                        if (fileInfo.Length >= value0 && fileInfo.Length <= value1)
                        {
                            matchedCount++;
                        }
                        break;
                    case CompareOperator.OutOfRange:
                        if (!(fileInfo.Length >= value0 && fileInfo.Length <= value1))
                        {
                            matchedCount++;
                        }
                        break;
                    default:
                        break;
                }
            }
            return matchedCount > 0;

            static long CalcuateLength(BinaryLengthUnit binaryLengthUnit, double value)
            {
                var length = binaryLengthUnit switch
                {
                    BinaryLengthUnit.Byte => value,
                    BinaryLengthUnit.KB => value * 1024,
                    BinaryLengthUnit.MB => value * 1024 * 1024,
                    BinaryLengthUnit.GB => value * 1024 * 1024 * 1024,
                    BinaryLengthUnit.PB => value * 1024 * 1024 * 1024 * 1024,
                    _ => throw new NotImplementedException(),
                };
                return (long)length;
            }
        }

        bool DateTimeFilter(string filePath)
        {
            bool isMatched = false;
            var lastWriteTime = File.GetLastWriteTime(filePath);
            var creationTime = File.GetCreationTime(filePath);
            foreach (var dateTimeFilter in _ftpUploadConfig.DateTimeFilters)
            {
                switch (dateTimeFilter.Kind)
                {
                    case DateTimeFilterKind.DateTime:
                        isMatched = dateTimeFilter.IsMatched(lastWriteTime);
                        break;
                    case DateTimeFilterKind.TimeOnly:
                        isMatched = dateTimeFilter.IsMatched(TimeOnly.FromDateTime(lastWriteTime));
                        break;
                    case DateTimeFilterKind.Days:
                    case DateTimeFilterKind.Hours:
                    case DateTimeFilterKind.Minutes:
                    case DateTimeFilterKind.Seconds:
                        isMatched = dateTimeFilter.IsMatched(DateTime.Now - lastWriteTime);
                        break;
                    default:
                        break;
                }

                if (isMatched)
                {
                    break;
                }
            }
            return isMatched;
        }


    }
}