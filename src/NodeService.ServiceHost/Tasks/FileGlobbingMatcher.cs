using Microsoft.Extensions.FileSystemGlobbing;
using System.Collections.Immutable;

namespace NodeService.ServiceHost.Tasks
{
    public class FileGlobbingMatcher:NodeFileSystemMatcherBase
    {
        Matcher _matcher;
        public FileGlobbingMatcher(StringComparison stringComparison)
        {
            _matcher = new Matcher(stringComparison);
        }

        public IEnumerable<string> Match(
            string directoryPath,
            IEnumerable<string> includes,
            IEnumerable<string> excludes,
            ImmutableArray<DateTimeFilter> dateTimeFilters,
            ImmutableArray<FileLengthFilter> fileLengthFilters)
        {
            _matcher.AddIncludePatterns(includes);
            _matcher.AddExcludePatterns(excludes);
            var filePathList = _matcher.GetResultsInFullPath(directoryPath);
            foreach (var filePath in filePathList)
            {
                if (ExecuteDateTimeFilters(filePath, dateTimeFilters) && ExecuteFileLengthFilters(filePath, fileLengthFilters))
                {
                    yield return filePath;
                }
            }
            yield break;
        }


    }
}
