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
                foreach (var localFilePath in filePathList)
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

        public static string CalcuateTargetDirectory(string localDirectoryBase, string relativeTo, string remoteDirectoryBase)
        {
            var relativePath = Path.GetRelativePath(localDirectoryBase, relativeTo);
            var targetDirectory = Path.Combine(remoteDirectoryBase, relativePath);
            targetDirectory = targetDirectory.Replace("\\", "/");
            targetDirectory = targetDirectory.Replace("/./", "/");
            return targetDirectory;
        }

    }

    internal class NodeFileSyncDirectoryEnumerator
    {
        FtpUploadConfigModel _ftpUploadConfig;
        public NodeFileSyncDirectoryEnumerator(FtpUploadConfigModel ftpUploadConfig)
        {
            _ftpUploadConfig = ftpUploadConfig;
        }

        public IEnumerable<string> EnumerateFiles(string directory)
        {

            if (_ftpUploadConfig.Filters == null)
            {
                _ftpUploadConfig.Filters = [];
            }
            if (!_ftpUploadConfig.Filters.Any(StringEntryExtensions.IsSearchPatternFilter))
            {
                _ftpUploadConfig.Filters.Insert(0, new StringEntry()
                {
                    Name = nameof(FilePathFilter.SearchPattern),
                    Value = _ftpUploadConfig.SearchPattern,
                });
            }

            IEnumerable<string> localFiles = [];
            var firstFilter = _ftpUploadConfig.Filters?.FirstOrDefault();
            if (firstFilter == null)
            {
                return localFiles;
            }
            if (firstFilter.IsSearchPatternFilter())
            {
                localFiles = EnumerateFiles(directory, _ftpUploadConfig, firstFilter.Value);
            }
            else
            {
                localFiles = EnumerateFiles(directory, _ftpUploadConfig, "*");
            }
            foreach (var filter in _ftpUploadConfig.Filters)
            {
                if (filter.Name == null || string.IsNullOrEmpty(filter.Value))
                {
                    continue;
                }

                if (!Enum.TryParse(filter.Name, true, out FilePathFilter pathFilter))
                {
                    continue;
                }
                switch (pathFilter)
                {
                    case FilePathFilter.Contains:
                        localFiles = from item in localFiles
                                     where item.Contains(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.NotContains:
                        localFiles = from item in localFiles
                                     where !item.Contains(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.StartWith:
                        localFiles = from item in localFiles
                                     where item.StartsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.NotStartWith:
                        localFiles = from item in localFiles
                                     where !item.StartsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.EndsWith:
                        localFiles = from item in localFiles
                                     where item.EndsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.NotEndsWith:
                        localFiles = from item in localFiles
                                     where !item.EndsWith(filter.Value)
                                     select item;
                        break;
                    case FilePathFilter.RegExp:
                        {
                            var regex = new Regex(filter.Value);
                            localFiles = from item in localFiles
                                         where regex.IsMatch(item)
                                         select item;
                        }

                        break;
                    case FilePathFilter.RegExpNotMatch:
                        {
                            var regex = new Regex(filter.Value);
                            localFiles = from item in localFiles
                                         where !regex.IsMatch(item)
                                         select item;
                        }
                        break;
                    case FilePathFilter.SearchPattern:
                        if (firstFilter == filter)
                        {
                            continue;
                        }
                        localFiles = localFiles.GroupBy(PathHelper.GetFilePathDirectoryName)
                                               .SelectMany(x => EnumerateFiles(x.Key, _ftpUploadConfig, filter.Value)).Distinct();
                        break;
                    default:
                        break;
                }
            }

            if (_ftpUploadConfig.DateTimeFilters != null && _ftpUploadConfig.DateTimeFilters.Count != 0)
            {
                localFiles = localFiles.Where(DateTimeFilter);
            }

            if (_ftpUploadConfig.LengthFilters != null && _ftpUploadConfig.LengthFilters.Count != 0)
            {
                localFiles = localFiles.Where(FileLengthFilter);
            }

            return localFiles.ToImmutableArray();



        }

        string[] EnumerateFiles(
            string directory,
            FtpUploadConfigModel ftpUploadConfig,
            string? searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern))
            {
                return [];
            }
            return Directory.GetFiles(directory,
                searchPattern,
                new EnumerationOptions()
                {
                    MatchCasing = ftpUploadConfig.MatchCasing,
                    RecurseSubdirectories = ftpUploadConfig.IncludeSubDirectories,
                    MaxRecursionDepth = ftpUploadConfig.MaxRecursionDepth,
                    MatchType = ftpUploadConfig.MatchType,
                }
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
